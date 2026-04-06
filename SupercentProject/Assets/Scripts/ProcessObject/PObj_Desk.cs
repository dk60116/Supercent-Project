using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PObj_Desk : ProcessObject
{
    private const int PresonerCompletionOutputCount = 8;
    private const float DefaultDeskResourceOperationInterval = 0.05f;

    private enum DeskInputSlotState
    {
        Empty,
        Incoming,
        Occupied,
        Outgoing,
    }

    [SerializeField]
    private float resourceOperationInterval = DefaultDeskResourceOperationInterval;
    [SerializeField]
    private float prisonerOutputInterval = 0.1f;
    [SerializeField]
    private float prisonerTransferDuration = 0.1f;
    [SerializeField]
    private float prisonerTransferJumpPower = 0.75f;
    [SerializeField]
    private int prisonerTransferJumpCount = 1;
    [SerializeField]
    private Vector3 prisonerTargetOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, ReadOnly]
    private int pendingPrisonerOutputRequestCount;
    [SerializeField, ReadOnly]
    private bool counterWorkerPrisonerOutputActive;
    [SerializeField, ReadOnly]
    private float prisonerOutputTickTime;

    private DeskInputSlotState[] inputSlotStates;

    protected new void Awake()
    {
        base.Awake();

        EnsureDeskInputSlotStateCache();
        RefreshOccupiedInputCount();
    }

    protected new void Update()
    {
        EnsureDeskInputSlotStateCache();

        PlayerController playerController = GameManager.Instance != null && GameManager.Instance.Player != null
            ? GameManager.Instance.Player.Controller : null;
        Presoner targetPresoner = GetCounterPresoner();

        bool canOutputToPlayer = enterOutput
            && outputCount > 0
            && playerController != null
            && playerController.CanAddResource(outputResourceType);
        bool canOutputToPresoner = CanQueuePrisonerOutput(targetPresoner);
        bool canAcceptInput = enterInput
            && playerController != null
            && playerController.GetResourceCount(inputResourceType) > 0
            && CanAddInputResource();

        if (!(canAcceptInput || canOutputToPresoner || canOutputToPlayer))
        {
            tickTIme = 0f;
            prisonerOutputTickTime = 0f;
            return;
        }

        if (canOutputToPresoner)
        {
            if (prisonerOutputTickTime == 0f || prisonerOutputTickTime >= prisonerOutputInterval)
            {
                GameManager.Instance.Player.PlaySound(popSound);
                RequestSubOutputResource(targetPresoner);
                prisonerOutputTickTime = 0f;
            }

            prisonerOutputTickTime += Time.deltaTime;
        }
        else
        {
            prisonerOutputTickTime = 0f;
        }

        if (!(canAcceptInput || canOutputToPlayer))
        {
            tickTIme = 0f;
            return;
        }

        if (tickTIme == 0f || tickTIme >= resourceOperationInterval)
        {
            if (canAcceptInput)
            {
                AddInputResource();
                PlaySound(pushSound);
            }

            if (canOutputToPlayer)
            {
                base.SubOutputResource();
            }

            tickTIme = 0f;
        }

        tickTIme += Time.deltaTime;
    }

    public override void SubOutputResource()
    {
        RequestSubOutputResource(GetCounterPresoner());
    }

    public void SetCounterWorkerPrisonerOutputActive(bool active)
    {
        counterWorkerPrisonerOutputActive = active;
    }

    public bool IsCounterInputEmptyForWorker => !HasBufferedDeskInput();

    protected override bool CanAddInputResource()
    {
        EnsureDeskInputSlotStateCache();
        return GetAvailableDeskInputCapacity() > 0;
    }

    protected override bool TryExecuteQueuedAddInputResource(PendingInputRequest pendingRequest)
    {
        EnsureDeskInputSlotStateCache();

        int targetIndex = GetNextStackInputSlotIndex();
        if (targetIndex < 0 || targetIndex >= inputSlotStates.Length)
        {
            return false;
        }

        PortableResource targetResource = GetStackResource(inputResourcesStack, targetIndex);
        if (targetResource == null || pendingRequest.SourceResource == null)
        {
            return true;
        }

        inputSlotStates[targetIndex] = DeskInputSlotState.Incoming;

        if (pendingRequest.UseJumpAnimation)
        {
            pendingRequest.SourceResource.PlayTransferAnimation_Jum
            (
                pendingRequest.SourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                pendingRequest.Duration,
                targetResource.gameObject,
                () =>
                {
                    EnsureDeskInputSlotStateCache();
                    if (targetIndex >= 0 && targetIndex < inputSlotStates.Length)
                    {
                        inputSlotStates[targetIndex] = DeskInputSlotState.Occupied;
                    }

                    RefreshOccupiedInputCount();
                },
                pendingRequest.JumpPower,
                pendingRequest.JumpCount
            );
        }
        else
        {
            pendingRequest.SourceResource.PlayTransferAnimation
            (
                pendingRequest.SourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                pendingRequest.Duration,
                targetResource.gameObject,
                () =>
                {
                    EnsureDeskInputSlotStateCache();
                    if (targetIndex >= 0 && targetIndex < inputSlotStates.Length)
                    {
                        inputSlotStates[targetIndex] = DeskInputSlotState.Occupied;
                    }

                    RefreshOccupiedInputCount();
                }
            );
        }

        return true;
    }

    private bool RequestSubOutputResource(Presoner targetPresoner)
    {
        if (!CanQueuePrisonerOutput(targetPresoner))
        {
            return false;
        }

        bool enqueued = EnqueueCustomResourceOperation(() =>
        {
            bool completed = TryExecuteQueuedPrisonerOutput(targetPresoner);
            if (completed)
            {
                pendingPrisonerOutputRequestCount = Mathf.Max(0, pendingPrisonerOutputRequestCount - 1);
            }

            return completed;
        });

        if (enqueued)
        {
            ++pendingPrisonerOutputRequestCount;
        }

        return enqueued;
    }

    private bool TryExecuteQueuedPrisonerOutput(Presoner targetPresoner)
    {
        EnsureDeskInputSlotStateCache();

        int sourceIndex = GetTopOccupiedInputSlotIndex();
        if (sourceIndex < 0 || sourceIndex >= inputSlotStates.Length)
        {
            return true;
        }

        if (targetPresoner == null || !targetPresoner.NeedResource(inputResourceType))
        {
            return true;
        }

        PortableResource sourceResource = GetStackResource(inputResourcesStack, sourceIndex);
        if (sourceResource == null)
        {
            return true;
        }

        inputSlotStates[sourceIndex] = DeskInputSlotState.Outgoing;
        RefreshOccupiedInputCount();

        sourceResource.PlayTransferAnimation_Jum
        (
            sourceResource.transform.position,
            targetPresoner.transform.position + prisonerTargetOffset,
            targetPresoner.transform.eulerAngles,
            prisonerTransferDuration,
            null,
            () =>
            {
                if (targetPresoner != null)
                {
                    bool consumedResource = targetPresoner.ReceiveResource(inputResourceType);
                    if (consumedResource && !targetPresoner.NeedResource(inputResourceType))
                    {
                        AddCompletionOutputResources();
                    }
                }

                EnsureDeskInputSlotStateCache();
                if (sourceIndex >= 0 && sourceIndex < inputSlotStates.Length)
                {
                    inputSlotStates[sourceIndex] = DeskInputSlotState.Empty;
                }

                RefreshOccupiedInputCount();
            },
            prisonerTransferJumpPower,
            prisonerTransferJumpCount
        );

        return true;
    }

    private Presoner GetCounterPresoner()
    {
        return GameManager.Instance.WaitingLine.GetCounterPresoner();
    }

    private bool HasOccupiedInputSlot()
    {
        return GetTopOccupiedInputSlotIndex() >= 0;
    }

    private bool CanQueuePrisonerOutput(Presoner targetPresoner)
    {
        return CanProcessPrisonerOutput()
            && targetPresoner != null
            && targetPresoner.NeedResource(inputResourceType)
            && GetAvailableDeskOccupiedInputCount() > 0;
    }

    private bool CanProcessPrisonerOutput()
    {
        return counterWorkerPrisonerOutputActive || enterInput || enterOutput;
    }

    private int GetNextStackInputSlotIndex()
    {
        EnsureDeskInputSlotStateCache();

        for (int i = 0; i < inputSlotStates.Length; ++i)
        {
            DeskInputSlotState slotState = inputSlotStates[i];

            if (slotState == DeskInputSlotState.Occupied || slotState == DeskInputSlotState.Incoming)
            {
                continue;
            }

            if (slotState == DeskInputSlotState.Empty)
            {
                return i;
            }

            // Do not stack a new item above a slot that is still leaving.
            return -1;
        }

        return -1;
    }

    private int GetTopOccupiedInputSlotIndex()
    {
        EnsureDeskInputSlotStateCache();

        for (int i = inputSlotStates.Length - 1; i >= 0; --i)
        {
            if (inputSlotStates[i] == DeskInputSlotState.Occupied)
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshOccupiedInputCount()
    {
        EnsureDeskInputSlotStateCache();

        int occupiedCount = 0;

        for (int i = 0; i < inputSlotStates.Length; ++i)
        {
            if (inputSlotStates[i] == DeskInputSlotState.Occupied)
            {
                ++occupiedCount;
            }
        }

        inputCount = occupiedCount;
    }

    private int GetAvailableDeskInputCapacity()
    {
        EnsureDeskInputSlotStateCache();

        int emptySlotCount = 0;

        for (int i = 0; i < inputSlotStates.Length; ++i)
        {
            if (inputSlotStates[i] == DeskInputSlotState.Empty)
            {
                ++emptySlotCount;
            }
        }

        return Mathf.Max(0, emptySlotCount - PendingAddInputRequestCount);
    }

    private int GetAvailableDeskOccupiedInputCount()
    {
        EnsureDeskInputSlotStateCache();

        int occupiedSlotCount = 0;

        for (int i = 0; i < inputSlotStates.Length; ++i)
        {
            if (inputSlotStates[i] == DeskInputSlotState.Occupied)
            {
                ++occupiedSlotCount;
            }
        }

        return Mathf.Max(0, occupiedSlotCount - pendingPrisonerOutputRequestCount);
    }

    private bool HasBufferedDeskInput()
    {
        EnsureDeskInputSlotStateCache();

        if (PendingAddInputRequestCount > 0)
        {
            return true;
        }

        for (int i = 0; i < inputSlotStates.Length; ++i)
        {
            DeskInputSlotState slotState = inputSlotStates[i];
            if (slotState == DeskInputSlotState.Incoming || slotState == DeskInputSlotState.Occupied)
            {
                return true;
            }
        }

        return false;
    }

    private void AddCompletionOutputResources()
    {
        int outputStackCount = GetStackCount(outputResourcesStack);
        int addCount = Mathf.Min(PresonerCompletionOutputCount, Mathf.Max(0, outputStackCount - outputCount));

        for (int i = 0; i < addCount; ++i)
        {
            AddOutputResource();
        }
    }

    private void EnsureDeskInputSlotStateCache()
    {
        int inputStackCount = GetStackCount(inputResourcesStack);
        if (inputSlotStates != null && inputSlotStates.Length == inputStackCount)
        {
            return;
        }

        DeskInputSlotState[] previousStates = inputSlotStates;
        inputSlotStates = new DeskInputSlotState[inputStackCount];

        if (previousStates != null)
        {
            int copyCount = Mathf.Min(previousStates.Length, inputSlotStates.Length);
            for (int i = 0; i < copyCount; ++i)
            {
                inputSlotStates[i] = previousStates[i];
            }
        }
        else
        {
            for (int i = 0; i < inputSlotStates.Length; ++i)
            {
                PortableResource portableResource = GetStackResource(inputResourcesStack, i);
                inputSlotStates[i] = portableResource != null && portableResource.gameObject.activeSelf
                    ? DeskInputSlotState.Occupied
                    : DeskInputSlotState.Empty;
            }
        }
    }
}
