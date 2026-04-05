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

        if (body == null && transform.childCount > 0)
        {
            body = transform.GetChild(0);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    void Update()
    {
        
    }

    protected bool MoveTowardsPosition(Vector3 destination, float arrivalDistance, float yawOffset = 0f)
    {
        Vector3 currentPosition = transform.position;
        Vector3 toDestination = destination - currentPosition;
        toDestination.y = 0f;

        if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            transform.position = destination;
            return true;
        }

        Vector3 moveDirection = toDestination.normalized;
        transform.position = Vector3.MoveTowards(
            currentPosition,
            destination,
            status.moveSpeed * Time.deltaTime);

        if (moveDirection.sqrMagnitude > 0f)
        {
            RotateTowardsDirection(moveDirection, yawOffset);
        }

        Vector3 remainingDirection = destination - transform.position;
        remainingDirection.y = 0f;
        if (remainingDirection.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            transform.position = destination;
            return true;
        }

        return false;
    }

    protected bool RotateTowardsDirection(Vector3 direction, float yawOffset = 0f, float angleThreshold = 0f)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Quaternion targetRotation = GetPlanarLookRotation(direction, yawOffset);
        if (status.rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return true;
        }

        if (Quaternion.Angle(transform.rotation, targetRotation) <= angleThreshold)
        {
            transform.rotation = targetRotation;
            return true;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            status.rotationSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, targetRotation) <= angleThreshold)
        {
            transform.rotation = targetRotation;
            return true;
        }

        return false;
    }

    protected Quaternion GetPlanarRotation(Quaternion rotation, float yawOffset = 0f)
    {
        Vector3 eulerAngles = rotation.eulerAngles;
        return Quaternion.Euler(0f, eulerAngles.y + yawOffset, 0f);
    }

    protected Quaternion GetPlanarLookRotation(Vector3 direction, float yawOffset = 0f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return transform.rotation;
        }

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return GetPlanarRotation(lookRotation, yawOffset);
    }

    public CharacterStatus Status => status;
    public Animator Animator => animator;
}
