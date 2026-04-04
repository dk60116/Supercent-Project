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
    [SerializeField, ReadOnly]
    private List<bool> hasPersonList;

    [SerializeField]
    private List<Presoner> presoners;

    void Start()
    {
        
    }

    void Update()
    {
        for (int i = 0; i < presoners.Count; ++i)
        {
            presoners[i].StartWaiting();
        }
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
}
