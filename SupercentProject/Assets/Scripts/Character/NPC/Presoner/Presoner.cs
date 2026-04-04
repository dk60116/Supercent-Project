using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static WaitingLine;

public class Presoner : Character
{
    [SerializeField]
    GameObject waitirngAvata, presonerAvatar;

    [SerializeField, ReadOnly]
    private bool isTarget;
    [SerializeField, ReadOnly]
    private int waitIndex = -1;

    [SerializeField, ReadOnly]
    private int needHanCuffsCount;

    [SerializeField, ReadOnly]
    private bool isArrival;
    [SerializeField, ReadOnly]
    private bool isArrival_Counter;
    [SerializeField, ReadOnly]
    private bool isLeaving;
    [SerializeField, ReadOnly]
    private bool isCompleted;

    [SerializeField, ReadOnly]
    private NeedBubble bubble;

    private RectTransform bubbleRect;
    private RectTransform canvasRect;
    private Camera targetCamera;
    private PresonerSpawner ownerSpawner;

    protected new void Awake()
    {
        base.Awake();

        navAgent.speed = status.moveSpeed;

        ChangeMode(false);
    }

    private void OnEnable()
    {
        EnsureBubble();
        RefreshNeedBubble();
        targetCamera = Camera.main;
    }

    private void OnDisable()
    {
        ReleaseBubble();
    }

    void Update()
    {
        isArrival = isTarget && !isLeaving && navAgent.remainingDistance < 0.1f;
        isArrival_Counter = isArrival && waitIndex == 0;

        Animator.SetBool("bMove", navAgent.remainingDistance > 0.1f);

        if (isLeaving && !navAgent.pathPending && navAgent.remainingDistance < 0.1f)
        {
            isLeaving = false;
        }
    }

    void LateUpdate()
    {
        UpdateBubblePosition();
    }

    private void OnDestroy()
    {
        ReleaseBubble();
    }

    private void UpdateBubblePosition()
    {
        if (bubble == null || bubbleRect == null || canvasRect == null)
        {
            return;
        }

        if (needHanCuffsCount <= 0)
        {
            if (bubble.gameObject.activeSelf)
            {
                bubble.gameObject.SetActive(false);
            }

            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                return;
            }
        }

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);

        if (screenPoint.z <= 0f)
        {
            if (bubble.gameObject.activeSelf)
            {
                bubble.gameObject.SetActive(false);
            }

            return;
        }

        if (!bubble.gameObject.activeSelf)
        {
            bubble.gameObject.SetActive(true);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        bubbleRect.anchoredPosition = localPoint;
    }

    public void ChangeMode(bool presoner)
    {
        waitirngAvata.SetActive(!presoner);
        presonerAvatar.SetActive(presoner);
    }

    public void StartWaiting()
    {
        if (isTarget || isLeaving || isCompleted)
            return;

        FindWaitTargetInfo targetInfo = GameManager.Instance.WaitingLine.FindEmptyPoint();

        if (targetInfo.find)
        {
            AssignWaitPoint(targetInfo.transform, targetInfo.index);
            GameManager.Instance.WaitingLine.SetHasPerson(targetInfo.index, true);
        }
    }

    public bool NeedResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Handcuffs:
                return needHanCuffsCount > 0;
        }

        return false;
    }

    public bool ReceiveResource(ResourceType type, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        switch (type)
        {
            case ResourceType.Handcuffs:
                if (needHanCuffsCount <= 0)
                {
                    return false;
                }

                needHanCuffsCount = Mathf.Max(0, needHanCuffsCount - amount);
                RefreshNeedBubble();

                if (needHanCuffsCount == 0)
                {
                    GameManager.Instance.WaitingLine.ReleasePresoner(this);
                }

                return true;
        }

        return false;
    }

    public void AssignWaitPoint(Transform targetPoint, int index)
    {
        if (targetPoint == null)
        {
            return;
        }

        navAgent.SetDestination(targetPoint.position);
        waitIndex = index;
        isTarget = true;
        isLeaving = false;
        isCompleted = false;
    }

    public void MoveToEndPoint(Transform targetPoint)
    {
        if (targetPoint == null)
        {
            return;
        }

        navAgent.SetDestination(targetPoint.position);
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = true;
        isCompleted = true;
    }

    private void RefreshNeedBubble()
    {
        if (bubble == null)
        {
            return;
        }

        bubble.SetData(null, needHanCuffsCount);
        bubble.gameObject.SetActive(needHanCuffsCount > 0);
    }

    public void PrepareForSpawn(PresonerSpawner spawner, Transform spawnPoint)
    {
        ownerSpawner = spawner;
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = false;
        isCompleted = false;
        needHanCuffsCount = Random.Range(1, 6);

        ChangeMode(false);

        if (spawnPoint != null)
        {
            MoveTo(spawnPoint.position, spawnPoint.rotation);
        }

        EnsureBubble();
        RefreshNeedBubble();
    }

    public void ResetForPool()
    {
        waitIndex = -1;
        isTarget = false;
        isArrival = false;
        isArrival_Counter = false;
        isLeaving = false;
        isCompleted = false;
        ownerSpawner = null;

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.ResetPath();
        }
    }

    private void EnsureBubble()
    {
        if (bubble != null)
        {
            return;
        }

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            return;
        }

        bubble = uiManager.GetBubble();
        if (bubble == null)
        {
            return;
        }

        bubbleRect = bubble.transform as RectTransform;
        canvasRect = uiManager.MainCanvas != null ? uiManager.MainCanvas.transform as RectTransform : null;
    }

    private void ReleaseBubble()
    {
        if (bubble != null)
        {
            bubble.Release();
            bubble = null;
        }

        bubbleRect = null;
        canvasRect = null;
    }

    private void MoveTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.Warp(position);
        }
    }

    public bool IsArrivalCounter => isArrival_Counter;
    public int NeedHanCuffsCount => needHanCuffsCount;
    public int WaitIndex => waitIndex;
    public bool IsLeaving => isLeaving;
    public bool IsCompleted => isCompleted;
}
