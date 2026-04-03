using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnterArea_Output : MonoBehaviour
{
    [SerializeField]
    ProcessObject pObject;
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pObject.EnterOutputAreaEvent();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pObject.OutOutputAreaEvent();
        }
    }
}
