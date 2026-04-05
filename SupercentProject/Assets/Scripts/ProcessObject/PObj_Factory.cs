using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PObj_Factory : ProcessObject
{
    [SerializeField, ReadOnly]
    bool processRunning;
    [SerializeField, ReadOnly]
    private int pendingExternalOutputRequestCount;

    protected new void Update()
    {
        base.Update();

        processRunning = inputCount > 0;

        animator.SetBool("bRunning", processRunning);
    }

    public bool TryTransferOutputToExternal(PortableResource targetResource, float duration, bool useJumpAnimation = false, float jumpPower = 1f, int jumpCount = 1)
    {
        if (targetResource == null || ShouldPrioritizePlayerOutput() || GetAvailableFactoryOutputCount() <= 0)
        {
            return false;
        }

        bool enqueued = EnqueueCustomResourceOperation(() =>
        {
            bool completed = TryExecuteQueuedExternalOutput(targetResource, duration, useJumpAnimation, jumpPower, jumpCount);
            if (completed)
            {
                pendingExternalOutputRequestCount = Mathf.Max(0, pendingExternalOutputRequestCount - 1);
            }

            return completed;
        });

        if (enqueued)
        {
            ++pendingExternalOutputRequestCount;
        }

        return enqueued;
    }

    private bool TryExecuteQueuedExternalOutput(PortableResource targetResource, float duration, bool useJumpAnimation, float jumpPower, int jumpCount)
    {
        int sourceIndex = GetCurrentOutputResourceSlotIndex();
        if (sourceIndex < 0)
        {
            return true;
        }

        PortableResource sourceResource = GetStackResource(outputResourcesStack, sourceIndex);
        if (sourceResource == null || targetResource == null)
        {
            return true;
        }

        ReserveOutputSlotClear(sourceIndex);

        if (useJumpAnimation)
        {
            sourceResource.PlayTransferAnimation_Jum(
                sourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                duration,
                targetResource.gameObject,
                () => FinalizeConsumedOutputSlot(sourceIndex, sourceResource),
                jumpPower,
                jumpCount);
        }
        else
        {
            sourceResource.PlayTransferAnimation(
                sourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                duration,
                targetResource.gameObject,
                () => FinalizeConsumedOutputSlot(sourceIndex, sourceResource));
        }

        return true;
    }

    private int GetAvailableFactoryOutputCount()
    {
        return Mathf.Max(0, outputCount + PendingAddOutputRequestCount - PendingSubOutputRequestCount - pendingExternalOutputRequestCount);
    }

    private bool ShouldPrioritizePlayerOutput()
    {
        if (!enterOutput || GameManager.Instance == null || GameManager.Instance.Player == null)
        {
            return false;
        }

        PlayerController playerController = GameManager.Instance.Player.Controller;
        return playerController != null && playerController.CanAddResource(outputResourceType);
    }

    public bool ProcessRunning => processRunning;
    public int InputCount => inputCount;
    public int OutputCount => outputCount;
}
