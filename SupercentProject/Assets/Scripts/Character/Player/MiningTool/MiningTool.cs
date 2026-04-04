using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MiningToolType { None, Pickaxe, Screw, Vehicle }; 

[Serializable]
public struct MiningToolStatus
{
    public int maxOre;
    public float pickingDelay;
    public float rnage;
    public float width;
}

public abstract class MiningTool : BaseObject
{
    [SerializeField]
    protected MiningToolStatus status;
    [SerializeField, ReadOnly]
    protected MiningToolType type;

    public MiningToolStatus Status => status;
    public MiningToolType Type => type;
}
