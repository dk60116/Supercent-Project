using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Jail : BaseObject
{
    [SerializeField]
    private Transform door;

    [SerializeField, ReadOnly]
    private List<Presoner> imprisonedList;

    [SerializeField, ReadOnly]
    private List<Presoner> approachingPresoners;
    [SerializeField, ReadOnly]
    private List<Presoner> reservedPresoners;

    [SerializeField]
    private int maxCount;

    [SerializeField]
    private TextMeshPro countText;

    [SerializeField, ReadOnly]
    private bool isDoorOpen;

    [SerializeField]
    private StorePanel expandStore;

    [SerializeField]
    private GameObject unexpand, expand;

    private void Awake()
    {
        EnsureReferences();
        RefreshCountText();
        RefreshDoorState(true);
    }

    private void OnEnable()
    {
        EnsureReferences();
        RefreshCountText();
        RefreshDoorState(true);
    }

    private void OnValidate()
    {
        EnsureReferences();
        RefreshCountText();
    }

    public void OpenDoor()
    {
        isDoorOpen = true;
        door.DOKill();
        door.DOMoveY(-2.3f, 0.5f);
    }

    public void CloseDoor()
    {
        isDoorOpen = false;
        door.DOKill();
        door.DOMoveY(0f, 0.5f);
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

        approachingPresoners?.Remove(presoner);
        reservedPresoners?.Remove(presoner);
        RefreshCountText();
        RefreshDoorState();
    }

    public bool CanPresonerAdvanceToExitPoint(Presoner presoner)
    {
        if (presoner == null)
        {
            return false;
        }

        EnsureReferences();
        RemoveInvalidPresoners();
        approachingPresoners.RemoveAll(target => target == null);

        return approachingPresoners.Count > 0 && approachingPresoners[0] == presoner;
    }

    public bool CanPresonerEnter(Presoner presoner)
    {
        if (presoner == null)
        {
            return false;
        }

        EnsureReferences();
        RemoveInvalidPresoners();
        approachingPresoners.RemoveAll(target => target == null);

        if (reservedPresoners.Contains(presoner))
        {
            return true;
        }

        if (maxCount <= 0)
        {
            return true;
        }

        return GetReservedJailCount() < maxCount;
    }

    public bool TryReservePresonerEntry(Presoner presoner)
    {
        if (presoner == null)
        {
            return false;
        }

        EnsureReferences();
        RemoveInvalidPresoners();

        if (reservedPresoners.Contains(presoner))
        {
            return true;
        }

        if (!CanPresonerEnter(presoner))
        {
            return false;
        }

        reservedPresoners.Add(presoner);
        RefreshCountText();
        return true;
    }

    public bool HasReservedPresonerEntry(Presoner presoner)
    {
        if (presoner == null)
        {
            return false;
        }

        EnsureReferences();
        RemoveInvalidPresoners();
        return reservedPresoners.Contains(presoner);
    }

    public bool IsAtCapacity()
    {
        EnsureReferences();
        RemoveInvalidPresoners();

        if (maxCount <= 0)
        {
            return false;
        }

        return GetReservedJailCount() >= maxCount;
    }

    public void NotifyPresonerApproaching(Presoner presoner)
    {
        if (presoner == null)
        {
            return;
        }

        EnsureReferences();

        if (!approachingPresoners.Contains(presoner))
        {
            approachingPresoners.Add(presoner);
        }

        RefreshDoorState();
    }

    public void NotifyPresonerApproachCancelled(Presoner presoner)
    {
        if (presoner == null || approachingPresoners == null)
        {
            return;
        }

        bool removedApproach = approachingPresoners.Remove(presoner);
        bool removedReserved = reservedPresoners != null && reservedPresoners.Remove(presoner);

        if (removedApproach || removedReserved)
        {
            RefreshCountText();
            RefreshDoorState();
        }
    }

    private void EnsureReferences()
    {
        if (imprisonedList == null)
        {
            imprisonedList = new List<Presoner>();
        }

        if (approachingPresoners == null)
        {
            approachingPresoners = new List<Presoner>();
        }

        if (reservedPresoners == null)
        {
            reservedPresoners = new List<Presoner>();
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

        int currentCount = GetReservedJailCount();
        int displayMaxCount = maxCount > 0 ? maxCount : currentCount;
        countText.text = $"{currentCount}/{displayMaxCount}";

        if (currentCount >= maxCount)
        {
            countText.color = Color.red;

            expandStore.gameObject.SetActive(true);
        }
    }

    private void RemoveInvalidPresoners()
    {
        if (imprisonedList == null)
        {
            return;
        }

        imprisonedList.RemoveAll(presoner => presoner == null);
        approachingPresoners?.RemoveAll(presoner => presoner == null);
        reservedPresoners?.RemoveAll(presoner => presoner == null);
    }

    private int GetReservedJailCount()
    {
        EnsureReferences();
        return imprisonedList.Count + (reservedPresoners != null ? reservedPresoners.Count : 0);
    }

    private void RefreshDoorState(bool force = false)
    {
        EnsureReferences();
        approachingPresoners.RemoveAll(presoner => presoner == null);
        bool desiredDoorOpen = approachingPresoners.Count > 0;

        if (!force && isDoorOpen == desiredDoorOpen)
        {
            return;
        }

        if (desiredDoorOpen)
        {
            OpenDoor();
        }
        else
        {
            CloseDoor();
        }
    }

    public void Expand()
    {
        unexpand.SetActive(false);
        expand.SetActive(true);
    }
}
