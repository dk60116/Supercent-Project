using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceType { Ore, Handcuffs, Money }

public abstract class Resource : BaseObject
{
    protected Collider colliderTrigger;
    [SerializeField]
    protected GameObject renderObj;

    protected float tickTime;

    private void Awake()
    {
        colliderTrigger = GetComponent<Collider>();
    }
    public virtual void GetResource()
    {
    }

    public virtual void ConsumeResource()
    {
    }

    public virtual bool IsAlive => true;
}
