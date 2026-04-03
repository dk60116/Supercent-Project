using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Player : Character
{
    private PlayerController controller;

    [SerializeField]
    private List<MiningTool> miningToolList;
    [SerializeField, ReadOnly]
    private MiningTool equipMiningTool;

    private void Awake()
    {
        controller = GetComponentInChildren<PlayerController>();

        if (miningToolList.Count > 0)
            ChangeMiningTool(MiningToolType.Screw);
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void ChangeMiningTool(MiningToolType type)
    {
        switch (type)
        {
            case MiningToolType.Pickaxe:
                equipMiningTool = miningToolList[0];
                break;
            case MiningToolType.Screw:
                equipMiningTool = miningToolList[1];
                break;
            case MiningToolType.Excavator:
                equipMiningTool = miningToolList[2];
                break;
        }
    }

    public PlayerController Controller => controller;
    public MiningTool EquipMiningTool => equipMiningTool;
}
