using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnterArea_Input : MonoBehaviour
{
    [SerializeField]
    ProcessObject pObject;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pObject.EnterInputAreaEvent();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pObject.OutInputAreaEvent();
        }
    }
}
