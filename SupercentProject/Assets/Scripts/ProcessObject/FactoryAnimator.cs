using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FactoryAnimator : MonoBehaviour
{
    [SerializeField]
    PObj_Factory factory;

    private void StartProcess()
    {
        if (factory.ProcessRunning)
            factory.SubInputResource();
    }
}
