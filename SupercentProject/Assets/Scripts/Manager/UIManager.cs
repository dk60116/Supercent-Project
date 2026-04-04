using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField]
    private Canvas mainCanvas;

    [SerializeField]
    private Transform bubblePoolRoot;

    [SerializeField]
    private bool preloadSceneBubbles = true;

    private readonly Queue<NeedBubble> bubblePool = new Queue<NeedBubble>();
    private readonly HashSet<NeedBubble> activeBubbles = new HashSet<NeedBubble>();
    private NeedBubble bubbleTemplate;

    public Canvas MainCanvas => mainCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (mainCanvas == null)
        {
            mainCanvas = FindObjectOfType<Canvas>();
        }

        if (mainCanvas == null)
        {
            return;
        }

        NeedBubble[] sceneBubbles = mainCanvas.GetComponentsInChildren<NeedBubble>(true);

        if (sceneBubbles.Length > 0)
        {
            bubbleTemplate = sceneBubbles[0];

            if (bubblePoolRoot == null)
            {
                bubblePoolRoot = bubbleTemplate.transform.parent;
            }
        }

        if (bubblePoolRoot == null)
        {
            bubblePoolRoot = mainCanvas.transform;
        }

        if (!preloadSceneBubbles)
        {
            return;
        }

        for (int i = 0; i < sceneBubbles.Length; ++i)
        {
            RegisterBubble(sceneBubbles[i]);
            ReturnBubbleToPool(sceneBubbles[i]);
        }
    }

    public NeedBubble GetBubble(Transform parent = null)
    {
        NeedBubble bubble = bubblePool.Count > 0 ? bubblePool.Dequeue() : CreateBubbleInstance();

        if (bubble == null)
        {
            return null;
        }

        Transform targetParent = parent != null ? parent : bubblePoolRoot;
        bubble.transform.SetParent(targetParent, false);
        bubble.gameObject.SetActive(true);
        bubble.SetPooled(false);
        activeBubbles.Add(bubble);

        return bubble;
    }

    public void ReleaseBubble(NeedBubble bubble)
    {
        if (bubble == null)
        {
            return;
        }

        if (!activeBubbles.Remove(bubble) && bubble.IsPooled)
        {
            return;
        }

        ReturnBubbleToPool(bubble);
    }

    public void ReleaseAllBubbles()
    {
        if (activeBubbles.Count == 0)
        {
            return;
        }

        NeedBubble[] activeBubbleSnapshot = new NeedBubble[activeBubbles.Count];
        activeBubbles.CopyTo(activeBubbleSnapshot);

        for (int i = 0; i < activeBubbleSnapshot.Length; ++i)
        {
            ReturnBubbleToPool(activeBubbleSnapshot[i]);
        }

        activeBubbles.Clear();
    }

    private NeedBubble CreateBubbleInstance()
    {
        if (bubbleTemplate == null)
        {
            Debug.LogWarning($"{nameof(UIManager)} has no NeedBubble template to expand the pool.");
            return null;
        }

        NeedBubble bubble = Instantiate(bubbleTemplate, bubblePoolRoot);
        RegisterBubble(bubble);
        bubble.gameObject.SetActive(false);
        bubble.SetPooled(true);

        return bubble;
    }

    private void RegisterBubble(NeedBubble bubble)
    {
        if (bubble == null)
        {
            return;
        }

        bubble.BindPool(this);
    }

    private void ReturnBubbleToPool(NeedBubble bubble)
    {
        bubble.ResetBubble();
        bubble.transform.SetParent(bubblePoolRoot, false);
        bubble.gameObject.SetActive(false);
        bubble.SetPooled(true);
        bubblePool.Enqueue(bubble);
    }
}
