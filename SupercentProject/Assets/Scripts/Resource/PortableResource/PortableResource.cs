using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public abstract class PortableResource : MonoBehaviour
{
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        ResetToOriginTransform();
        transform.localScale = Vector3.zero;

        transform.DOKill();

        var sequence = DOTween.Sequence();

        sequence.Append(transform.DOScale(Vector3.one * 1.5f, 0.2f));
        sequence.Append(transform.DOScale(Vector3.one, 0.1f));
    }

    public void PlayTransferAnimation(Vector3 start, Vector3 target, Vector3 targetRot, float duration, GameObject ableObject)
    {
        PlayTransferAnimation(start, target, targetRot, duration, ableObject, null);
    }

    public void PlayTransferAnimation(Vector3 start, Vector3 target, Vector3 targetRot, float duration, GameObject ableObject, Action onComplete)
    {
        transform.position = start;
        transform.eulerAngles = targetRot;
        transform.DOKill();
        transform.DOMove(target, duration).
            OnComplete(() =>
            {
                ResetToOriginTransform();
                transform.localScale = Vector3.one;
                gameObject.SetActive(false);

                if (ableObject != null)
                {
                    ableObject.SetActive(true);
                }

                onComplete?.Invoke();
            });
    }

    private void ResetToOriginTransform()
    {
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }
}
