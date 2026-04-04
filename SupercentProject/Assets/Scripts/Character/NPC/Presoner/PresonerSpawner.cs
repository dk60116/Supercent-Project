using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PresonerSpawner : MonoBehaviour
{
    [SerializeField]
    private GameObject presonerAsset;
    [SerializeField]
    private Transform poolRoot;
    [SerializeField]
    private int initialPoolSize = 5;
    [SerializeField]
    private float spawnInterval = 0.5f;

    [SerializeField, ReadOnly]
    private int pooledCount;
    [SerializeField, ReadOnly]
    private int activeCount;

    private readonly Queue<Presoner> pooledPresoners = new Queue<Presoner>();
    private readonly HashSet<Presoner> activePresoners = new HashSet<Presoner>();
    private WaitingLine waitingLine;
    private float spawnTickTime;

    private void Awake()
    {
        waitingLine = GameManager.Instance != null ? GameManager.Instance.WaitingLine : FindObjectOfType<WaitingLine>();

        if (poolRoot == null)
        {
            poolRoot = transform;
        }
    }

    void Start()
    {
        if (waitingLine == null || presonerAsset == null)
        {
            return;
        }

        int warmCount = Mathf.Max(initialPoolSize, waitingLine.WaitingPointCount);
        for (int i = 0; i < warmCount; ++i)
        {
            Presoner presoner = CreatePresonerInstance();
            if (presoner == null)
            {
                continue;
            }

            pooledPresoners.Enqueue(presoner);
        }

        FillWaitingLine();
    }

    void Update()
    {
        if (waitingLine == null || presonerAsset == null || !waitingLine.HasAvailableWaitPoint())
        {
            spawnTickTime = 0f;
            return;
        }

        if (spawnInterval <= 0f)
        {
            TrySpawnPresoner();
            return;
        }

        spawnTickTime += Time.deltaTime;
        if (spawnTickTime < spawnInterval)
        {
            return;
        }

        spawnTickTime = 0f;
        TrySpawnPresoner();
    }

    public void ReturnToPool(Presoner presoner)
    {
        if (presoner == null)
        {
            return;
        }

        activePresoners.Remove(presoner);
        waitingLine?.UnregisterPresoner(presoner);

        presoner.ResetForPool();
        presoner.transform.SetParent(poolRoot, true);
        presoner.gameObject.SetActive(false);
        pooledPresoners.Enqueue(presoner);

        RefreshDebugCounts();
    }

    private void FillWaitingLine()
    {
        while (waitingLine != null && waitingLine.HasAvailableWaitPoint())
        {
            if (!TrySpawnPresoner())
            {
                break;
            }
        }
    }

    private bool TrySpawnPresoner()
    {
        if (waitingLine == null || !waitingLine.HasAvailableWaitPoint())
        {
            return false;
        }

        Presoner presoner = GetPooledPresoner();
        if (presoner == null)
        {
            return false;
        }

        presoner.transform.SetParent(null, true);
        presoner.gameObject.SetActive(true);
        presoner.PrepareForSpawn(this, waitingLine.StartPoint);

        waitingLine.RegisterPresoner(presoner);
        if (!waitingLine.TryAssignWaitPoint(presoner))
        {
            ReturnToPool(presoner);
            return false;
        }

        activePresoners.Add(presoner);
        RefreshDebugCounts();
        return true;
    }

    private Presoner GetPooledPresoner()
    {
        if (pooledPresoners.Count > 0)
        {
            return pooledPresoners.Dequeue();
        }

        return CreatePresonerInstance();
    }

    private Presoner CreatePresonerInstance()
    {
        if (presonerAsset == null)
        {
            return null;
        }

        GameObject spawnedObject = Instantiate(presonerAsset, poolRoot);
        Presoner presoner = spawnedObject.GetComponent<Presoner>();
        if (presoner == null)
        {
            Destroy(spawnedObject);
            return null;
        }

        spawnedObject.SetActive(false);
        RefreshDebugCounts();

        return presoner;
    }

    private void RefreshDebugCounts()
    {
        pooledCount = pooledPresoners.Count;
        activeCount = activePresoners.Count;
    }
}
