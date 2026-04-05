using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProcessObject : BaseObject
{
    [SerializeField]
    protected AudioClip pushSound, popSound; 
    [SerializeField]
    protected AudioClip inputEnterSound, outputEntertSound;

    private const string DefaultInputStackRootName = "Resources_Input";
    private const string DefaultOutputStackRootName = "Resources_Output";
    private const float DefaultResourceOperationInterval = 0.05f;
    private const float DefaultTransferDuration = 0.05f;

    protected struct PendingInputRequest
    {
        public PortableResource SourceResource;
        public float Duration;
        public bool UseJumpAnimation;
        public float JumpPower;
        public int JumpCount;
    }

    private enum PendingResourceOperationType
    {
        AddInput,
        SubInput,
        AddOutput,
        SubOutput,
        Custom,
    }

    private struct PendingResourceOperation
    {
        public PendingResourceOperationType Type;
        public PendingInputRequest InputRequest;
        public Func<bool> CustomExecutor;
    }

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
    [SerializeField, ReadOnly]
    private int pendingSubInputRequests;
    [SerializeField, ReadOnly]
    private int pendingAddInputRequestCount;
    [SerializeField, ReadOnly]
    private int pendingAddOutputRequestCount;
    [SerializeField, ReadOnly]
    private int pendingSubOutputRequestCount;
    [SerializeField, ReadOnly]
    private int pendingCustomOperationCount;

    private bool[] inputSlotsPendingClear;
    private bool[] inputSlotsPendingFill;
    private bool[] outputSlotsPendingClear;
    private readonly Queue<PendingResourceOperation> pendingResourceOperations = new Queue<PendingResourceOperation>();
    private int lastProcessedResourceOperationFrame = -1;

    protected void Awake()
    {
        EnsureInputSlotStateCache();
        EnsureOutputSlotStateCache();
        ClampResourceCountsToStackCapacity();
        DeactivateStack(inputResourcesStack);
        DeactivateStack(outputResourcesStack);
    }

    public void AutoFillResourceStacks()
    {
        inputStackRoot = ResolveStackRoot(inputStackRoot, DefaultInputStackRootName);
        outputStackRoot = ResolveStackRoot(outputStackRoot, DefaultOutputStackRootName);

        FillStackFromRoot(inputStackRoot, ref inputResourcesStack);
        FillStackFromRoot(outputStackRoot, ref outputResourcesStack);

        EnsureInputSlotStateCache();
        EnsureOutputSlotStateCache();
        ClampResourceCountsToStackCapacity();
    }

    protected void Update()
    {
        PlayerController playerController = GetPlayerController();
        if (playerController == null)
        {
            return;
        }

        if (enterInput || enterOutput)
        {
            if (tickTIme == 0f || tickTIme >= DefaultResourceOperationInterval)
            {
                if (enterInput
                    && playerController.GetResourceCount(inputResourceType) > 0
                    && CanAddInputResource())
                {
                    AddInputResource();
                    PlaySound(pushSound);
                }

                if (enterOutput && CanSubOutputResource())
                {
                    SubOutputResource();
                    PlaySound(popSound);
                }

                tickTIme = 0f;
            }

            tickTIme += Time.deltaTime;
        }
    }

    protected void LateUpdate()
    {
        FlushPendingResourceOperations();
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

        if (outputCount > 0)
            PlaySound(outputEntertSound);
    }

    public void OutOutputAreaEvent()
    {
        enterOutput = false;
    }

    virtual protected void AddInputResource()
    {
        PlayerController playerController = GetPlayerController();
        if (playerController == null || !CanAddInputResource())
        {
            return;
        }

        PortableResource sourceResource = playerController.GetCurrentTopResource(inputResourceType);
        if (sourceResource == null)
        {
            return;
        }

        playerController.SubResrouce(inputResourceType);

        if (!EnqueueInputRequest(sourceResource, DefaultTransferDuration, false, 0f, 0))
        {
            playerController.AddResource(inputResourceType);
        }
    }

    public virtual void SubInputResource()
    {
        if (!CanSubInputResource())
        {
            return;
        }

        EnqueueOperation(PendingResourceOperationType.SubInput);
    }

    public virtual void AddOutputResource()
    {
        if (!CanAddOutputResource())
        {
            return;
        }

        EnqueueOperation(PendingResourceOperationType.AddOutput);
    }

    public virtual void SubOutputResource()
    {
        if (!CanSubOutputResource())
        {
            return;
        }

        EnqueueOperation(PendingResourceOperationType.SubOutput);
    }

    public PortableResource GetNextInputResource()
    {
        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return null;
        }

        return GetStackResource(inputResourcesStack, nextInputIndex);
    }

    public PortableResource GetNextOutputResource()
    {
        int nextOutputIndex = GetNextOutputResourceIndex();
        if (nextOutputIndex < 0)
        {
            return null;
        }

        return GetStackResource(outputResourcesStack, nextOutputIndex);
    }

    public PortableResource GetCurrentOutputResource()
    {
        int currentOutputIndex = GetCurrentOutputResourceIndex();
        if (currentOutputIndex < 0)
        {
            return null;
        }

        return GetStackResource(outputResourcesStack, currentOutputIndex);
    }

    protected int GetCurrentOutputResourceSlotIndex()
    {
        return GetCurrentOutputResourceIndex();
    }

    protected int GetNextOutputResourceSlotIndex()
    {
        return GetNextOutputResourceIndex();
    }

    public bool TryAddExternalInputResource(PortableResource sourceResource, float duration, float jumpPower = 1f, int jumpCount = 1)
    {
        return TryAddExternalInputResource(sourceResource, duration, true, jumpPower, jumpCount);
    }

    public bool TryAddExternalInputResource(PortableResource sourceResource, float duration, bool useJumpAnimation, float jumpPower = 1f, int jumpCount = 1)
    {
        if (sourceResource == null)
        {
            return false;
        }

        if (!CanAddInputResource())
        {
            return false;
        }

        return EnqueueInputRequest(sourceResource, duration, useJumpAnimation, jumpPower, jumpCount);
    }

    protected virtual bool CanAddInputResource()
    {
        return GetAvailableQueuedInputCapacity() > 0;
    }

    protected virtual bool CanSubInputResource()
    {
        ClampResourceCountsToStackCapacity();
        return inputCount - pendingSubInputRequests > 0;
    }

    protected virtual bool CanAddOutputResource()
    {
        ClampResourceCountsToStackCapacity();
        return GetAvailableQueuedOutputCapacity() > 0;
    }

    protected virtual bool CanSubOutputResource()
    {
        ClampResourceCountsToStackCapacity();
        PlayerController playerController = GetPlayerController();
        return playerController != null
            && playerController.CanAddResource(outputResourceType)
            && GetAvailableQueuedOutputCount() > 0;
    }

    protected bool EnqueueCustomResourceOperation(Func<bool> customExecutor)
    {
        if (customExecutor == null)
        {
            return false;
        }

        return EnqueueOperation(new PendingResourceOperation
        {
            Type = PendingResourceOperationType.Custom,
            CustomExecutor = customExecutor,
        });
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

    protected PortableResource GetStackResource(List<PortableResource> stack, int index)
    {
        if (stack == null || index < 0 || index >= stack.Count)
        {
            return null;
        }

        return stack[index];
    }

    protected int GetStackCount(List<PortableResource> stack)
    {
        return stack != null ? stack.Count : 0;
    }

    private void ClampResourceCountsToStackCapacity()
    {
        inputCount = Mathf.Clamp(inputCount, 0, GetStackCount(inputResourcesStack));
        outputCount = Mathf.Clamp(outputCount, 0, GetStackCount(outputResourcesStack));
    }

    private int GetNextInputResourceIndex()
    {
        EnsureInputSlotStateCache();

        if (inputResourcesStack == null)
        {
            return -1;
        }

        for (int i = 0; i < inputResourcesStack.Count; ++i)
        {
            if (inputSlotsPendingClear[i])
            {
                return -1;
            }

            PortableResource portableResource = inputResourcesStack[i];
            bool isOccupied = portableResource != null && portableResource.gameObject.activeSelf;

            if (inputSlotsPendingFill[i] || isOccupied)
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private bool EnqueueInputRequest(PortableResource sourceResource, float duration, bool useJumpAnimation, float jumpPower, int jumpCount)
    {
        if (sourceResource == null || !CanAddInputResource())
        {
            return false;
        }

        return EnqueueOperation(new PendingResourceOperation
        {
            Type = PendingResourceOperationType.AddInput,
            InputRequest = new PendingInputRequest
            {
                SourceResource = sourceResource,
                Duration = duration,
                UseJumpAnimation = useJumpAnimation,
                JumpPower = jumpPower,
                JumpCount = jumpCount,
            },
        });
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
        EnsureInputSlotStateCache();

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

    private void RefreshOutputCountFromSlots()
    {
        EnsureOutputSlotStateCache();

        if (outputResourcesStack == null)
        {
            outputCount = 0;
            return;
        }

        int occupiedCount = 0;

        for (int i = 0; i < outputResourcesStack.Count; ++i)
        {
            if (outputSlotsPendingClear != null && outputSlotsPendingClear[i])
            {
                break;
            }

            PortableResource portableResource = outputResourcesStack[i];
            if (portableResource == null || !portableResource.gameObject.activeSelf)
            {
                break;
            }

            ++occupiedCount;
        }

        outputCount = occupiedCount;
    }

    private void FlushPendingResourceOperations()
    {
        if (pendingResourceOperations.Count <= 0
            || lastProcessedResourceOperationFrame == Time.frameCount)
        {
            return;
        }

        PendingResourceOperation pendingOperation = pendingResourceOperations.Peek();
        if (!TryExecutePendingResourceOperation(pendingOperation))
        {
            return;
        }

        pendingResourceOperations.Dequeue();
        AdjustPendingOperationCount(pendingOperation.Type, -1);
        lastProcessedResourceOperationFrame = Time.frameCount;
    }

    private bool TryExecutePendingResourceOperation(PendingResourceOperation pendingOperation)
    {
        switch (pendingOperation.Type)
        {
            case PendingResourceOperationType.AddInput:
                return TryExecuteQueuedAddInputResource(pendingOperation.InputRequest);
            case PendingResourceOperationType.SubInput:
                return TryExecuteQueuedSubInputResource();
            case PendingResourceOperationType.AddOutput:
                return TryExecuteQueuedAddOutputResource();
            case PendingResourceOperationType.SubOutput:
                return TryExecuteQueuedSubOutputResource();
            case PendingResourceOperationType.Custom:
                return pendingOperation.CustomExecutor == null || pendingOperation.CustomExecutor();
        }

        return true;
    }

    protected virtual bool TryExecuteQueuedAddInputResource(PendingInputRequest pendingRequest)
    {
        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return false;
        }

        PortableResource targetResource = GetStackResource(inputResourcesStack, nextInputIndex);
        if (targetResource == null || pendingRequest.SourceResource == null)
        {
            return true;
        }

        ReserveInputSlot(nextInputIndex);

        if (pendingRequest.UseJumpAnimation)
        {
            pendingRequest.SourceResource.PlayTransferAnimation_Jum(
                pendingRequest.SourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                pendingRequest.Duration,
                targetResource.gameObject,
                () => FinalizeIncomingInputSlot(nextInputIndex),
                pendingRequest.JumpPower,
                pendingRequest.JumpCount);
        }
        else
        {
            pendingRequest.SourceResource.PlayTransferAnimation(
                pendingRequest.SourceResource.transform.position,
                targetResource.transform.position,
                targetResource.transform.eulerAngles,
                pendingRequest.Duration,
                targetResource.gameObject,
                () => FinalizeIncomingInputSlot(nextInputIndex));
        }

        return true;
    }

    protected virtual bool TryExecuteQueuedSubInputResource()
    {
        if (HasPendingInputSlotUpdates())
        {
            return false;
        }

        if (inputCount <= 0)
        {
            return true;
        }

        EnsureInputSlotStateCache();
        ClampResourceCountsToStackCapacity();

        int targetIndex = inputCount - 1;
        if (targetIndex < 0
            || inputResourcesStack == null
            || targetIndex >= inputResourcesStack.Count
            || targetIndex >= inputSlotsPendingClear.Length)
        {
            RefreshInputCountFromSlots();
            return true;
        }

        PortableResource targetResource = GetStackResource(inputResourcesStack, targetIndex);
        inputSlotsPendingClear[targetIndex] = true;
        --inputCount;

        if (targetResource == null)
        {
            FinalizeConsumedInputSlot(targetIndex, null, Vector3.one);
            return true;
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

        return true;
    }

    protected virtual bool TryExecuteQueuedAddOutputResource()
    {
        int nextOutputIndex = GetNextOutputResourceIndex();
        if (nextOutputIndex < 0)
        {
            return false;
        }

        PortableResource targetResource = GetStackResource(outputResourcesStack, nextOutputIndex);
        if (targetResource == null)
        {
            return true;
        }

        targetResource.gameObject.SetActive(true);
        RefreshOutputCountFromSlots();
        return true;
    }

    protected virtual bool TryExecuteQueuedSubOutputResource()
    {
        RefreshOutputCountFromSlots();

        PlayerController playerController = GetPlayerController();
        if (playerController == null)
        {
            return false;
        }

        if (!playerController.CanAddResource(outputResourceType))
        {
            return false;
        }

        if (outputCount <= 0)
        {
            return true;
        }

        int sourceIndex = GetCurrentOutputResourceIndex();
        if (sourceIndex < 0)
        {
            return true;
        }

        PortableResource sourceResource = GetStackResource(outputResourcesStack, sourceIndex);
        PortableResource targetResource = playerController.GetPoppedResource(outputResourceType);
        if (sourceResource == null || targetResource == null)
        {
            return true;
        }

        outputSlotsPendingClear[sourceIndex] = true;
        RefreshOutputCountFromSlots();

        playerController.AddResource(outputResourceType);
        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            DefaultTransferDuration,
            targetResource.gameObject,
            () => FinalizeConsumedOutputSlot(sourceIndex, sourceResource)
        );

        return true;
    }

    private bool HasPendingInputSlotUpdates()
    {
        EnsureInputSlotStateCache();

        for (int i = 0; i < inputSlotsPendingClear.Length; ++i)
        {
            if (inputSlotsPendingClear[i] || inputSlotsPendingFill[i])
            {
                return true;
            }
        }

        return false;
    }

    private int GetAvailableQueuedInputCapacity()
    {
        int totalInputSlotCount = GetStackCount(inputResourcesStack);
        if (totalInputSlotCount <= 0)
        {
            return 0;
        }

        int nextInputIndex = GetNextInputResourceIndex();
        if (nextInputIndex < 0)
        {
            return 0;
        }

        return Mathf.Max(0, totalInputSlotCount - nextInputIndex - pendingAddInputRequestCount);
    }

    private int GetAvailableQueuedOutputCount()
    {
        RefreshOutputCountFromSlots();
        return Mathf.Max(0, outputCount + pendingAddOutputRequestCount - pendingSubOutputRequestCount);
    }

    private int GetAvailableQueuedOutputCapacity()
    {
        RefreshOutputCountFromSlots();

        int totalOutputSlotCount = GetStackCount(outputResourcesStack);
        if (totalOutputSlotCount <= 0)
        {
            return 0;
        }

        int firstEmptyIndex = GetNextOutputResourceIndex();
        if (firstEmptyIndex < 0)
        {
            return 0;
        }

        return Mathf.Max(0, totalOutputSlotCount - firstEmptyIndex - pendingAddOutputRequestCount);
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

    private void EnsureOutputSlotStateCache()
    {
        int outputStackCount = outputResourcesStack != null ? outputResourcesStack.Count : 0;
        if (outputSlotsPendingClear == null || outputSlotsPendingClear.Length != outputStackCount)
        {
            outputSlotsPendingClear = new bool[outputStackCount];
        }
    }

    private int GetNextOutputResourceIndex()
    {
        EnsureOutputSlotStateCache();
        RefreshOutputCountFromSlots();

        if (outputResourcesStack == null)
        {
            return -1;
        }

        for (int i = 0; i < outputResourcesStack.Count; ++i)
        {
            if (outputSlotsPendingClear != null && outputSlotsPendingClear[i])
            {
                return -1;
            }

            PortableResource portableResource = outputResourcesStack[i];
            if (portableResource == null || !portableResource.gameObject.activeSelf)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetCurrentOutputResourceIndex()
    {
        EnsureOutputSlotStateCache();
        RefreshOutputCountFromSlots();

        if (outputResourcesStack == null)
        {
            return -1;
        }

        for (int i = outputResourcesStack.Count - 1; i >= 0; --i)
        {
            if (outputSlotsPendingClear != null && outputSlotsPendingClear[i])
            {
                continue;
            }

            PortableResource portableResource = outputResourcesStack[i];
            if (portableResource != null && portableResource.gameObject.activeSelf)
            {
                return i;
            }
        }

        return -1;
    }

    protected void ReserveOutputSlotClear(int targetIndex)
    {
        EnsureOutputSlotStateCache();

        if (targetIndex < 0 || targetIndex >= outputSlotsPendingClear.Length)
        {
            return;
        }

        outputSlotsPendingClear[targetIndex] = true;
        RefreshOutputCountFromSlots();
    }

    protected void FinalizeConsumedOutputSlot(int targetIndex, PortableResource targetResource)
    {
        if (outputSlotsPendingClear == null
            || targetIndex < 0
            || targetIndex >= outputSlotsPendingClear.Length
            || !outputSlotsPendingClear[targetIndex])
        {
            return;
        }

        outputSlotsPendingClear[targetIndex] = false;

        if (targetResource != null)
        {
            targetResource.transform.localScale = Vector3.one;
            targetResource.gameObject.SetActive(false);
        }

        RefreshOutputCountFromSlots();
    }

    private bool EnqueueOperation(PendingResourceOperationType operationType)
    {
        return EnqueueOperation(new PendingResourceOperation
        {
            Type = operationType,
        });
    }

    private bool EnqueueOperation(PendingResourceOperation pendingOperation)
    {
        pendingResourceOperations.Enqueue(pendingOperation);
        AdjustPendingOperationCount(pendingOperation.Type, 1);
        return true;
    }

    private void AdjustPendingOperationCount(PendingResourceOperationType operationType, int delta)
    {
        switch (operationType)
        {
            case PendingResourceOperationType.AddInput:
                pendingAddInputRequestCount = Mathf.Max(0, pendingAddInputRequestCount + delta);
                break;
            case PendingResourceOperationType.SubInput:
                pendingSubInputRequests = Mathf.Max(0, pendingSubInputRequests + delta);
                break;
            case PendingResourceOperationType.AddOutput:
                pendingAddOutputRequestCount = Mathf.Max(0, pendingAddOutputRequestCount + delta);
                break;
            case PendingResourceOperationType.SubOutput:
                pendingSubOutputRequestCount = Mathf.Max(0, pendingSubOutputRequestCount + delta);
                break;
            case PendingResourceOperationType.Custom:
                pendingCustomOperationCount = Mathf.Max(0, pendingCustomOperationCount + delta);
                break;
        }
    }

    private PlayerController GetPlayerController()
    {
        if (GameManager.Instance == null || GameManager.Instance.Player == null)
        {
            return null;
        }

        return GameManager.Instance.Player.Controller;
    }

    protected int PendingAddInputRequestCount => pendingAddInputRequestCount;
    protected int PendingSubInputRequestCount => pendingSubInputRequests;
    protected int PendingAddOutputRequestCount => pendingAddOutputRequestCount;
    protected int PendingSubOutputRequestCount => pendingSubOutputRequestCount;
    protected int PendingCustomOperationCount => pendingCustomOperationCount;

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
