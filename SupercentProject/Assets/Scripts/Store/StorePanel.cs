using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class StorePanel : BaseObject
{
    private const float DefaultAbsorbInterval = 0.1f;
    private const float DefaultTransferDuration = 0.1f;

    [SerializeField]
    private int price;
    private int originPrice;

    [SerializeField]
    private Transform fill;
    [SerializeField]
    private SpriteRenderer icon;
    [SerializeField]
    private TextMeshPro moneyText;

    [SerializeField, ReadOnly]
    private bool isEnter;
    [SerializeField]
    private float absorbInterval = DefaultAbsorbInterval;
    [SerializeField]
    private float transferDuration = DefaultTransferDuration;

    [SerializeField]
    private List<UnityEvent> onCompleteEvent;

    private float absorbTickTime;
    private bool isCompleted;

    private void Awake()
    {
        originPrice = price;

        RefreshPriceText();

        fill.transform.localScale = new Vector3(1f, 0f, 1f);
    }

    private void Update()
    {
        if (!isEnter)
        {
            absorbTickTime = 0f;
            return;
        }

        if (price <= 0)
        {
            CompleteStore();
            absorbTickTime = 0f;
            return;
        }

        Player player = GetTriggerPlayer();
        if (player == null || player.Controller == null || player.Controller.GetResourceCount(ResourceType.Money) <= 0)
        {
            absorbTickTime = 0f;
            return;
        }

        if (absorbTickTime == 0f || absorbTickTime >= absorbInterval)
        {
            AbsorbPlayerMoney(player);
            absorbTickTime = 0f;
        }

        absorbTickTime += Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        isEnter = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        isEnter = false;
        absorbTickTime = 0f;
    }

    private void AbsorbPlayerMoney(Player player)
    {
        if (price <= 0)
        {
            CompleteStore();
            return;
        }

        PlayerController controller = player.Controller;
        controller.SubResrouce(ResourceType.Money);

        PortableResource topMoney = controller.GetPoppedResource(ResourceType.Money);
        if (topMoney != null)
        {
            topMoney.PlayTransferAnimation
                (
                topMoney.transform.position,
                transform.position,
                transform.eulerAngles,
                transferDuration,
                null
                );
        }

        price = Mathf.Max(0, price - 1);
        RefreshPriceText();

        fill.transform.localScale = new Vector3(1f, 1f - (price / (float)originPrice), 1f);

        if (price <= 0)
        {
            CompleteStore();
        }
    }

    private void RefreshPriceText()
    {
        if (moneyText != null)
        {
            moneyText.text = price.ToString();
        }
    }

    private void CompleteStore()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;

        for (int i = 0; i < onCompleteEvent.Count; ++i)
        {
            onCompleteEvent[i]?.Invoke();
        }

        gameObject.SetActive(false);
    }

    private Player GetTriggerPlayer()
    {
        return GameManager.Instance != null ? GameManager.Instance.Player : null;
    }

    private bool IsPlayerCollider(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        return player != null && player == GetTriggerPlayer();
    }
}
