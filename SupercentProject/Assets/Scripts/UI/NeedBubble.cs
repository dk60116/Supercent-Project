using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NeedBubble : MonoBehaviour
{
    [SerializeField]
    private Image icon;
    [SerializeField]
    public TextMeshProUGUI countTxt;

    private UIManager owner;

    public bool IsPooled { get; private set; }

    public void BindPool(UIManager poolOwner)
    {
        owner = poolOwner;
    }

    public void SetData(string count)
    {
        if (countTxt != null)
        {
            countTxt.text = count;
        }
    }

    public void SetData(Sprite bubbleIcon, int count)
    {
        SetData(count.ToString());
    }

    public void ResetBubble()
    {
        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (countTxt != null)
        {
            countTxt.text = string.Empty;
        }
    }

    public void Release()
    {
        if (owner != null)
        {
            owner.ReleaseBubble(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void SetPooled(bool pooled)
    {
        IsPooled = pooled;
    }
}
