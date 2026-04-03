using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Screw : MiningTool
{
    [SerializeField]
    private Transform drill;
    [SerializeField]
    private float rotateSpeed = 1080f;
    [SerializeField]
    private float rotateDuration = 0.5f;

    private float rotateTimer;

    private void Awake()
    {
        type = MiningToolType.Screw; 
    }

    protected void Update()
    {
        if (rotateTimer <= 0f)
        {
            return;
        }

        rotateTimer -= Time.deltaTime;
        drill.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime, Space.Self);
    }

    public void ActivateRotation()
    {
        rotateTimer = rotateDuration;
    }
}
