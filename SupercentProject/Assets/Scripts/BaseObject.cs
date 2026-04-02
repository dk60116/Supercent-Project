using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseObject : MonoBehaviour
{
    [SerializeField]
    protected string objectName;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public string Player
    {
        get => objectName;
        protected set => objectName = value;
    }
}
