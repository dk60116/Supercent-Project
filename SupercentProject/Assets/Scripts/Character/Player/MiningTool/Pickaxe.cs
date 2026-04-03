using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pickaxe : MiningTool
{
    void Awake()
    {
        type = MiningToolType.Pickaxe;
    }
}
