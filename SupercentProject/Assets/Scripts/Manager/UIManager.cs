using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField]
    private Canvas mainCanvas;

    [SerializeField]
    private Queue<NeedBubble> bubbleList;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        var n = mainCanvas.GetComponentsInChildren<NeedBubble>();

        bubbleList = new Queue<NeedBubble>();

        for (int i = 0; i < n.Length; ++i)
            bubbleList.Enqueue(n[i]);
    }
}
