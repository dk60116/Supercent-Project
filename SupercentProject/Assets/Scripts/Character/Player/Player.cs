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


    protected new void Awake()
    {
        base.Awake();

        controller = GetComponentInChildren<PlayerController>();

        if (miningToolList.Count > 0)
            ChangeMiningTool(MiningToolType.Vehicle);

        TakeMiningTool(false);
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
            case MiningToolType.None:
                break;
            case MiningToolType.Pickaxe:
                equipMiningTool = miningToolList[0];
                break;
            case MiningToolType.Screw:
                equipMiningTool = miningToolList[1];
                break;
            case MiningToolType.Vehicle:
                equipMiningTool = miningToolList[2];
                break;
        }
    }

    public void TakeMiningTool(bool value)
    {
        for (int i = 0; i < miningToolList.Count; ++i)
            miningToolList[i].gameObject.SetActive(false);

        equipMiningTool.gameObject.SetActive(value);

        if (!controller.IsEnterMine)
        {
            animator.SetFloat("fEquip", 0f);
            return;
        }

        int type = 0;

        switch (GetToolType())
        {
            case MiningToolType.None:
                type = 0;
                break;
            case MiningToolType.Pickaxe:
                type = 0;
                break;
            case MiningToolType.Screw:
                type = 1;
                break;
            case MiningToolType.Vehicle:
                type = 2;
                break;
        }

        animator.SetFloat("fEquip", type);
    }

    public MiningToolType GetToolType()
    {
        return equipMiningTool.Type;
    }

    public PlayerController Controller => controller;
    public MiningTool EquipMiningTool => equipMiningTool;
}
