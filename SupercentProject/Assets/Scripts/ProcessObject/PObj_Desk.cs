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

    private DeskInputSlotState[] inputSlotStates;

    protected new void Awake()
    {
        base.Awake();

        inputSlotStates = new DeskInputSlotState[inputResourcesStack.Count];
        inputCount = 0;
    }

    protected new void Update()
    {
        Presoner targetPresoner = GetCounterPresoner();
        bool canOutputToPlayer = enterOutput && outputCount > 0;
        bool canOutputToPresoner = targetPresoner != null
            && targetPresoner.NeedResource(inputResourceType)
            && HasOccupiedInputSlot();
        int nextInputSlotIndex = GetNextStackInputSlotIndex();
        bool canAcceptInput = enterInput
            && GameManager.Instance.Player.Controller.GetResourceCount(inputResourceType) > 0
            && nextInputSlotIndex >= 0;

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
                SubOutputResource(targetPresoner);
            }

            if (canAcceptInput)
            {
                GameManager.Instance.Player.Controller.SubResrouce(inputResourceType);
                AddInputResource(nextInputSlotIndex);
            }

            if (canOutputToPlayer)
            {
                GameManager.Instance.Player.Controller.AddResource(outputResourceType);
                base.SubOutputResource();
            }

            tickTIme = 0f;
        }

        tickTIme += Time.deltaTime;
    }

    public override void SubOutputResource()
    {
        SubOutputResource(GetCounterPresoner());
    }

    protected override void AddInputResource()
    {
        AddInputResource(GetNextStackInputSlotIndex());
    }

    private void AddInputResource(int targetIndex)
    {
        if (targetIndex < 0)
        {
            return;
        }

        PortableResource sourceResource = GameManager.Instance.Player.Controller.GetPoppedResource(inputResourceType);
        PortableResource targetResource = inputResourcesStack[targetIndex];

        inputSlotStates[targetIndex] = DeskInputSlotState.Incoming;

        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            0.25f,
            targetResource.gameObject,
            () =>
            {
                inputSlotStates[targetIndex] = DeskInputSlotState.Occupied;
                RefreshOccupiedInputCount();
            }
        );
    }

    private void SubOutputResource(Presoner targetPresoner)
    {
        int sourceIndex = GetTopOccupiedInputSlotIndex();
        if (targetPresoner == null || sourceIndex < 0)
        {
            return;
        }

        PortableResource sourceResource = inputResourcesStack[sourceIndex];
        if (sourceResource == null)
        {
            return;
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
    }

    private Presoner GetCounterPresoner()
    {
        return GameManager.Instance.WaitingLine.GetCounterPresoner();
    }

    private bool HasOccupiedInputSlot()
    {
        return GetTopOccupiedInputSlotIndex() >= 0;
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

    private void AddCompletionOutputResources()
    {
        int addCount = Mathf.Min(PresonerCompletionOutputCount, outputResourcesStack.Count - outputCount);

        for (int i = 0; i < addCount; ++i)
        {
            AddOutputResource();
        }
    }
}
