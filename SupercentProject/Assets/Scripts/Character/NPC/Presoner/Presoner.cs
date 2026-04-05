using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static WaitingLine;

public class Presoner : Character
{
    private enum JailMoveResult
    {
        Started,
        Waiting,
        Failed,
    }

    private const int MaxBodyOverlapResolveIterations = 4;
    private const float BodyOverlapResolvePadding = 0.01f;
    private const float BubbleWorldOffsetY = 1.5f;

    [SerializeField]
    private Collider bodyCollider;
    [SerializeField]
    private Rigidbody rig;

    [SerializeField]
    SkinnedMeshRenderer clothMesh;

    private const float ArrivalDistance = 0.05f;

    [SerializeField]
    GameObject waitirngAvata, presonerAvatar;
    [SerializeField]
    private float jailAreaMoveTimeout = 1.5f;

    [SerializeField, ReadOnly]
    private bool isTarget;
    [SerializeField, ReadOnly]
    private int waitIndex = -1;

    [SerializeField, ReadOnly]
    private int needHanCuffsCount;

    [SerializeField, ReadOnly]
    private bool isArrival;
    [SerializeField, ReadOnly]
    private bool isArrival_Counter;
    [SerializeField, ReadOnly]
    private bool isLeaving;
    [SerializeField, ReadOnly]
    private bool isCompleted;

    [SerializeField, ReadOnly]
    private NeedBubble bubble;

    private RectTransform bubbleRect;
    private RectTransform canvasRect;
    private Camera targetCamera;
    private PresonerSpawner ownerSpawner;
    private Jail targetJail;
    private Transform moveTarget;
    private Transform queuedMoveTarget;
    private Transform jailEntryPointTarget;
    private Vector3 destinationPosition;
    private Quaternion destinationRotation;
    private bool isMovingToDestination;
    private bool moveToJailAfterExit;
    private bool isMovingToJail;
    private bool queuedMoveRequiresJailQueue;
    private float moveAttemptTimeout = -1f;
    private float moveAttemptElapsed;
    private readonly Collider[] overlapResults = new Collider[16];

    protected new void Awake()
    {
        base.Awake();

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider>();
        }

        if (rig == null)
        {
            rig = GetComponent<Rigidbody>();
        }

