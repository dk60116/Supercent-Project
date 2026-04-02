using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private static readonly int MoveHash = Animator.StringToHash("bMove");
    private static readonly int PickingHash = Animator.StringToHash("tPicking");
    private static readonly int PickStateHash = Animator.StringToHash("Base Layer.Pick");
    private static readonly int PickRunStateHash = Animator.StringToHash("Base Layer.PickRun");

    [Header("Protable Resource Stack")]
    [SerializeField]
    List<PortableResource> oreStack, moneyStack;

    int maxOre, maxMoney;
    int oreCount, moneyCount;

    [Header("Mining Box Debug")]
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

    private Player player;
    private bool pickTriggerPending;
    private bool wasPickingAnimationPlaying;

    [SerializeField, ReadOnly]
    private float tickTime;
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
    private string lockedOreName;

    private Resource lockedOre;

    private void Awake()
    {
        player = GetComponent<Player>();

        for (int i = 0; i < oreStack.Count; ++i)
            oreStack[i].gameObject.SetActive(false);
        for (int i = 0; i < moneyStack.Count; ++i)
            moneyStack[i].gameObject.SetActive(false);
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        UpdatePickingState();
        MovePlayer();

        tickTime += Time.deltaTime;

        Vector3 rayOrigin = GetMiningRayOrigin();
        Vector3 rayDirection = transform.forward;
        Quaternion boxOrientation = transform.rotation;
        RaycastHit hit;

        bool hasHit = Physics.BoxCast(rayOrigin, miningBoxHalfExtents, rayDirection, out hit, boxOrientation, miningRayDistance);
        UpdateMiningRayDebug(rayOrigin, rayDirection, hasHit, hit);

        if (hasHit)
        {
            if (hit.collider.CompareTag("Ore"))
            {
                Resource hitOre = hit.collider.GetComponent<Resource>();

                if (hitOre != null && CanStartPick(hitOre))
                {
                    StartPick(hitOre);
                }
            }
        }
    }

    private void StartPick(Resource targetOre)
    {
        lockedOre = targetOre;
        lockedOreName = targetOre.name;
        pickTriggerPending = true;
        if (player.Animator != null)
        {
            player.Animator.SetTrigger(PickingHash);
        }
    }

    private bool CanStartPick(Resource hitOre)
    {
        if (hitOre == null)
        {
            return false;
        }

        return lockedOre == null && !pickTriggerPending && !wasPickingAnimationPlaying;
    }

    private void LateUpdate()
    {
    }

    private void OnDrawGizmosSelected()
    {
        if (!showMiningRayDebug)
        {
            return;
        }

        Vector3 rayOrigin = Application.isPlaying ? debugRayOrigin : GetMiningRayOrigin();
        Vector3 rayEnd = Application.isPlaying ? debugRayEnd : rayOrigin + transform.forward * miningRayDistance;
        Color rayColor = Application.isPlaying ? GetDebugRayColor() : defaultRayColor;

        Gizmos.color = rayColor;
        Gizmos.DrawLine(rayOrigin, rayEnd);
        DrawDebugBox(rayOrigin, rayColor);
        DrawDebugBox(rayEnd, rayColor);

        if (Application.isPlaying && !string.IsNullOrEmpty(hitTargetName))
        {
            Gizmos.DrawSphere(debugHitPoint, 0.05f);
        }
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
        transform.position += moveDirection * player.Status.moveSpeed * Time.deltaTime;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, player.Status.rotationSpeed * Time.deltaTime);
    }

    private void UpdatePickingState()
    {
        bool isPickingAnimationPlaying = IsPickingAnimationPlaying();

        if (isPickingAnimationPlaying && !wasPickingAnimationPlaying)
        {
            pickTriggerPending = false;
        }

        if (!isPickingAnimationPlaying && wasPickingAnimationPlaying)
        {
            CompletePick();
        }

        wasPickingAnimationPlaying = isPickingAnimationPlaying;
    }

    private bool IsPickingAnimationPlaying()
    {
        if (player.Animator == null)
        {
            return false;
        }

        AnimatorStateInfo currentState = player.Animator.GetCurrentAnimatorStateInfo(0);
        if (IsPickState(currentState))
        {
            return true;
        }

        if (player.Animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = player.Animator.GetNextAnimatorStateInfo(0);
            return IsPickState(nextState);
        }

        return false;
    }

    private static bool IsPickState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.fullPathHash == PickStateHash || stateInfo.fullPathHash == PickRunStateHash;
    }

    private void CompletePick()
    {
        if (lockedOre != null && lockedOre.gameObject.activeInHierarchy)
        {
            lockedOre.GetResource();
        }

        lockedOre = null;
        lockedOreName = string.Empty;
        pickTriggerPending = false;
    }

    private Vector3 GetMiningRayOrigin()
    {
        return transform.position + Vector3.up * miningRayHeight;
    }

    private void UpdateMiningRayDebug(Vector3 rayOrigin, Vector3 rayDirection, bool hasHit, RaycastHit hit)
    {
        debugRayOrigin = rayOrigin;
        debugRayEnd = rayOrigin + rayDirection * (hasHit ? hit.distance : miningRayDistance);
        debugHitPoint = hasHit ? hit.point : debugRayEnd;
        hitTargetName = hasHit ? hit.collider.name : string.Empty;
        isHittingOre = hasHit && hit.collider.CompareTag("Ore");

        if (!showMiningRayDebug)
        {
            return;
        }

        Debug.DrawLine(rayOrigin, debugRayEnd, GetDebugRayColor(), 0f, false);
    }

    private Color GetDebugRayColor()
    {
        if (isHittingOre)
        {
            return oreHitRayColor;
        }

        if (!string.IsNullOrEmpty(hitTargetName))
        {
            return blockedRayColor;
        }

        return defaultRayColor;
    }

    private void DrawDebugBox(Vector3 center, Color color)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = color;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, miningBoxHalfExtents * 2f);
        Gizmos.matrix = previousMatrix;
    }

    void UpdateOreStack(int stack)
    {
        oreStack[stack - 1].gameObject.SetActive(true);
    }

    public void AddOre()
    {
        UpdateOreStack(++oreCount);
    }

    public void SubOre()
    {
        UpdateOreStack(--oreCount);
    }

    int OreCount => oreCount;
    int MoneyCount => moneyCount;
}
