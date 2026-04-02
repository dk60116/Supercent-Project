using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Resource : BaseObject
{
    protected Collider colliderTrigger;
    [SerializeField]
    protected GameObject renderObj;

    private void Awake()
    {
        colliderTrigger = GetComponent<Collider>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public virtual void GetResource()
    {
    }
}
