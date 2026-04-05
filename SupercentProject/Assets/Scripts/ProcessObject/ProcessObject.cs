using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProcessObject : BaseObject
{
    private const string DefaultInputStackRootName = "Resources_Input";
    private const string DefaultOutputStackRootName = "Resources_Output";

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
    private readonly Queue<PendingResourceOperation> pendingResourceOperations = new Queue<PendingResourceOperation>();
    private int lastProcessedResourceOperationFrame = -1;

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
        PlayerController playerController = GetPlayerController();
        if (playerController == null)
        {
            return;
        }

        if (enterInput || enterOutput)
        {
            if (tickTIme == 0f || tickTIme >= 0.1f)
            {
                if (enterInput
                    && playerController.GetResourceCount(inputResourceType) > 0
                    && CanAddInputResource())
                {
                    AddInputResource();
                }

                if (enterOutput && CanSubOutputResource())
                {
                    SubOutputResource();
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

        if (!EnqueueInputRequest(sourceResource, 0.25f, false, 0f, 0))
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

        if (!CanAddInputResource())
        {
            return false;
        }

        return EnqueueInputRequest(sourceResource, duration, true, jumpPower, jumpCount);
    }

    protected virtual bool CanAddInputResource()
    {
        return GetAvailableQueuedInputCapacity() > 0;
    }

    protected virtual bool CanSubInputResource()
    {
        return inputCount - pendingSubInputRequests > 0;
    }

    protected virtual bool CanAddOutputResource()
    {
        return GetAvailableQueuedOutputCapacity() > 0;
    }

    protected virtual bool CanSubOutputResource()
    {
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

        PortableResource targetResource = inputResourcesStack[nextInputIndex];
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

        int targetIndex = inputCount - 1;
        PortableResource targetResource = inputResourcesStack[targetIndex];
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
        if (outputResourcesStack == null || outputCount >= outputResourcesStack.Count)
        {
            return true;
        }

        PortableResource targetResource = GetNextOutputResource();
        if (targetResource == null)
        {
            return true;
        }

        targetResource.gameObject.SetActive(true);
        ++outputCount;
        return true;
    }

    protected virtual bool TryExecuteQueuedSubOutputResource()
    {
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

        PortableResource sourceResource = GetCurrentOutputResource();
        PortableResource targetResource = playerController.GetPoppedResource(outputResourceType);
        if (sourceResource == null || targetResource == null)
        {
            return true;
        }

        playerController.AddResource(outputResourceType);
        sourceResource.PlayTransferAnimation
        (
            sourceResource.transform.position,
            targetResource.transform.position,
            targetResource.transform.eulerAngles,
            0.25f,
            targetResource.gameObject
        );

        --outputCount;
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
        int totalInputSlotCount = inputResourcesStack != null ? inputResourcesStack.Count : 0;
        if (totalInputSlotCount <= 0)
        {
            return 0;
        }

        EnsureInputSlotStateCache();

        int reservedSlotCount = inputCount;

        for (int i = 0; i < inputSlotsPendingClear.Length; ++i)
        {
            if (inputSlotsPendingClear[i])
            {
                ++reservedSlotCount;
            }

            if (inputSlotsPendingFill[i])
            {
                ++reservedSlotCount;
            }
        }

        return totalInputSlotCount - reservedSlotCount - pendingAddInputRequestCount;
    }

    private int GetAvailableQueuedOutputCount()
    {
        return Mathf.Max(0, outputCount + pendingAddOutputRequestCount - pendingSubOutputRequestCount);
    }

    private int GetAvailableQueuedOutputCapacity()
    {
        int totalOutputSlotCount = outputResourcesStack != null ? outputResourcesStack.Count : 0;
        if (totalOutputSlotCount <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, totalOutputSlotCount - GetAvailableQueuedOutputCount());
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
