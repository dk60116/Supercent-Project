using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Player : Character
{
    private PlayerController controller;

    [SerializeField]
    private List<MiningTool> miningToolList;
    [SerializeField]
    private MiningTool equipMiningTool;

    private void Awake()
    {
        controller = GetComponentInChildren<PlayerController>();

        if (miningToolList.Count > 0)
            ChangeMiningTool(0);
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void ChangeMiningTool(int idx)
    {
        equipMiningTool = miningToolList[idx];
    }

    public PlayerController Controller => controller;
    public MiningTool EquipMiningTool => equipMiningTool;
}
