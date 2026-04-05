using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField]
    Camera mainCam;
    [SerializeField]
    private Canvas mainCanvas;

    [SerializeField]
    private Transform bubblePoolRoot;

    [SerializeField]
    TextMeshProUGUI moneyCount;

    [SerializeField]
    private bool preloadSceneBubbles = true;

    private readonly Queue<NeedBubble> bubblePool = new Queue<NeedBubble>();
    private readonly HashSet<NeedBubble> activeBubbles = new HashSet<NeedBubble>();
    private NeedBubble bubbleTemplate;
    private int displayedMoneyCount = int.MinValue;

    [SerializeField]
    private Image fadeImage;

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

    private void OnEnable()
    {
        displayedMoneyCount = int.MinValue;
        RefreshMoneyCount(true);
    }

    private void LateUpdate()
    {
        RefreshMoneyCount(false);
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

    private void RefreshMoneyCount(bool force)
    {
        if (moneyCount == null)
        {
            return;
        }

        int currentMoneyCount = GetCurrentMoneyCount();
        if (!force && displayedMoneyCount == currentMoneyCount)
        {
            return;
        }

        displayedMoneyCount = currentMoneyCount;
        moneyCount.text = currentMoneyCount.ToString();
    }

    private int GetCurrentMoneyCount()
    {
        if (GameManager.Instance == null || GameManager.Instance.Player == null || GameManager.Instance.Player.Controller == null)
        {
            return 0;
        }

        return GameManager.Instance.Player.Controller.MoneyCount;
    }

    public void Ending()
    {
        Camera.main.DOOrthoSize(7.5f, 1f);

        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.gameObject.SetActive(true);

        fadeImage.DOFade(1f, 2f).OnComplete(() => fadeImage.transform.GetChild(0).gameObject.SetActive(true)).SetDelay(3f);
    }
}