        ChangeMode(false);
    }

    private void OnEnable()
    {
        EnsureBubble();
        RefreshNeedBubble();
        targetCamera = Camera.main;
    }

    private void OnDisable()
    {
        ReleaseBubble();
    }

    void Update()
    {
        UpdateMovement();

        isArrival = isTarget && !isLeaving && !isMovingToDestination;
        isArrival_Counter = isArrival && waitIndex == 0;

        Animator.SetBool("bMove", isMovingToDestination);

        if (isLeaving && !isMovingToDestination)
        {
            if (queuedMoveTarget != null)
            {
                Transform nextTarget = queuedMoveTarget;
                queuedMoveTarget = null;
                queuedMoveRequiresJailQueue = false;
                SetMoveTarget(nextTarget);
            }
            else if (moveToJailAfterExit)
            {
                if (jailEntryPointTarget != null)
                {
                    if (!TryMoveToJailEntryPoint())
                    {
                        return;
                    }

                    Transform nextTarget = jailEntryPointTarget;
                    jailEntryPointTarget = null;
                    SetMoveTarget(nextTarget);
                    return;
                }

                JailMoveResult jailMoveResult = TryMoveToJail();

                if (jailMoveResult == JailMoveResult.Started)
                {
                    moveToJailAfterExit = false;
                }
                else if (jailMoveResult == JailMoveResult.Waiting)
                {
                    return;
                }
                else
                {
                    CancelJailApproach();
                    isLeaving = false;
                }
            }
            else
            {
                if (isMovingToJail && targetJail != null)
                {
                    targetJail.RegisterPresoner(this);
                    isMovingToJail = false;
                }

                isLeaving = false;
            }
        }
    }

    void LateUpdate()
    {
        UpdateBubblePosition();
    }

    private void OnDestroy()
    {
        ReleaseBubble();
    }

    private void UpdateBubblePosition()
    {
        if (bubble == null || bubbleRect == null || canvasRect == null)
        {
            return;
        }

        if (!ShouldShowBubble())
        {
            if (bubble.gameObject.activeSelf)
            {
                bubble.gameObject.SetActive(false);
            }

            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                return;
            }
        }

        Canvas mainCanvas = UIManager.Instance != null ? UIManager.Instance.MainCanvas : null;
        Camera uiCamera = GetBubbleUICamera(mainCanvas);
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(transform.position + Vector3.up * BubbleWorldOffsetY);

        if (screenPoint.z <= 0f || !IsScreenPointVisible(screenPoint))
        {
            if (bubble.gameObject.activeSelf)
            {
                bubble.gameObject.SetActive(false);
            }

            return;
        }

        if (!bubble.gameObject.activeSelf)
        {
            bubble.gameObject.SetActive(true);
        }

        if (mainCanvas == null || mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            bubbleRect.position = screenPoint;
        }
        else if (canvasRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            bubbleRect.anchoredPosition = localPoint;
        }
    }

    public void ChangeMode(bool presoner)
    {
        if (!presoner)
        {
            body = waitirngAvata.transform;
            animator = waitirngAvata.GetComponent<Animator>();
            SetBodyColliderActive(false);
        }
        else
        {
            body = presonerAvatar.transform;
            animator = presonerAvatar.GetComponent<Animator>();
        }

            waitirngAvata.SetActive(!presoner);
        presonerAvatar.SetActive(presoner);
    }

    public void StartWaiting()
    {
        if (isTarget || isLeaving || isCompleted)
            return;

        FindWaitTargetInfo targetInfo = GameManager.Instance.WaitingLine.FindEmptyPoint();

        if (targetInfo.find)
        {
            AssignWaitPoint(targetInfo.transform, targetInfo.index);
            GameManager.Instance.WaitingLine.SetHasPerson(targetInfo.index, true);
        }
    }

    public bool NeedResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Handcuffs:
                return needHanCuffsCount > 0;
        }

        return false;
    }

    public bool ReceiveResource(ResourceType type, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        switch (type)
        {
            case ResourceType.Handcuffs:
                if (needHanCuffsCount <= 0)
                {
                    return false;
                }

                needHanCuffsCount = Mathf.Max(0, needHanCuffsCount - amount);
                RefreshNeedBubble();

                if (needHanCuffsCount == 0)
                {
                    GameManager.Instance.WaitingLine.ReleasePresoner(this);
                }

                return true;
        }

        return false;
    }

    public void AssignWaitPoint(Transform targetPoint, int index)
    {
        if (targetPoint == null)
        {
            return;
        }

        queuedMoveTarget = null;
        jailEntryPointTarget = null;
        SetMoveTarget(targetPoint);
        waitIndex = index;
        isTarget = true;
        isLeaving = false;
        isCompleted = false;
        isArrival = false;
        isArrival_Counter = false;
    }

    public void MoveToEndPoint(Transform targetPoint, Transform nextTargetPoint = null, Transform jailEntryTargetPoint = null)
    {
        if (targetPoint == null)
        {
            if (nextTargetPoint == null)
            {
                return;
            }

            targetPoint = nextTargetPoint;
            nextTargetPoint = null;
        }

        queuedMoveTarget = nextTargetPoint;
        jailEntryPointTarget = jailEntryTargetPoint;
        moveToJailAfterExit = true;
        queuedMoveRequiresJailQueue = nextTargetPoint != null;
        MarkJailApproachStarted();
        isMovingToJail = false;
        SetMoveTarget(targetPoint);
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = true;
        isCompleted = true;
    }

    private void RefreshNeedBubble()
    {
        if (bubble == null)
        {
            return;
        }

        bubble.SetData(null, needHanCuffsCount);
        bubble.gameObject.SetActive(ShouldShowBubble());
    }

    private bool ShouldShowBubble()
    {
        return needHanCuffsCount > 0 && isArrival_Counter && !isLeaving && !isCompleted;
    }

    public void PrepareForSpawn(PresonerSpawner spawner, Transform spawnPoint)
    {
        CancelJailApproach();
        ownerSpawner = spawner;
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = false;
        isCompleted = false;
        needHanCuffsCount = Random.Range(1, 6);
        clothMesh.material.color = Random.ColorHSV();
        targetJail = null;
        moveToJailAfterExit = false;
        isMovingToJail = false;
        queuedMoveRequiresJailQueue = false;
        jailEntryPointTarget = null;
        SetBodyColliderActive(false);

        ChangeMode(false);

        if (spawnPoint != null)
        {
            MoveTo(spawnPoint.position, spawnPoint.rotation);
        }

        EnsureBubble();
        RefreshNeedBubble();
    }

    public void ResetForPool()
    {
        CancelJailApproach();
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = false;
        isCompleted = false;
        ownerSpawner = null;
        targetJail = null;
        moveTarget = null;
        queuedMoveTarget = null;
        jailEntryPointTarget = null;
        isMovingToDestination = false;
        moveToJailAfterExit = false;
        isMovingToJail = false;
        queuedMoveRequiresJailQueue = false;
        SetBodyColliderActive(false);
    }

    private void EnsureBubble()
    {
        if (bubble != null)
        {
            return;
        }

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            return;
        }

        bubble = uiManager.GetBubble();
        if (bubble == null)
        {
            return;
        }

        bubbleRect = bubble.transform as RectTransform;
        canvasRect = uiManager.MainCanvas != null ? uiManager.MainCanvas.transform as RectTransform : null;
    }

    private void ReleaseBubble()
    {
        if (bubble != null)
        {
            bubble.Release();
            bubble = null;
        }

        bubbleRect = null;
        canvasRect = null;
    }

    private Camera GetBubbleUICamera(Canvas mainCanvas)
    {
        if (mainCanvas == null || mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return mainCanvas.worldCamera != null ? mainCanvas.worldCamera : targetCamera;
    }

    private bool IsScreenPointVisible(Vector3 screenPoint)
    {
        return screenPoint.x >= 0f
            && screenPoint.x <= Screen.width
            && screenPoint.y >= 0f
            && screenPoint.y <= Screen.height;
    }

    private void MoveTo(Vector3 position, Quaternion rotation)
    {
        Quaternion uprightRotation = GetPlanarRotation(rotation);
        transform.SetPositionAndRotation(position, uprightRotation);
        destinationPosition = position;
        destinationRotation = uprightRotation;
        isMovingToDestination = false;
        ResetMoveAttemptTimeout();
    }

    private void SetMoveTarget(Transform targetPoint)
    {
        SetMoveTarget(targetPoint, -1f);
    }

    private void SetMoveTarget(Transform targetPoint, float timeout)
    {
        moveTarget = targetPoint;
        destinationPosition = targetPoint.position;
        destinationRotation = GetPlanarRotation(targetPoint.rotation);
        isMovingToDestination = true;
        moveAttemptTimeout = timeout;
        moveAttemptElapsed = 0f;
    }

    private void UpdateMovement()
    {
        if (!isMovingToDestination)
        {
            return;
        }

        if (moveTarget != null)
        {
            destinationPosition = moveTarget.position;
            destinationRotation = GetPlanarRotation(moveTarget.rotation);
        }

        if (moveAttemptTimeout > 0f)
        {
            moveAttemptElapsed += Time.deltaTime;
            if (moveAttemptElapsed >= moveAttemptTimeout)
            {
                isMovingToDestination = false;
                moveTarget = null;
                ResetMoveAttemptTimeout();
                return;
            }
        }

        if (MoveTowardsPosition(destinationPosition, ArrivalDistance))
        {
            transform.position = destinationPosition;
            isMovingToDestination = false;
            moveTarget = null;
            ResetMoveAttemptTimeout();
        }
    }

    private bool CanAdvanceToQueuedMoveTarget()
    {
        if (targetJail == null)
        {
            targetJail = FindObjectOfType<Jail>();
        }

        if (targetJail == null)
        {
            return true;
        }

        return targetJail.CanPresonerAdvanceToExitPoint(this);
    }

    private bool TryMoveToJailEntryPoint()
    {
        if (targetJail == null)
        {
            targetJail = FindObjectOfType<Jail>();
        }

        if (targetJail == null)
        {
            return false;
        }

        return targetJail.TryReservePresonerEntry(this);
    }

    private JailMoveResult TryMoveToJail()
    {
        if (targetJail == null)
        {
            targetJail = FindObjectOfType<Jail>();
        }

        if (targetJail == null)
        {
            return JailMoveResult.Failed;
        }

        if (!targetJail.HasReservedPresonerEntry(this)
            && !targetJail.TryReservePresonerEntry(this))
        {
            return JailMoveResult.Waiting;
        }

        SetBodyColliderActive(true);
        SetMoveTarget(targetJail.transform);
        isMovingToJail = true;
        return JailMoveResult.Started;
    }

    public void MoveToJailArea(Transform targetPoint)
    {
        if (targetPoint == null)
        {
            return;
        }

        SetMoveTarget(targetPoint, jailAreaMoveTimeout);
    }

    private void ResetMoveAttemptTimeout()
    {
        moveAttemptTimeout = -1f;
        moveAttemptElapsed = 0f;
    }

    private void CancelJailApproach()
    {
        if (targetJail != null)
        {
            targetJail.NotifyPresonerApproachCancelled(this);
        }

        isMovingToJail = false;
    }

    private void MarkJailApproachStarted()
    {
        if (targetJail == null)
        {
            targetJail = FindObjectOfType<Jail>();
        }

        if (targetJail != null)
        {
            targetJail.NotifyPresonerApproaching(this);
        }
    }

    private void SetBodyColliderActive(bool active)
    {
        if (bodyCollider != null)
        {
            bodyCollider.enabled = active;

            if (active)
            {
                ResolveBodyOverlap();

                if (rig != null)
                {
                    rig.WakeUp();
                }
            }
        }
    }

    private void ResolveBodyOverlap()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
        {
            return;
        }

        Physics.SyncTransforms();

        for (int iteration = 0; iteration < MaxBodyOverlapResolveIterations; ++iteration)
        {
            Bounds bounds = bodyCollider.bounds;
            int overlapCount = Physics.OverlapSphereNonAlloc(
                bounds.center,
                bounds.extents.magnitude,
                overlapResults,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 totalSeparation = Vector3.zero;
            bool hasOverlap = false;

            for (int i = 0; i < overlapCount; ++i)
            {
                Collider otherCollider = overlapResults[i];
                overlapResults[i] = null;

                if (otherCollider == null
                    || otherCollider == bodyCollider
                    || otherCollider.transform == transform
                    || otherCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!Physics.ComputePenetration(
                    bodyCollider,
                    transform.position,
                    transform.rotation,
                    otherCollider,
                    otherCollider.transform.position,
                    otherCollider.transform.rotation,
                    out Vector3 separationDirection,
                    out float separationDistance))
                {
                    continue;
                }

                hasOverlap = true;
                totalSeparation += separationDirection * (separationDistance + BodyOverlapResolvePadding);
            }

            if (!hasOverlap || totalSeparation.sqrMagnitude <= 0.0001f)
            {
                break;
            }

            Vector3 resolvedPosition = transform.position + totalSeparation;

            if (rig != null && !rig.isKinematic)
            {
                rig.position = resolvedPosition;
            }
            else
            {
                transform.position = resolvedPosition;
            }

            Physics.SyncTransforms();
        }
    }

    public bool IsArrivalCounter => isArrival_Counter;
    public int NeedHanCuffsCount => needHanCuffsCount;
    public int WaitIndex => waitIndex;
    public bool IsLeaving => isLeaving;
    public bool IsCompleted => isCompleted;
    public bool IsMovingToJail => isMovingToJail;
    public bool IsWaitingForJailEntry => isLeaving
        && !isMovingToDestination
        && moveToJailAfterExit
        && jailEntryPointTarget != null;
}
