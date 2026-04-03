using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PObj_Factory : ProcessObject
{
    [SerializeField, ReadOnly]
    bool processRunning;

    protected new void Update()
    {
        base.Update();

        processRunning = inputCount > 0;

        animator.SetBool("bRunning", processRunning);
    }

    public bool ProcessRunning => processRunning;
}
