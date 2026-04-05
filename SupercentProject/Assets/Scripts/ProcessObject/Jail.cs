using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Jail : BaseObject
{
    [SerializeField, ReadOnly]
    private List<Presoner> imprisonedList;

    [SerializeField]
    private int maxCount;

    [SerializeField]
    private TextMeshPro countText;

    private void Awake()
    {
        EnsureReferences();
        RefreshCountText();
    }

    private void OnEnable()
    {
        EnsureReferences();
        RefreshCountText();
    }

    private void OnValidate()
    {
        EnsureReferences();
        RefreshCountText();
    }

    public void RegisterPresoner(Presoner presoner)
    {
        if (presoner == null)
        {
            return;
        }

        if (imprisonedList == null)
        {
            imprisonedList = new List<Presoner>();
        }

        if (!imprisonedList.Contains(presoner))
        {
            imprisonedList.Add(presoner);
        }

        RefreshCountText();
    }

    private void EnsureReferences()
    {
        if (imprisonedList == null)
        {
            imprisonedList = new List<Presoner>();
        }

        if (countText == null)
        {
            countText = GetComponentInChildren<TextMeshPro>(true);
        }
    }

    private void RefreshCountText()
    {
        EnsureReferences();
        RemoveInvalidPresoners();

        if (countText == null)
        {
            return;
        }

        int currentCount = imprisonedList.Count;
        int displayMaxCount = maxCount > 0 ? maxCount : currentCount;
        countText.text = $"{currentCount}/{displayMaxCount}";
    }

    private void RemoveInvalidPresoners()
    {
        if (imprisonedList == null)
        {
            return;
        }

        imprisonedList.RemoveAll(presoner => presoner == null);
    }
}
