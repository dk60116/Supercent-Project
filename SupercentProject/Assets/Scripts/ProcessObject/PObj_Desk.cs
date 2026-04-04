using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PObj_Desk : ProcessObject
{
    [SerializeField]
    private float prisonerOutputInterval = 0.1f;
    [SerializeField]
    private float prisonerTransferDuration = 0.1f;
    [SerializeField]
    private Vector3 prisonerTargetOffset = new Vector3(0f, 1f, 0f);

    protected new void Update()
    {
        Presoner targetPresoner = GetCounterPresoner();
        bool canOutputToPresoner = targetPresoner != null && inputCount > 0;

        if (!(enterInput || canOutputToPresoner))
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

            if (enterInput && GameManager.Instance.Player.Controller.GetResourceCount(inputResourceType) > 0)
            {
                GameManager.Instance.Player.Controller.SubResrouce(inputResourceType);
                AddInputResource();
            }

            tickTIme = 0f;
        }

        tickTIme += Time.deltaTime;
    }

    public override void SubOutputResource()
    {
        SubOutputResource(GetCounterPresoner());
    }

    private void SubOutputResource(Presoner targetPresoner)
    {
        if (targetPresoner == null || inputCount <= 0)
        {
            return;
        }

        PortableResource sourceResource = inputResourcesStack[inputCount - 1];
        if (sourceResource == null)
        {
            return;
        }

        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetPresoner.transform.position + prisonerTargetOffset,
            targetPresoner.transform.eulerAngles,
            prisonerTransferDuration,
            null
        );

        --inputCount;
    }

    private Presoner GetCounterPresoner()
    {
        return GameManager.Instance.WaitingLine.GetCounterPresoner();
    }
}
