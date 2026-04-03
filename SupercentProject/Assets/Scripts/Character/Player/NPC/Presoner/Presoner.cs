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

    protected new void Awake()
    {
        base.Awake();

        needHanCuffsCount = Random.Range(1, 6);

        navAgent.speed = status.moveSpeed;

        ChangeMode(false);
    }

    void Start()
    {
    }

    void Update()
    {
        isArrival = isTarget && navAgent.remainingDistance < 0.1f;

        Animator.SetBool("bMove", navAgent.remainingDistance > 0.1f);
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
}
