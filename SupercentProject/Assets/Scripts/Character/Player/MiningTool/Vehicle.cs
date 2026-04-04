using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vehicle : MiningTool
{
    [SerializeField]
    private List<Transform> drills;
    [SerializeField]
    private float rotateSpeed = 1080f;
    [SerializeField]
    private float rotateDuration = 0.5f;

    private float rotateTimer;
    private bool wasRotating;

    private void Awake()
    {
        type = MiningToolType.Vehicle;    
    }

    private void Update()
    {
        if (rotateTimer <= 0f)
        {
            if (wasRotating)
            {
                ResetDrillRotation();
                wasRotating = false;
            }

            return;
        }

        rotateTimer -= Time.deltaTime;
        wasRotating = true;

        for (int i = 0; i < drills.Count; ++i)
        {
            if (drills[i] == null)
            {
                continue;
            }

            drills[i].Rotate(Vector3.forward * rotateSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void ActivateRotation()
    {
        rotateTimer = rotateDuration;
    }

    private void ResetDrillRotation()
    {
        for (int i = 0; i < drills.Count; ++i)
        {
            if (drills[i] == null)
            {
                continue;
            }

            drills[i].localEulerAngles = Vector3.right * 90f;
        }
    }
}
