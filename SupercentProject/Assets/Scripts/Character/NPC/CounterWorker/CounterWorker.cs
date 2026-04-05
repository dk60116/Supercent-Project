using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CounterWorker : Character
{
    private static readonly int MoveHash = Animator.StringToHash("bMove");
    private static readonly int CarryHash = Animator.StringToHash("fCarry");
    private const string DefaultHandcuffsHolderName = "Handcuffs";
    private const float ArrivalDistance = 0.05f;
    private const float DefaultTransferInterval = 0.05f;
    private const float DefaultTransferDuration = 0.05f;

    private enum CounterWorkerState
    {
        Idle,
        MovingToFactory,
        CollectingHandcuffs,
        MovingToCounterA,
        SupplyingCounter,
        MovingToCounterB,
        WaitingForCounter,
    }

    [SerializeField]
    private Transform factoryTarget, counterTargetA, counterTargetB;
    [SerializeField]
    private PObj_Factory factory;
    [SerializeField]
    private PObj_Desk counterDesk;
    [SerializeField]
    private float factoryPickupInterval = DefaultTransferInterval;
    [SerializeField]
    private float counterDeliveryInterval = DefaultTransferInterval;
    [SerializeField]
    private float transferDuration = DefaultTransferDuration;

    [SerializeField]
    private List<PortableResource> handcuffsStack;
    [SerializeField]
    private GameObject handcuffsHolder;
    [SerializeField, ReadOnly]
    private int maxHandcuffs;
    [SerializeField, ReadOnly]
    private int handcuffsCount;
    [SerializeField, ReadOnly]
    private CounterWorkerState currentState;
    [SerializeField, ReadOnly]
    private bool workLoopRunning;

    private Coroutine workCoroutine;

    protected new void Awake()
    {
        base.Awake();

        ResolveTargets();
        DeactivateStack(handcuffsStack);
        ClampResourceCountsToStackCapacity();
        UpdateHandcuffsStack(handcuffsCount);
        UpdateCarryAnimation();
    }

    private void OnEnable()
    {
        ResolveTargets();
        StartWork();
    }

    private void OnDisable()
    {
        StopWork();
    }

    public void AutoFillResourceStacks()
    {
        if (handcuffsHolder == null)
        {
            Transform handcuffsRoot = FindChildByName(transform, DefaultHandcuffsHolderName);
            handcuffsHolder = handcuffsRoot != null ? handcuffsRoot.gameObject : null;
        }

        FillStackFromHolder(handcuffsHolder, ref handcuffsStack);
        ClampResourceCountsToStackCapacity();
        UpdateHandcuffsStack(handcuffsCount);
        UpdateCarryAnimation();
        ResolveTargets();
    }

    public void StartWork()
    {
        if (!isActiveAndEnabled || workCoroutine != null)
        {
            return;
        }

        workCoroutine = StartCoroutine(WorkRoutine());
    }

    public void StopWork()
    {
        if (workCoroutine != null)
        {
            StopCoroutine(workCoroutine);
            workCoroutine = null;
        }

        SetCounterDeskActive(false);
        SetMoveAnimation(false);
        UpdateCarryAnimation();
        workLoopRunning = false;
        currentState = CounterWorkerState.Idle;
    }

    public bool CanAddHandcuffs()
    {
        return handcuffsCount < maxHandcuffs;
    }

    public void AddHandcuffs()
    {
        handcuffsCount = Mathf.Clamp(handcuffsCount + 1, 0, maxHandcuffs);
        UpdateHandcuffsStack(handcuffsCount);
        UpdateCarryAnimation();
    }

    public void SubHandcuffs()
    {
        handcuffsCount = Mathf.Clamp(handcuffsCount - 1, 0, maxHandcuffs);
        UpdateHandcuffsStack(handcuffsCount);
        UpdateCarryAnimation();
    }

    public PortableResource GetPoppedHandcuffs()
    {
        return GetPortableResource(handcuffsCount);
    }

    public PortableResource GetCurrentTopHandcuffs()
    {
        return GetPortableResource(handcuffsCount - 1);
    }

    public bool WorkLoopRunning => workLoopRunning;

    private IEnumerator WorkRoutine()
    {
        workLoopRunning = true;

        while (isActiveAndEnabled)
        {
            ResolveTargets();

            if (!HasValidWorkTargets())
            {
                currentState = CounterWorkerState.Idle;
                SetMoveAnimation(false);
                yield return null;
                continue;
            }

            SetCounterDeskActive(false);

            yield return MoveToTarget(factoryTarget, CounterWorkerState.MovingToFactory, false);
            yield return CollectHandcuffsFromFactory();
            yield return MoveToTarget(counterTargetA, CounterWorkerState.MovingToCounterA, true);
            yield return SupplyCounter();
            yield return MoveToTarget(counterTargetB, CounterWorkerState.MovingToCounterB, true);
            yield return WaitForCounterToEmpty();
        }

        workLoopRunning = false;
        currentState = CounterWorkerState.Idle;
    }

    private IEnumerator MoveToTarget(Transform target, CounterWorkerState moveState, bool alignRotationOnArrival)
    {
        currentState = moveState;
        SetMoveAnimation(true);

        while (isActiveAndEnabled && target != null)
        {
            if (MoveTowardsPosition(target.position, ArrivalDistance))
            {
                transform.position = target.position;
                if (alignRotationOnArrival)
                {
                    transform.rotation = GetPlanarRotation(target.rotation);
                }

                break;
            }

            yield return null;
        }

        SetMoveAnimation(false);
    }

    private IEnumerator CollectHandcuffsFromFactory()
    {
        currentState = CounterWorkerState.CollectingHandcuffs;
        SetMoveAnimation(false);

        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, factoryPickupInterval));

        while (isActiveAndEnabled && CanAddHandcuffs())
        {
            ResolveTargets();

            PortableResource targetResource = GetPoppedHandcuffs();
            bool queuedTransfer = factory != null
                && targetResource != null
                && factory.TryTransferOutputToExternal(targetResource, transferDuration);

            if (queuedTransfer)
            {
                AddHandcuffs();
            }

            yield return wait;
        }
    }

    private IEnumerator SupplyCounter()
    {
        currentState = CounterWorkerState.SupplyingCounter;
        SetMoveAnimation(false);
        SetCounterDeskActive(false);

        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, counterDeliveryInterval));

        while (isActiveAndEnabled && handcuffsCount > 0)
        {
            ResolveTargets();

            PortableResource sourceResource = GetCurrentTopHandcuffs();
            bool queuedTransfer = counterDesk != null
                && sourceResource != null
                && counterDesk.TryAddExternalInputResource(sourceResource, transferDuration, false);

            if (queuedTransfer)
            {
                SubHandcuffs();
            }

            yield return wait;
        }
    }

    private IEnumerator WaitForCounterToEmpty()
    {
        currentState = CounterWorkerState.WaitingForCounter;
        SetMoveAnimation(false);
        SetCounterDeskActive(true);

        while (isActiveAndEnabled)
        {
            ResolveTargets();

            if (counterDesk == null || counterDesk.IsCounterInputEmptyForWorker)
            {
                break;
            }

            yield return null;
        }

        SetCounterDeskActive(false);
    }

    private void ResolveTargets()
    {
        if (factory == null && factoryTarget != null)
        {
            factory = factoryTarget.GetComponentInParent<PObj_Factory>();
        }

        if (counterDesk == null && counterTargetA != null)
        {
            counterDesk = counterTargetA.GetComponentInParent<PObj_Desk>();
        }

        if (counterDesk == null && counterTargetB != null)
        {
            counterDesk = counterTargetB.GetComponentInParent<PObj_Desk>();
        }

        if (factoryTarget == null && factory != null)
        {
            factoryTarget = factory.transform;
        }

        if (counterTargetA == null && counterDesk != null)
        {
            counterTargetA = counterDesk.transform;
        }

        if (counterTargetB == null && counterDesk != null)
        {
            counterTargetB = counterDesk.transform;
        }
    }

    private bool HasValidWorkTargets()
    {
        return factoryTarget != null
            && counterTargetA != null
            && counterTargetB != null
            && factory != null
            && counterDesk != null
            && maxHandcuffs > 0;
    }

    private void SetCounterDeskActive(bool active)
    {
        if (counterDesk != null)
        {
            counterDesk.SetCounterWorkerPrisonerOutputActive(active);
        }
    }

    private void SetMoveAnimation(bool isMoving)
    {
        if (Animator != null)
        {
            Animator.SetBool(MoveHash, isMoving);
        }
    }

    private void UpdateCarryAnimation()
    {
        if (Animator != null)
        {
            Animator.SetFloat(CarryHash, handcuffsCount > 0 ? 1f : 0f);
        }
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

    private void DeactivateStack(List<PortableResource> stack)
    {
        if (stack == null)
        {
            return;
        }

        for (int i = 0; i < stack.Count; ++i)
        {
            PortableResource portableResource = stack[i];
            if (portableResource != null)
            {
                portableResource.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateHandcuffsStack(int stackCount)
    {
        if (handcuffsStack == null)
        {
            return;
        }

        int clampedCount = Mathf.Clamp(stackCount, 0, handcuffsStack.Count);
        for (int i = 0; i < handcuffsStack.Count; ++i)
        {
            PortableResource portableResource = handcuffsStack[i];
            if (portableResource != null)
            {
                portableResource.gameObject.SetActive(i < clampedCount);
            }
        }
    }

    private void ClampResourceCountsToStackCapacity()
    {
        maxHandcuffs = handcuffsStack != null ? handcuffsStack.Count : 0;
        handcuffsCount = Mathf.Clamp(handcuffsCount, 0, maxHandcuffs);
    }

    private PortableResource GetPortableResource(int index)
    {
        if (handcuffsStack == null || index < 0 || index >= handcuffsStack.Count)
        {
            return null;
        }

        return handcuffsStack[index];
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; ++i)
        {
            Transform foundChild = FindChildByName(parent.GetChild(i), childName);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }
}
