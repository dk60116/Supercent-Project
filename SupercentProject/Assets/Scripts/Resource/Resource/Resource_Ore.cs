using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource_Ore : Resource
{
    void Start()
    {
        
    }

    void OnEnable()
    {
    }

    void Update()
    {
        
    }

    public override void GetResource()
    {
        Player player = GameManager.Instance.Player;

        colliderTrigger.enabled = false;
        renderObj.SetActive(false);

        if (player.Controller.OreCount < player.EquipMiningTool.Status.maxOre)
            player.Controller.AddResource(ResourceType.Ore);
    }
}
