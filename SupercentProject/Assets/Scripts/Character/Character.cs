using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public struct CharacterStatus 
{
    public float moveSpeed;
    public float rotationSpeed;

    public CharacterStatus(float moveSpeed, float rotationSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.rotationSpeed = rotationSpeed;
    }
}

public abstract class Character : BaseObject
{
    [SerializeField]
    protected CharacterStatus status;

    [SerializeField]
    protected Transform body;
    [SerializeField]
    protected Animator animator;
    [SerializeField, ReadOnly]
    protected NavMeshAgent navAgent;

    protected void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        
    }

    public CharacterStatus Status => status;
    public Animator Animator => animator;
}
