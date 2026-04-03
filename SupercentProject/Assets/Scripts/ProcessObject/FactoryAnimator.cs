using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FactoryAnimator : MonoBehaviour
{
    [SerializeField]
    PObj_Factory factory;

    private bool consumedInputThisCycle;

    private void StartProcess()
    {
        consumedInputThisCycle = false;

        if (factory != null && factory.InputCount > 0)
        {
            factory.SubInputResource();
            consumedInputThisCycle = true;
        }
    }

    private void EndProcess()
    {
        if (factory == null || !consumedInputThisCycle)
        {
            return;
        }

        factory.AddOutputResource();
        consumedInputThisCycle = false;
    }
}
