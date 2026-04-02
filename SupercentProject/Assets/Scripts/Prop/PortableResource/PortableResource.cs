using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PortableResource : MonoBehaviour
{
    void OnEnable()
    {
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.3f);
    }
    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
