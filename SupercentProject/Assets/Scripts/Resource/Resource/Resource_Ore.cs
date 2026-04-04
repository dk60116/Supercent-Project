using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource_Ore : Resource
{
    [SerializeField, ReadOnly]
    private bool alive = true;

    void Start()
    {
        
    }

    void OnEnable()
    {
    }

    protected void Update()
    {
        if (!alive)
        {
            tickTime += Time.deltaTime;

            if (tickTime >= 8f)
                Regeneration();
        }
    }

    public override void GetResource()
    {
        Player player = GameManager.Instance.Player;

        ConsumeOre();

        if (player.Controller.GetResourceCount(ResourceType.Ore) < player.EquipMiningTool.Status.maxOre)
            player.Controller.AddResource(ResourceType.Ore);
    }

    public override void ConsumeResource()
    {
        ConsumeOre();
    }

    public void Regeneration()
    {
        alive = true;
        renderObj.SetActive(true);

        renderObj.transform.localScale = new Vector3(1f, 1f, 0.2f);
        renderObj.transform.DOKill();
        renderObj.transform.DOScaleZ(1f, 0.3f).SetDelay(0.5f).OnComplete(()=>
        {
            colliderTrigger.enabled = true;
        });

        tickTime = 0f;
    }

    private void ConsumeOre()
    {
        colliderTrigger.enabled = false;
        renderObj.SetActive(false);
        alive = false;
        tickTime = 0f;
    }

    public override bool IsAlive => alive;
}
