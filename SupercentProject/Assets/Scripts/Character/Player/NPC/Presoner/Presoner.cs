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
    private NeedBubble bubble;

    private RectTransform bubbleRect;
    private RectTransform canvasRect;
    private Camera targetCamera;

    protected new void Awake()
    {
        base.Awake();

        needHanCuffsCount = Random.Range(1, 6);

        navAgent.speed = status.moveSpeed;

        ChangeMode(false);
    }

    void Start()
    {
        UIManager uiManager = UIManager.Instance;

        bubble = uiManager.GetBubble();

        bubble.SetData(null, needHanCuffsCount);
        bubbleRect = bubble.transform as RectTransform;
        canvasRect = uiManager.MainCanvas != null ? uiManager.MainCanvas.transform as RectTransform : null;
        targetCamera = Camera.main;
    }

    void Update()
    {
        isArrival = isTarget && navAgent.remainingDistance < 0.1f;
        isArrival_Counter = isArrival && waitIndex == 0;

        Animator.SetBool("bMove", navAgent.remainingDistance > 0.1f);
    }

    void LateUpdate()
    {
        UpdateBubblePosition();
    }

    private void OnDestroy()
    {
        if (bubble != null)
        {
            bubble.Release();
            bubble = null;
        }
    }

    private void UpdateBubblePosition()
    {
        if (bubble == null || bubbleRect == null || canvasRect == null)
        {
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
        if (isTarget)
            return;

        FindWaitTargetInfo targetInfo = GameManager.Instance.WaitingLine.FindEmptyPoint();

        if (targetInfo.find)
        {
            navAgent.SetDestination(targetInfo.transform.position);
            GameManager.Instance.WaitingLine.SetHasPerson(targetInfo.index, true);
            waitIndex = targetInfo.index;
            isTarget = true;
        }
    }

    public bool IsArrivalCounter => isArrival_Counter;
}
