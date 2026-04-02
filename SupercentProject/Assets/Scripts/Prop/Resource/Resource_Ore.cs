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
        colliderTrigger.enabled = false;
        renderObj.SetActive(false);
        GameManager.Instance.Player.Controller.AddOre();
    }
}
