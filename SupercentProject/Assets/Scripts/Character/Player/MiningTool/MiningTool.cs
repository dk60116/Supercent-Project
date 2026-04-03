using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MiningToolType { Pickaxe, Screw, Excavator }; 

[Serializable]
public struct MiningToolStatus
{
    public int maxOre;
    public float pickingDelay;
}

public abstract class MiningTool : BaseObject
{
    [SerializeField]
    private MiningToolStatus status;

    public MiningToolStatus Status => status;
}
