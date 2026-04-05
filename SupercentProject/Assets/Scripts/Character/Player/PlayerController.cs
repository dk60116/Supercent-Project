using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private static readonly int MoveHash = Animator.StringToHash("bMove");
    private static readonly int PickingHash = Animator.StringToHash("tPicking");
    private static readonly int PickStateHash = Animator.StringToHash("Base Layer.Pick");
    private static readonly int PickRunStateHash = Animator.StringToHash("Base Layer.PickRun");
    private const int MaxTickMiningDebugTargets = 16;

    [SerializeField]
    private List<PortableResource> oreStack, handcuffsStack, moneyStack;
    [SerializeField]
    private GameObject oreHolder, handcuffsHolder, moneyHodler;

    [SerializeField, ReadOnly]
    private int maxOre, maxHandcuff, maxMoney;
    [SerializeField, ReadOnly]
    private int oreCount, handcuffsCount, moneyCount;

    [SerializeField]
    private Image maxIcon;

    [SerializeField]
    private bool showMiningRayDebug = true;
    [SerializeField]
    private float miningRayHeight = 0.2f;
    [SerializeField]
    private float miningRayDistance = 0.5f;
    [SerializeField]
    private Vector3 miningBoxHalfExtents = new Vector3(0.25f, 0.25f, 0.25f);
    [SerializeField]
    private Color defaultRayColor = Color.yellow;
    [SerializeField]
    private Color oreHitRayColor = Color.green;
    [SerializeField]
    private Color blockedRayColor = Color.red;

    [Header("Tick Mining Debug")]
    [SerializeField]
    private bool showTickMiningDebug = true;
    [SerializeField]
    private float tickMiningHeight = 0.5f;
    [SerializeField]
    private float tickMiningBack = 0.25f;
    [SerializeField]
    private Color tickMiningIdleColor = Color.cyan;
    [SerializeField]
    private Color tickMiningOreColor = Color.magenta;
    [SerializeField]
    private Color tickMiningBlockedColor = Color.red;

    private Player player;
    private bool pickTriggerPending;
    private bool wasPickingAnimationPlaying;
    private Coroutine ableMaxIconCoroutine;
    private readonly Collider[] tickMiningDebugColliders = new Collider[MaxTickMiningDebugTargets];
    private readonly Vector3[] tickMiningDebugOrePositions = new Vector3[MaxTickMiningDebugTargets];

    [SerializeField, ReadOnly]
    private bool isHittingOre;
    [SerializeField, ReadOnly]
    private string hitTargetName;
    [SerializeField, ReadOnly]
    private Vector3 debugRayOrigin;
    [SerializeField, ReadOnly]
    private Vector3 debugRayEnd;
    [SerializeField, ReadOnly]
    private Vector3 debugHitPoint;
    [SerializeField, ReadOnly]
    private Vector3 debugCastEndCenter;
    [SerializeField, ReadOnly]
    private bool debugUsesBoxCast;
    [SerializeField, ReadOnly]
    private string lockedOreName;

    [SerializeField, ReadOnly]
    private bool enterMine;
    [SerializeField, ReadOnly]
    private bool enterOre;

    private Resource lockedOre;

    [SerializeField, ReadOnly]
    private float tickTime;
    [SerializeField, ReadOnly]
    private Vector3 tickMiningCenter;
    [SerializeField, ReadOnly]
    private int tickMiningOverlapCount;
    [SerializeField, ReadOnly]
    private int tickMiningOreCount;

    private void Awake()
    {
        player = GetComponentInParent<Player>();

        for (int i = 0; i < oreStack.Count; ++i)
            oreStack[i].gameObject.SetActive(false);
        for (int i = 0; i < handcuffsStack.Count; ++i)
            handcuffsStack[i].gameObject.SetActive(false);
        for (int i = 0; i < moneyStack.Count; ++i)
            moneyStack[i].gameObject.SetActive(false);

        maxIcon.gameObject.SetActive(false);
    }

    public void AutoFillResourceStacks()
    {
        FillStackFromHolder(oreHolder, ref oreStack);
        FillStackFromHolder(handcuffsHolder, ref handcuffsStack);
        FillStackFromHolder(moneyHodler, ref moneyStack);

        maxOre = oreStack != null ? oreStack.Count : 0;
        maxHandcuff = handcuffsStack != null ? handcuffsStack.Count : 0;
        maxMoney = moneyStack != null ? moneyStack.Count : 0;

        oreCount = Mathf.Clamp(oreCount, 0, maxOre);
        handcuffsCount = Mathf.Clamp(handcuffsCount, 0, maxHandcuff);
        moneyCount = Mathf.Clamp(moneyCount, 0, maxMoney);
    }

    private void FillStackFromHolder(GameObject holder, ref List<PortableResource> stack)
    {
        if (stack == null)
        {
            stack = new List<PortableResource>();
        }
        else
        {
            stack.Clear();
        }

        if (holder == null)
        {
            return;
        }

        CollectPortableResources(holder.transform, stack);
    }

    private void CollectPortableResources(Transform parent, List<PortableResource> stack)
    {
        PortableResource portableResource = parent.GetComponent<PortableResource>();
        if (portableResource != null)
        {
            stack.Add(portableResource);
        }

        for (int i = 0; i < parent.childCount; ++i)
        {
            CollectPortableResources(parent.GetChild(i), stack);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        MovePlayer();

        if (enterMine)
            TickMining();
        else
            player.Animator.SetFloat("fEquip", handcuffsCount > 0 ? 1f : 0f);
    }

    public void EnterMine(bool enter)
    {
        enterMine = enter;
        
        player.TakeMiningTool(enterMine);

        handcuffsHolder.SetActive(!enterMine);
    }

    void TickMining()
    {
        RaycastHit hit;

        tickMiningCenter = GetTickMiningCenter(tickMiningHeight);
        DebugDrawTickMining();

        if (TryGetTickMiningHit(out hit))
        {
            if (hit.collider.CompareTag("Ore"))
            {
                enterOre = true;

                if (tickTime == 0f || tickTime >= player.EquipMiningTool.Status.pickingDelay)
                {
                    tickTime = 0f;

                    switch (player.GetToolType())
                    {
                        case MiningToolType.None:
                            break;
                        case MiningToolType.Pickaxe:
                            player.Animator.SetTrigger("tPicking");
                            break;
                        case MiningToolType.Screw:
                            PickingEvent_Screw();
                            break;
                        case MiningToolType.Vehicle:
                            PickingEvent_Vehicle();
                            break;
                    }
                }

                tickTime += Time.deltaTime;
            }
            else
            {
                enterOre = false;
                tickTime = 0f;
                player.Animator.ResetTrigger("tPicking");
            }
        }
        else
        {
            enterOre = false;
            tickTime = 0f;
            player.Animator.ResetTrigger("tPicking");
        }
    }

    private Vector3 GetTickMiningCenter(float height)
    {
        return transform.position + Vector3.up * height + transform.forward * -tickMiningBack;
    }

    private float GetMiningRange()
    {
        if (player != null && player.EquipMiningTool != null)
        {
            return player.EquipMiningTool.Status.rnage;
        }

        return miningRayDistance;
    }

    private Vector3 GetMiningBoxHalfExtents()
    {
        Vector3 halfExtents = miningBoxHalfExtents;

        if (player != null && player.EquipMiningTool != null)
        {
            float miningWidth = player.EquipMiningTool.Status.width;
            if (miningWidth > 0f)
            {
                halfExtents.x = miningWidth * 0.5f;
            }
        }

        return halfExtents;
    }

    private bool IsVehicleMiningTool()
    {
        return player != null && player.GetToolType() == MiningToolType.Vehicle;
    }

    private bool TryGetTickMiningHit(out RaycastHit hit)
    {
        float miningRange = GetMiningRange();

        if (IsVehicleMiningTool())
        {
            Vector3 boxHalfExtents = GetMiningBoxHalfExtents();

            return Physics.BoxCast(
                tickMiningCenter,
                boxHalfExtents,
                transform.forward,
                out hit,
                transform.rotation,
                miningRange);
        }

        return Physics.Raycast(tickMiningCenter, transform.forward, out hit, miningRange);
    }

    private void DebugDrawTickMining()
    {
        float miningRange = GetMiningRange();

        debugRayOrigin = tickMiningCenter;
        debugRayEnd = tickMiningCenter + transform.forward.normalized * miningRange;
        debugHitPoint = debugRayEnd;
        debugCastEndCenter = debugRayEnd;
        debugUsesBoxCast = IsVehicleMiningTool();
        hitTargetName = string.Empty;
        lockedOreName = string.Empty;
        isHittingOre = false;
        tickMiningOverlapCount = 0;
        tickMiningOreCount = 0;

        for (int i = 0; i < tickMiningDebugOrePositions.Length; ++i)
        {
            tickMiningDebugOrePositions[i] = Vector3.zero;
        }

        if (showTickMiningDebug)
        {
            if (debugUsesBoxCast)
            {
                Vector3 boxHalfExtents = GetMiningBoxHalfExtents();
                Vector3 overlapCenter = tickMiningCenter + transform.forward.normalized * (miningRange * 0.5f);
                Vector3 overlapHalfExtents = new Vector3(
                    boxHalfExtents.x,
                    boxHalfExtents.y,
                    boxHalfExtents.z + miningRange * 0.5f);

                tickMiningOverlapCount = Physics.OverlapBoxNonAlloc(
                    overlapCenter,
                    overlapHalfExtents,
                    tickMiningDebugColliders,
                    transform.rotation);
            }
            else
            {
                tickMiningOverlapCount = Physics.OverlapSphereNonAlloc(
                    tickMiningCenter,
                    miningRange,
                    tickMiningDebugColliders);
            }

            for (int i = 0; i < tickMiningOverlapCount; ++i)
            {
                Collider overlap = tickMiningDebugColliders[i];
                if (overlap == null)
                {
                    continue;
                }

                if (overlap.CompareTag("Ore") && tickMiningOreCount < tickMiningDebugOrePositions.Length)
                {
                    tickMiningDebugOrePositions[tickMiningOreCount++] = overlap.bounds.ClosestPoint(tickMiningCenter);
                }

                tickMiningDebugColliders[i] = null;
            }
        }

        RaycastHit debugHit;
        if (TryGetTickMiningHit(out debugHit))
        {
            debugHitPoint = debugHit.point;
            debugCastEndCenter = tickMiningCenter + transform.forward.normalized * debugHit.distance;
            hitTargetName = debugHit.collider.name;
            isHittingOre = debugHit.collider.CompareTag("Ore");
            if (isHittingOre)
            {
                lockedOreName = debugHit.collider.name;
            }
        }

        if (!showTickMiningDebug)
        {
            return;
        }

        Color rayColor = isHittingOre
            ? tickMiningOreColor
            : string.IsNullOrEmpty(hitTargetName) ? tickMiningIdleColor : tickMiningBlockedColor;

        Vector3 rayTarget = string.IsNullOrEmpty(hitTargetName) ? debugRayEnd : debugHitPoint;
        Debug.DrawLine(debugRayOrigin, rayTarget, rayColor);
        Debug.DrawRay(debugRayOrigin, Vector3.up * 0.15f, tickMiningIdleColor);

        if (debugUsesBoxCast)
        {
            Vector3 boxHalfExtents = GetMiningBoxHalfExtents();
            Vector3 boxEndCenter = string.IsNullOrEmpty(hitTargetName) ? debugRayEnd : debugCastEndCenter;
            DrawDebugWireBox(debugRayOrigin, transform.rotation, boxHalfExtents, rayColor);
            DrawDebugWireBox(boxEndCenter, transform.rotation, boxHalfExtents, rayColor);
            DrawDebugBoxCastSweep(debugRayOrigin, boxEndCenter, transform.rotation, boxHalfExtents, rayColor);
        }

        if (!string.IsNullOrEmpty(hitTargetName))
        {
            Debug.DrawRay(debugHitPoint, Vector3.up * 0.2f, rayColor);
        }
    }

    private void DrawDebugBoxCastSweep(Vector3 startCenter, Vector3 endCenter, Quaternion rotation, Vector3 halfExtents, Color color)
    {
        Vector3 right = rotation * Vector3.right * halfExtents.x;
        Vector3 up = rotation * Vector3.up * halfExtents.y;

        Debug.DrawLine(startCenter + right + up, endCenter + right + up, color);
        Debug.DrawLine(startCenter + right - up, endCenter + right - up, color);
        Debug.DrawLine(startCenter - right + up, endCenter - right + up, color);
        Debug.DrawLine(startCenter - right - up, endCenter - right - up, color);
    }

    private void DrawDebugWireBox(Vector3 center, Quaternion rotation, Vector3 halfExtents, Color color)
    {
        Vector3 right = rotation * Vector3.right * halfExtents.x;
        Vector3 up = rotation * Vector3.up * halfExtents.y;
        Vector3 forward = rotation * Vector3.forward * halfExtents.z;

        Vector3 topFrontRight = center + right + up + forward;
        Vector3 topFrontLeft = center - right + up + forward;
        Vector3 topBackRight = center + right + up - forward;
        Vector3 topBackLeft = center - right + up - forward;
        Vector3 bottomFrontRight = center + right - up + forward;
        Vector3 bottomFrontLeft = center - right - up + forward;
        Vector3 bottomBackRight = center + right - up - forward;
        Vector3 bottomBackLeft = center - right - up - forward;

        Debug.DrawLine(topFrontRight, topFrontLeft, color);
        Debug.DrawLine(topFrontLeft, topBackLeft, color);
        Debug.DrawLine(topBackLeft, topBackRight, color);
        Debug.DrawLine(topBackRight, topFrontRight, color);

        Debug.DrawLine(bottomFrontRight, bottomFrontLeft, color);
        Debug.DrawLine(bottomFrontLeft, bottomBackLeft, color);
        Debug.DrawLine(bottomBackLeft, bottomBackRight, color);
        Debug.DrawLine(bottomBackRight, bottomFrontRight, color);

        Debug.DrawLine(topFrontRight, bottomFrontRight, color);
        Debug.DrawLine(topFrontLeft, bottomFrontLeft, color);
        Debug.DrawLine(topBackLeft, bottomBackLeft, color);
        Debug.DrawLine(topBackRight, bottomBackRight, color);
    }

    private void PickingEvent_Pickaxe()
    {
        RaycastHit hit;

        tickMiningCenter = transform.position + Vector3.up * tickMiningHeight + transform.forward * -tickMiningBack;
        
        if (Physics.Raycast(tickMiningCenter, transform.forward, out hit, player.EquipMiningTool.Status.rnage))
        {
            if (hit.collider.CompareTag("Ore"))
            {
                hit.collider.GetComponent<Resource>().GetResource();

                if (oreCount >= player.EquipMiningTool.Status.maxOre)
                    TryStartAbleMaxIconCoroutine();
            }
        }
    }

    private void PickingEvent_Screw()
    {
        RaycastHit hit;
        Screw screw = player.EquipMiningTool as Screw;

        tickMiningCenter = transform.position + Vector3.up * tickMiningHeight + transform.forward * -tickMiningBack;

        if (Physics.Raycast(tickMiningCenter, transform.forward, out hit, player.EquipMiningTool.Status.rnage))
        {
            if (hit.collider.CompareTag("Ore"))
            {
                if (screw != null)
                {
                    screw.ActivateRotation();
                }

                hit.collider.GetComponent<Resource>().GetResource();

                if (oreCount >= player.EquipMiningTool.Status.maxOre)
                    TryStartAbleMaxIconCoroutine();
            }
        }
    }

    private void PickingEvent_Vehicle()
    {
        RaycastHit hit;
        Vehicle vehicle = player.EquipMiningTool as Vehicle;

        tickMiningCenter = transform.position + Vector3.up * tickMiningHeight + transform.forward * -tickMiningBack;

        if (Physics.BoxCast
            (
            tickMiningCenter,
            GetMiningBoxHalfExtents(),
            transform.forward,
            out hit,
            transform.rotation,
            player.EquipMiningTool.Status.rnage)
            )
        {
            if (hit.collider.CompareTag("Ore"))
            {
                if (vehicle != null)
                {
                    vehicle.ActivateRotation();
                }

                hit.collider.GetComponent<Resource>().GetResource();

                if (oreCount >= player.EquipMiningTool.Status.maxOre)
                    TryStartAbleMaxIconCoroutine();
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && !showTickMiningDebug)
        {
            return;
        }

        if (!Application.isPlaying && !showMiningRayDebug)
        {
            return;
        }

        float previewHeight = Application.isPlaying ? tickMiningHeight : miningRayHeight;
        float previewRange = Application.isPlaying ? GetMiningRange() : miningRayDistance;
        Vector3 center = Application.isPlaying ? tickMiningCenter : GetTickMiningCenter(previewHeight);
        Vector3 endCenter = Application.isPlaying ? debugCastEndCenter : center + transform.forward.normalized * previewRange;
        Vector3 boxHalfExtents = Application.isPlaying ? GetMiningBoxHalfExtents() : miningBoxHalfExtents;
        bool useBoxCast = Application.isPlaying && debugUsesBoxCast;
        Color gizmoColor = Application.isPlaying ? tickMiningIdleColor : defaultRayColor;
        if (Application.isPlaying)
        {
            gizmoColor = isHittingOre
                ? tickMiningOreColor
                : string.IsNullOrEmpty(hitTargetName) ? tickMiningIdleColor : tickMiningBlockedColor;
        }
        else
        {
            gizmoColor = defaultRayColor;
        }

        Gizmos.color = gizmoColor;

        if (useBoxCast)
        {
            DrawGizmoWireBox(center, transform.rotation, boxHalfExtents);
            DrawGizmoWireBox(endCenter, transform.rotation, boxHalfExtents);
            Gizmos.DrawLine(center, endCenter);
        }
        else
        {
            Gizmos.DrawWireSphere(center, previewRange);
            Gizmos.DrawLine(Application.isPlaying ? debugRayOrigin : center, Application.isPlaying ? debugRayEnd : center + transform.forward.normalized * previewRange);
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (!string.IsNullOrEmpty(hitTargetName))
        {
            Gizmos.DrawSphere(debugHitPoint, 0.05f);
        }

        int drawCount = Mathf.Min(tickMiningOreCount, tickMiningDebugOrePositions.Length);
        for (int i = 0; i < drawCount; ++i)
        {
            Vector3 orePosition = tickMiningDebugOrePositions[i];
            Gizmos.DrawLine(center, orePosition);
            Gizmos.DrawSphere(orePosition, 0.05f);
        }
    }

    private void DrawGizmoWireBox(Vector3 center, Quaternion rotation, Vector3 halfExtents)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = previousMatrix;
    }

    void OnTriggerStay(Collider other)
    {
    }

    private void OnTriggerExit(Collider other)
    {
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (Camera.main == null)
        {
            return new Vector3(input.x, 0f, input.y).normalized;
        }

        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * input.y + cameraRight * input.x;
        return moveDirection.normalized;
    }

    private void MovePlayer()
    {
        Vector2 input = JoyStick.Instance != null ? JoyStick.Instance.InputVector : Vector2.zero;
        bool isMoving = input.sqrMagnitude > 0.0001f;
        if (player.Animator != null)
        {
            player.Animator.SetBool(MoveHash, isMoving);
        }

        if (!isMoving)
        {
            return;
        }

        Vector3 moveDirection = GetMoveDirection(input);
        transform.parent.position += moveDirection * player.Status.moveSpeed * Time.deltaTime;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.parent.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, player.Status.rotationSpeed * Time.deltaTime);
    }

    private void TryStartAbleMaxIconCoroutine()
    {
        if (ableMaxIconCoroutine != null)
        {
            return;
        }

        ableMaxIconCoroutine = StartCoroutine(AbleMaxIconCoroutine());
    }

    private IEnumerator AbleMaxIconCoroutine()
    {
        maxIcon.gameObject.SetActive(true);
        Color color = maxIcon.color;
        color.a = 0;
        maxIcon.color = color;
        maxIcon.rectTransform.anchoredPosition = Vector2.zero;

        float ableTime = 1f;

        maxIcon.DOKill();
        maxIcon.DOFade(ableTime, ableTime);
        maxIcon.rectTransform.DOAnchorPosY(maxIcon.rectTransform.anchoredPosition.y + 100, ableTime);

        yield return new WaitForSeconds(ableTime);

        maxIcon.gameObject.SetActive(false);
        ableMaxIconCoroutine = null;
    }

    public int GetResourceCount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                return oreCount;
            case ResourceType.Handcuffs:
                return handcuffsCount;
            case ResourceType.Money:
                return moneyCount;
        }

        return 0;
    }

    void UpdateResourceStack(ResourceType type, int stack)
    {
        switch (type)
        {
            case ResourceType.Ore:
                for (int i = 0; i < oreStack.Count; ++i)
                    oreStack[i].gameObject.SetActive(i < stack);
                break;
            case ResourceType.Handcuffs:
                for (int i = 0; i < handcuffsStack.Count; ++i)
                    handcuffsStack[i].gameObject.SetActive(i < stack);
                break;
            case ResourceType.Money:
                for (int i = 0; i < moneyStack.Count; ++i)
                    moneyStack[i].gameObject.SetActive(i < stack);
                break;
        }
    }

    public void AddResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                UpdateResourceStack(type, ++oreCount);
                break;
            case ResourceType.Handcuffs:
                UpdateResourceStack(type, ++handcuffsCount);
                break;
            case ResourceType.Money:
                UpdateResourceStack(type, ++moneyCount);
                break;
        }
    }

    public void SubResrouce(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                --oreCount;
                break;
            case ResourceType.Handcuffs:
                --handcuffsCount;
                break;
            case ResourceType.Money:
                --moneyCount;
                break;
        }
    }

    public PortableResource GetPoppedResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                return oreStack[oreCount];
            case ResourceType.Handcuffs:
                return handcuffsStack[handcuffsCount];
            case ResourceType.Money:
                return moneyStack[moneyCount];
        }

        return null;
    }

    public PortableResource GetCurrentTopResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                return oreCount > 0 ? oreStack[oreCount - 1] : null;
            case ResourceType.Handcuffs:
                return handcuffsCount > 0 ? handcuffsStack[handcuffsCount - 1] : null;
            case ResourceType.Money:
                return moneyCount > 0 ? moneyStack[moneyCount - 1] : null;
        }

        return null;
    }

    public bool CanAddResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ore:
                return oreStack != null && oreCount < oreStack.Count;
            case ResourceType.Handcuffs:
                return handcuffsStack != null && handcuffsCount < handcuffsStack.Count;
            case ResourceType.Money:
                return moneyStack != null && moneyCount < moneyStack.Count;
        }

        return false;
    }

    public int HandcuffsCount => handcuffsCount;
    public int MoneyCount => moneyCount;
    public bool IsEnterMine => enterMine; 
    public bool IsEnterOre => enterOre;
}
