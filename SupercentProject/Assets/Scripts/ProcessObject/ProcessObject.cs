using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProcessObject : BaseObject
{
    [SerializeField]
    protected ResourceType inputResourceType, outputResourceType;

    [SerializeField]
    protected Animator animator;

    [SerializeField]
    protected List<PortableResource> inputResourcesStack, outputResourcesStack;
    [SerializeField, ReadOnly]
    protected int inputCount, outputCount;

    [SerializeField, ReadOnly]
    protected bool enterInput, enterOutput;

    [SerializeField]
    protected SpriteRenderer areaRect;

    [SerializeField]
    protected Color baseColor, enterColor;

    [SerializeField, ReadOnly]
    protected float tickTIme;

    protected void Awake()
    {
        for (int i = 0; i < inputResourcesStack.Count; ++i)
            inputResourcesStack[i].gameObject.SetActive(false);
        for (int i = 0; i < outputResourcesStack.Count; ++i)
            outputResourcesStack[i].gameObject.SetActive(false);
    }

    protected void Update()
    {
        if (enterInput || enterOutput)
        {
            if (tickTIme == 0f || tickTIme >= 0.1f)
            {
                if (enterInput && GameManager.Instance.Player.Controller.GetResourceCount(inputResourceType) > 0)
                {
                    GameManager.Instance.Player.Controller.SubResrouce(inputResourceType);
                    AddInputResource();
                }

                if (enterOutput && outputCount > 0)
                {
                    GameManager.Instance.Player.Controller.AddResource(outputResourceType);
                    SubOutputResource();
                }
                
                tickTIme = 0f;
            }

            tickTIme += Time.deltaTime;
        }
    }

    public void EnterInputAreaEvent()
    {
        enterInput = true;
        areaRect.color = enterColor;
    }

    public void OutInputAreaEvent()
    {
        enterInput = false;
        areaRect.color = baseColor;
    }

    public void EnterOutputAreaEvent()
    {
        enterOutput = true;
    }

    public void OutOutputAreaEvent()
    {
        enterOutput = false;
    }

    virtual protected void AddInputResource()
    {
        PortableResource sourceResource = GameManager.Instance.Player.Controller.GetPoppedResource(inputResourceType);
        PortableResource targetResource = GetNextInputResource();

        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            0.25f,
            targetResource.gameObject
        );

        ++inputCount;
    }

    public virtual void SubInputResource()
    {
        if (inputCount <= 0)
            return;

        PortableResource targetResource = inputResourcesStack[inputCount - 1];
        Vector3 originalScale = targetResource.transform.localScale;

        targetResource.transform.DOKill();
        targetResource.transform.DOScale(Vector3.zero, 0.3f).OnComplete(() =>
        {
            targetResource.transform.localScale = originalScale;
            targetResource.gameObject.SetActive(false);
        });

        --inputCount;
    }

    public virtual void AddOutputResource()
    {
        GetNextOutputResource().gameObject.SetActive(true);

        ++outputCount;
    }

    public virtual void SubOutputResource()
    {
        PortableResource sourceResource = GetCurrentOutputResource();
        PortableResource targetResource = GameManager.Instance.Player.Controller.GetCurrentTopResource(outputResourceType);

        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            0.25f,
            targetResource.gameObject
        );

        --outputCount;
    }

    public PortableResource GetNextInputResource()
    {
        return inputResourcesStack[inputCount];
    }

    public PortableResource GetNextOutputResource()
    {
        return outputResourcesStack[outputCount];
    }

    public PortableResource GetCurrentOutputResource()
    {
        if (outputCount <= 0)
        {
            return null;
        }

        return outputResourcesStack[outputCount - 1];
    }
}
