using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;


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
            ChangeMiningTool(1);

        TakeMiningTool(false);
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void ChangeMiningTool(int type)
    {
        if (Enum.IsDefined(typeof(MiningToolType), type))
        {
            MiningToolType eState = (MiningToolType)type;

            switch (eState)
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
    }

    public void TakeMiningTool(bool value)
    {
        for (int i = 0; i < miningToolList.Count; ++i)
            miningToolList[i].gameObject.SetActive(false);

        equipMiningTool.gameObject.SetActive(value);

        body.localPosition = Vector3.zero;

        if (!controller.IsEnterMine)
        {
            animator.SetFloat("fEquip", 0f);
            return;
        }

        float type = 0f;

        switch (GetToolType())
        {
            case MiningToolType.None:
                type = 0f;
                break;
            case MiningToolType.Pickaxe:
                type = 0f;
                break;
            case MiningToolType.Screw:
                type = 0.5f;
                break;
            case MiningToolType.Vehicle:
                type = 1f;
                body.localPosition = new Vector3(0.095f, 0.432f, -0.025f);
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
