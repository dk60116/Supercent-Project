using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaitingLine : MonoBehaviour
{
    public struct FindWaitTargetInfo
    {
        public bool find;
        public Transform transform;
        public int index;
    }

    [SerializeField]
    private Transform startPoint;
    [SerializeField]
    private List<Transform> waitPoints;
    [SerializeField]
    private Transform endPoint, endPoint2, endPoint3;
    [SerializeField, ReadOnly]
    private List<bool> hasPersonList;

    [SerializeField]
    private List<Presoner> presoners;
    [SerializeField, ReadOnly]
    private List<Presoner> pendingReleasePresoners;

    void Start()
    {
        
    }

    void Update()
    {
        EnsurePendingReleaseList();
        RemoveInvalidPendingReleasePresoners();

        for (int i = 0; i < presoners.Count; ++i)
        {
            Presoner presoner = presoners[i];
            if (presoner == null)
            {
                continue;
            }

            presoner.StartWaiting();
        }

        TryProcessPendingRelease();
    }

    public FindWaitTargetInfo FindEmptyPoint()
    {
        FindWaitTargetInfo info = new FindWaitTargetInfo();

        info.find = false;
        info.transform = null;
        info.index = -1;

        for (int i = 0; i < hasPersonList.Count; ++i)
        {
            if (!hasPersonList[i])
            {
                info.find = true;
                info.transform = waitPoints[i].transform;
                info.index = i;

                return info;
            }
        }

        return info;
    }

    public void SetHasPerson(int index, bool value)
    {
        hasPersonList[index] = value;
    }

    public void RegisterPresoner(Presoner presoner)
    {
        if (presoner == null || presoners.Contains(presoner))
        {
            return;
        }

        presoners.Add(presoner);
    }

    public void UnregisterPresoner(Presoner presoner)
    {
        if (presoner == null)
        {
            return;
        }

        presoners.Remove(presoner);
        pendingReleasePresoners?.Remove(presoner);
    }

    public bool TryAssignWaitPoint(Presoner presoner)
    {
        if (presoner == null)
        {
            return false;
        }

        FindWaitTargetInfo targetInfo = FindEmptyPoint();
        if (!targetInfo.find)
        {
            return false;
        }

        presoner.AssignWaitPoint(targetInfo.transform, targetInfo.index);
        SetHasPerson(targetInfo.index, true);
        return true;
    }

    public bool HasAvailableWaitPoint()
    {
        return FindEmptyPoint().find;
    }

    public void ReleasePresoner(Presoner targetPresoner)
    {
        if (targetPresoner == null)
        {
            return;
        }

        EnsurePendingReleaseList();

        if (!pendingReleasePresoners.Contains(targetPresoner))
        {
            pendingReleasePresoners.Add(targetPresoner);
        }

        TryProcessPendingRelease();
    }

    public Presoner GetCounterPresoner()
    {
        for (int i = 0; i < presoners.Count; ++i)
        {
            if (presoners[i] != null && presoners[i].IsArrivalCounter)
            {
                return presoners[i];
            }
        }

        return null;
    }

    public bool HasPresonerMovingToJail()
    {
        for (int i = 0; i < presoners.Count; ++i)
        {
            Presoner presoner = presoners[i];
            if (presoner != null && presoner.IsMovingToJail)
            {
                return true;
            }
        }

        return false;
    }

    public Transform StartPoint => startPoint;
    public int WaitingPointCount => waitPoints.Count;

    private void TryProcessPendingRelease()
    {
        EnsurePendingReleaseList();
        RemoveInvalidPendingReleasePresoners();

        if (pendingReleasePresoners.Count <= 0)
        {
            return;
        }

        int maxReleaseCount = pendingReleasePresoners.Count;
        for (int i = 0; i < maxReleaseCount; ++i)
        {
            Presoner targetPresoner = FindNextCounterReleasePresoner();
            if (targetPresoner == null)
            {
                return;
            }

            if (ShouldHoldReleaseAtCounter())
            {
                return;
            }

            BeginReleasePresoner(targetPresoner);
            pendingReleasePresoners.Remove(targetPresoner);
        }
    }

    private void BeginReleasePresoner(Presoner targetPresoner)
    {
        if (targetPresoner == null)
        {
            return;
        }

        targetPresoner.ChangeMode(true);

        int releasedIndex = targetPresoner.WaitIndex;
        if (releasedIndex < 0 || releasedIndex >= hasPersonList.Count)
        {
            targetPresoner.MoveToEndPoint(endPoint, endPoint2, endPoint3);
            return;
        }

        hasPersonList[releasedIndex] = false;

        for (int index = releasedIndex + 1; index < waitPoints.Count; ++index)
        {
            Presoner nextPresoner = GetPresonerByWaitIndex(index);
            if (nextPresoner == null || nextPresoner.IsLeaving || nextPresoner.IsCompleted)
            {
                continue;
            }

            nextPresoner.AssignWaitPoint(waitPoints[index - 1], index - 1);
            hasPersonList[index - 1] = true;
            hasPersonList[index] = false;
        }

        targetPresoner.MoveToEndPoint(endPoint, endPoint2, endPoint3);
    }

    private void EnsurePendingReleaseList()
    {
        if (pendingReleasePresoners == null)
        {
            pendingReleasePresoners = new List<Presoner>();
        }
    }

    private void RemoveInvalidPendingReleasePresoners()
    {
        if (pendingReleasePresoners == null)
        {
            return;
        }

        pendingReleasePresoners.RemoveAll(presoner =>
            presoner == null
            || !presoners.Contains(presoner)
            || presoner.IsLeaving);
    }

    private Presoner FindNextCounterReleasePresoner()
    {
        for (int i = 0; i < pendingReleasePresoners.Count; ++i)
        {
            Presoner presoner = pendingReleasePresoners[i];
            if (presoner != null && presoner.WaitIndex == 0)
            {
                return presoner;
            }
        }

        return null;
    }

    private Presoner GetPresonerByWaitIndex(int waitIndex)
    {
        for (int i = 0; i < presoners.Count; ++i)
        {
            Presoner presoner = presoners[i];
            if (presoner != null && presoner.WaitIndex == waitIndex)
            {
                return presoner;
            }
        }

        return null;
    }

    private bool ShouldHoldReleaseAtCounter()
    {
        Jail jail = FindObjectOfType<Jail>();
        if (jail == null || !jail.IsAtCapacity())
        {
            return false;
        }

        for (int i = 0; i < presoners.Count; ++i)
        {
            Presoner presoner = presoners[i];
            if (presoner != null && presoner.IsWaitingForJailEntry)
            {
                return true;
            }
        }

        return false;
    }
}
