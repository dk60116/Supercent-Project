using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    protected Animator animator;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public CharacterStatus Status => status;
    public Animator Animator => animator;
}
