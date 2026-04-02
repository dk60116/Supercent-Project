using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class JoyStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField]
    private Image frame;
    [SerializeField]
    private Image stick;
    [SerializeField]
    private float handleRangeMultiplier = 1.35f;

    public static JoyStick Instance { get; private set; }

    public Vector2 InputVector => inputVector;

    private RectTransform rootRect;
    private RectTransform frameRect;
    private RectTransform stickRect;
    private Vector2 inputVector;
    private Vector2 centerPosition;
    private float moveRadius;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rootRect = transform as RectTransform;
        frameRect = frame.rectTransform;
        stickRect = stick.rectTransform;

        float frameRadius = Mathf.Min(frameRect.rect.width, frameRect.rect.height) * 0.5f;
        float stickRadius = Mathf.Min(stickRect.rect.width, stickRect.rect.height) * 0.5f;
        moveRadius = Mathf.Max(0f, frameRadius - stickRadius) * handleRangeMultiplier;
        ResetStick();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out centerPosition))
        {
            return;
        }

        frameRect.anchoredPosition = centerPosition;
        stickRect.anchoredPosition = centerPosition;
        SetVisible(true);
        UpdateStick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateStick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetStick();
        SetVisible(false);
    }

    private void UpdateStick(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            return;
        }

        float maxRadius = Mathf.Min(frameRect.rect.width, frameRect.rect.height) * 0.5f;
        Vector2 delta = localPoint - centerPosition;
        inputVector = Vector2.ClampMagnitude(delta / maxRadius, 1f);
        stickRect.anchoredPosition = centerPosition + inputVector * moveRadius;
    }

    private void ResetStick()
    {
        inputVector = Vector2.zero;
        centerPosition = Vector2.zero;
        frameRect.anchoredPosition = Vector2.zero;
        stickRect.anchoredPosition = Vector2.zero;
    }

    private void SetVisible(bool isVisible)
    {
        frame.enabled = isVisible;
        stick.enabled = isVisible;
    }
}
