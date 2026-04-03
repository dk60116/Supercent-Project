using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProcessObject : BaseObject
{
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
        {
            inputResourcesStack[i].gameObject.SetActive(false);
        }
    }

    protected void Update()
    {
        if (enterInput || enterOutput)
        {
            if (tickTIme == 0f || tickTIme >= 0.1f)
            {
                if (GameManager.Instance.Player.Controller.OreCount > 0)
                {
                    GameManager.Instance.Player.Controller.SubOre();
                    AbleInputStack();
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

    virtual protected void AbleInputStack()
    {
        GameManager.Instance.Player.Controller.GetTopOre().PlayTransferAnimation(GameManager.Instance.Player.Controller.GetTopOre().transform.position, GetTopResource().transform.position, GetTopResource().transform.eulerAngles, 0.2f, GetTopResource().gameObject);

        ++inputCount;
    }

    public virtual void SubInputResource()
    {
        GetTopResource().transform.DOKill();
        GetTopResource().transform.localScale = Vector3.one;
        GetTopResource().transform.DOScale(0f, 2f).OnComplete(() =>
        {
            GetTopResource().transform.localScale = Vector3.one;
            GetTopResource().gameObject.SetActive(false);
        });

        --inputCount;
    }

    PortableResource GetTopResource()
    {
        return inputResourcesStack[inputCount];
    }
}
