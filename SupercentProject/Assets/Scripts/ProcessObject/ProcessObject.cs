using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProcessObject : BaseObject
{
    private const string DefaultInputStackRootName = "Resources_Input";
    private const string DefaultOutputStackRootName = "Resources_Output";

    [SerializeField]
    protected ResourceType inputResourceType, outputResourceType;

    [SerializeField]
    protected Animator animator;

    [SerializeField]
    protected List<PortableResource> inputResourcesStack, outputResourcesStack;
    [SerializeField]
    protected Transform inputStackRoot, outputStackRoot;
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

    private bool[] inputSlotsPendingClear;
    private bool[] inputSlotsPendingFill;

    protected void Awake()
    {
        EnsureInputSlotStateCache();

        for (int i = 0; i < inputResourcesStack.Count; ++i)
            inputResourcesStack[i].gameObject.SetActive(false);
        for (int i = 0; i < outputResourcesStack.Count; ++i)
            outputResourcesStack[i].gameObject.SetActive(false);
    }

    public void AutoFillResourceStacks()
    {
        inputStackRoot = ResolveStackRoot(inputStackRoot, DefaultInputStackRootName);
        outputStackRoot = ResolveStackRoot(outputStackRoot, DefaultOutputStackRootName);

        FillStackFromRoot(inputStackRoot, ref inputResourcesStack);
        FillStackFromRoot(outputStackRoot, ref outputResourcesStack);

        EnsureInputSlotStateCache();

        inputCount = Mathf.Clamp(inputCount, 0, inputResourcesStack != null ? inputResourcesStack.Count : 0);
        outputCount = Mathf.Clamp(outputCount, 0, outputResourcesStack != null ? outputResourcesStack.Count : 0);
    }

    protected void Update()
    {
        if (enterInput || enterOutput)
        {
            if (tickTIme == 0f || tickTIme >= 0.1f)
            {
                if (enterInput
                    && GameManager.Instance.Player.Controller.GetResourceCount(inputResourceType) > 0
                    && CanAddInputResource())
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
        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return;
        }

        PortableResource sourceResource = GameManager.Instance.Player.Controller.GetPoppedResource(inputResourceType);
        PortableResource targetResource = inputResourcesStack[nextInputIndex];
        ReserveInputSlot(nextInputIndex);

        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            0.25f,
            targetResource.gameObject,
            () => FinalizeIncomingInputSlot(nextInputIndex)
        );
    }

    public virtual void SubInputResource()
    {
        if (inputCount <= 0)
            return;

        EnsureInputSlotStateCache();

        int targetIndex = inputCount - 1;
        PortableResource targetResource = inputResourcesStack[targetIndex];
        inputSlotsPendingClear[targetIndex] = true;
        --inputCount;

        if (targetResource == null)
        {
            FinalizeConsumedInputSlot(targetIndex, null, Vector3.one);
            return;
        }

        Vector3 originalScale = targetResource.transform.localScale;

        targetResource.transform.DOKill();
        targetResource.transform.DOScale(Vector3.zero, 0.3f)
            .OnComplete(() => FinalizeConsumedInputSlot(targetIndex, targetResource, originalScale))
            .OnKill(() =>
            {
                if (IsInputSlotPendingClear(targetIndex))
                {
                    FinalizeConsumedInputSlot(targetIndex, targetResource, originalScale);
                }
            });
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
        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return null;
        }

        return inputResourcesStack[nextInputIndex];
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

    public bool TryAddExternalInputResource(PortableResource sourceResource, float duration, float jumpPower = 1f, int jumpCount = 1)
    {
        if (sourceResource == null)
        {
            return false;
        }

        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return false;
        }

        PortableResource targetResource = inputResourcesStack[nextInputIndex];
        if (targetResource == null)
        {
            return false;
        }

        ReserveInputSlot(nextInputIndex);

        sourceResource.PlayTransferAnimation_Jum(
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            duration,
            targetResource.gameObject,
            () => FinalizeIncomingInputSlot(nextInputIndex),
            jumpPower,
            jumpCount);

        return true;
    }

    protected virtual bool CanAddInputResource()
    {
        return GetNextInputResourceIndex() >= 0;
    }

    private Transform ResolveStackRoot(Transform currentRoot, string fallbackName)
    {
        if (currentRoot != null)
        {
            return currentRoot;
        }

        return FindChildByName(transform, fallbackName);
    }

    private void FillStackFromRoot(Transform root, ref List<PortableResource> stack)
    {
        if (stack == null)
        {
            stack = new List<PortableResource>();
        }
        else
        {
            stack.Clear();
        }

        if (root == null)
        {
            return;
        }

        CollectPortableResources(root, stack);
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

    private int GetNextInputResourceIndex()
    {
        EnsureInputSlotStateCache();

        if (inputResourcesStack == null)
        {
            return -1;
        }

        for (int i = inputCount; i < inputResourcesStack.Count; ++i)
        {
            if (inputSlotsPendingClear[i] || inputSlotsPendingFill[i])
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private void FinalizeConsumedInputSlot(int targetIndex, PortableResource targetResource, Vector3 originalScale)
    {
        if (!IsInputSlotPendingClear(targetIndex))
        {
            return;
        }

        inputSlotsPendingClear[targetIndex] = false;

        if (targetResource == null)
        {
            return;
        }

        targetResource.transform.localScale = originalScale;
        targetResource.gameObject.SetActive(false);
        RefreshInputCountFromSlots();
    }

    private void FinalizeIncomingInputSlot(int targetIndex)
    {
        if (inputSlotsPendingFill == null
            || targetIndex < 0
            || targetIndex >= inputSlotsPendingFill.Length
            || !inputSlotsPendingFill[targetIndex])
        {
            return;
        }

        inputSlotsPendingFill[targetIndex] = false;
        RefreshInputCountFromSlots();
    }

    private void ReserveInputSlot(int targetIndex)
    {
        EnsureInputSlotStateCache();

        if (targetIndex < 0 || targetIndex >= inputSlotsPendingFill.Length)
        {
            return;
        }

        inputSlotsPendingFill[targetIndex] = true;
    }

    private void RefreshInputCountFromSlots()
    {
        if (inputResourcesStack == null)
        {
            inputCount = 0;
            return;
        }

        int occupiedCount = 0;

        for (int i = 0; i < inputResourcesStack.Count; ++i)
        {
            if (inputSlotsPendingClear != null && inputSlotsPendingClear[i])
            {
                break;
            }

            if (inputSlotsPendingFill != null && inputSlotsPendingFill[i])
            {
                break;
            }

            PortableResource portableResource = inputResourcesStack[i];
            if (portableResource == null || !portableResource.gameObject.activeSelf)
            {
                break;
            }

            ++occupiedCount;
        }

        inputCount = occupiedCount;
    }

    private bool IsInputSlotPendingClear(int targetIndex)
    {
        return inputSlotsPendingClear != null
            && targetIndex >= 0
            && targetIndex < inputSlotsPendingClear.Length
            && inputSlotsPendingClear[targetIndex];
    }

    private void EnsureInputSlotStateCache()
    {
        int inputStackCount = inputResourcesStack != null ? inputResourcesStack.Count : 0;
        if (inputSlotsPendingClear == null || inputSlotsPendingClear.Length != inputStackCount)
        {
            inputSlotsPendingClear = new bool[inputStackCount];
        }

        if (inputSlotsPendingFill == null || inputSlotsPendingFill.Length != inputStackCount)
        {
            inputSlotsPendingFill = new bool[inputStackCount];
        }
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
