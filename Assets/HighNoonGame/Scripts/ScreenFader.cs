using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] float defaultDuration = 0.45f;
    [SerializeField] Color fadeColor = Color.black;

    CanvasGroup _group;
    Tween _fadeTween;

    public bool IsFading
    {
        get { return _fadeTween != null && _fadeTween.IsActive() && _fadeTween.IsPlaying(); }
    }

    public float Alpha
    {
        get
        {
            EnsureOverlay();
            return _group.alpha;
        }
    }

    public bool IsFullyOpaque
    {
        get { return Alpha >= 0.99f; }
    }

    void Awake()
    {
        EnsureOverlay();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    public void SetAlpha(float alpha)
    {
        EnsureOverlay();

        if (_fadeTween != null && _fadeTween.IsActive())
            _fadeTween.Kill();

        _fadeTween = null;
        _group.alpha = Mathf.Clamp01(alpha);
        _group.blocksRaycasts = _group.alpha > 0.01f;
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        FadeTo(1f, duration, onComplete);
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        FadeTo(0f, duration, onComplete);
    }

    public void FadeTo(float targetAlpha, float duration = -1f, Action onComplete = null)
    {
        EnsureOverlay();

        if (duration < 0f)
            duration = defaultDuration;

        if (_fadeTween != null && _fadeTween.IsActive())
            _fadeTween.Kill();

        _group.blocksRaycasts = true;

        if (Mathf.Abs(_group.alpha - targetAlpha) <= 0.001f)
        {
            _group.alpha = targetAlpha;
            _group.blocksRaycasts = targetAlpha > 0.01f;
            onComplete?.Invoke();
            return;
        }

        if (duration <= 0f)
        {
            _group.alpha = targetAlpha;
            _group.blocksRaycasts = targetAlpha > 0.01f;
            onComplete?.Invoke();
            return;
        }

        _fadeTween = DOTween
            .To(() => _group.alpha, value => _group.alpha = value, targetAlpha, duration)
            .SetUpdate(true)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                _group.blocksRaycasts = targetAlpha > 0.01f;
                onComplete?.Invoke();
            });
    }

    void EnsureOverlay()
    {
        if (_group != null)
            return;

        var canvasGo = new GameObject("ScreenFaderCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("Fade");
        imageGo.transform.SetParent(canvasGo.transform, false);

        var image = imageGo.AddComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _group = imageGo.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }
}
