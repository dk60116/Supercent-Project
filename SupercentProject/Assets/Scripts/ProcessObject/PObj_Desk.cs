using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PObj_Desk : ProcessObject
{
    private const int PresonerCompletionOutputCount = 8;

    private enum DeskInputSlotState
    {
        Empty,
        Incoming,
        Occupied,
        Outgoing,
    }

    [SerializeField]
    private float prisonerOutputInterval = 0.1f;
    [SerializeField]
    private float prisonerTransferDuration = 0.1f;
    [SerializeField]
    private Vector3 prisonerTargetOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, ReadOnly]
    private int pendingPrisonerOutputRequestCount;

    private DeskInputSlotState[] inputSlotStates;

    protected new void Awake()
    {
        base.Awake();

        inputSlotStates = new DeskInputSlotState[inputResourcesStack.Count];
        inputCount = 0;
    }

    protected new void Update()
    {
        PlayerController playerController = GameManager.Instance != null && GameManager.Instance.Player != null
            ? GameManager.Instance.Player.Controller
            : null;
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
            return;
        }

        if (tickTIme == 0f || tickTIme >= prisonerOutputInterval)
        {
            // Consume the already-buffered slot first so we do not animate a newly-added
            // input slot and immediately steal that same index before it is visible.
            if (canOutputToPresoner)
            {
                RequestSubOutputResource(targetPresoner);
            }

            bool canAcceptInputAfterOutput = enterInput
                && playerController != null
                && playerController.GetResourceCount(inputResourceType) > 0
                && CanAddInputResource();

            if (canAcceptInputAfterOutput)
            {
                AddInputResource();
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

    protected override bool CanAddInputResource()
    {
        return GetAvailableDeskInputCapacity() > 0;
    }

    protected override bool TryExecuteQueuedAddInputResource(PendingInputRequest pendingRequest)
    {
        int targetIndex = GetNextStackInputSlotIndex();
        if (targetIndex < 0)
        {
            return false;
        }

        PortableResource targetResource = inputResourcesStack[targetIndex];
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
                    inputSlotStates[targetIndex] = DeskInputSlotState.Occupied;
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
                    inputSlotStates[targetIndex] = DeskInputSlotState.Occupied;
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
        int sourceIndex = GetTopOccupiedInputSlotIndex();
        if (sourceIndex < 0)
        {
            return true;
        }

        if (targetPresoner == null || !targetPresoner.NeedResource(inputResourceType))
        {
            return true;
        }

        PortableResource sourceResource = inputResourcesStack[sourceIndex];
        if (sourceResource == null)
        {
            return true;
        }

        inputSlotStates[sourceIndex] = DeskInputSlotState.Outgoing;
        RefreshOccupiedInputCount();

        sourceResource.PlayTransferAnimation
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

                inputSlotStates[sourceIndex] = DeskInputSlotState.Empty;
            }
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
        return targetPresoner != null
            && targetPresoner.NeedResource(inputResourceType)
            && GetAvailableDeskOccupiedInputCount() > 0;
    }

    private int GetNextStackInputSlotIndex()
    {
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

    private void AddCompletionOutputResources()
    {
        int addCount = Mathf.Min(PresonerCompletionOutputCount, outputResourcesStack.Count - outputCount);

        for (int i = 0; i < addCount; ++i)
        {
            AddOutputResource();
        }
    }
}
