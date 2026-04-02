using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField, ReadOnly]
    private BaseObject target;

    [SerializeField]
    Vector3 targetRotation;
    [SerializeField]
    private float downwardViewOffset = -0.5f;

    private Quaternion fixedRotation;
    private float followDistance;

    private void Awake()
    {
        fixedRotation = transform.rotation;
        AssignTarget();
    }

    private void Start()
    {
        if (target != null)
        {
            UpdateFollowDistance();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            AssignTarget();

            if (target == null)
            {
                return;
            }

            UpdateFollowDistance();
        }

        transform.rotation = Quaternion.Euler(targetRotation);
        transform.position = target.transform.position - (transform.forward * followDistance) - (transform.up * downwardViewOffset);
    }

    private void AssignTarget()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        target = GameManager.Instance.Player;
    }

    private void UpdateFollowDistance()
    {
        Vector3 targetToCamera = transform.position - target.transform.position;
        followDistance = Vector3.Dot(targetToCamera, -transform.forward);

        if (followDistance <= 0f)
        {
            followDistance = targetToCamera.magnitude;
        }
    }
}
