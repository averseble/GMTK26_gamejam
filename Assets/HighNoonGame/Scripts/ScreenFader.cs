using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] float defaultDuration = 0.45f;
    [SerializeField] Color fadeColor = Color.black;
    [SerializeField] float messageFontSize = 28f;
    [SerializeField] float gameNameFontSize = 34f;
    [SerializeField] float creditsFontSize = 18f;
    [SerializeField] Color messageColor = Color.white;
    [SerializeField] Color creditsColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] float lineSpacing = 2f;
    [SerializeField] float lineHeightMul = 1.05f;

    CanvasGroup _group;
    RectTransform _messageRoot;
    readonly List<CanvasGroup> _lineGroups = new List<CanvasGroup>();
    Tween _fadeTween;
    Tween _messageTween;

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
        ClearMessage();
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

    public void PrepareMessageLines(
        string title,
        string gameName,
        string credits,
        TMP_FontAsset titleFont = null,
        TMP_FontAsset gameNameFont = null)
    {
        EnsureOverlay();
        EnsureMessageRoot();
        ClearLineGroups();

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.55f, 1f);
        float titleSize = messageFontSize * scale;
        float nameSize = gameNameFontSize * scale;
        float creditSize = creditsFontSize * scale;
        float spacing = lineSpacing * scale;

        if (_messageRoot != null)
        {
            _messageRoot.sizeDelta = new Vector2(Mathf.Min(Screen.width * 0.86f, 720f * scale), 0f);
            var layout = _messageRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.spacing = spacing;
        }

        if (!string.IsNullOrWhiteSpace(title))
            AddLine(title.Trim(), titleSize, messageColor, titleFont);

        if (!string.IsNullOrWhiteSpace(gameName))
            AddLine(gameName.Trim(), nameSize, messageColor, gameNameFont);

        if (!string.IsNullOrWhiteSpace(credits))
        {
            string[] parts = credits.Split(new[] { '\n' }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string line = parts[i].Trim();
                if (line.Length == 0)
                    continue;

                AddLine(line, creditSize, creditsColor, titleFont);
            }
        }

        if (_messageRoot != null)
        {
            _messageRoot.gameObject.SetActive(_lineGroups.Count > 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_messageRoot);
        }
    }

    public IEnumerator FadeInLinesSequential(float duration = 0.8f, float delayBetween = 0.35f)
    {
        if (_lineGroups.Count == 0)
            yield break;

        if (duration < 0f)
            duration = 0f;

        if (delayBetween < 0f)
            delayBetween = 0f;

        for (int i = 0; i < _lineGroups.Count; i++)
        {
            CanvasGroup lineGroup = _lineGroups[i];
            if (lineGroup == null)
                continue;

            if (_messageTween != null && _messageTween.IsActive())
                _messageTween.Kill();

            bool done = false;
            if (duration <= 0f)
            {
                lineGroup.alpha = 1f;
                done = true;
            }
            else
            {
                lineGroup.alpha = 0f;
                _messageTween = lineGroup
                    .DOFade(1f, duration)
                    .SetUpdate(true)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(() => done = true);
            }

            while (!done)
                yield return null;

            if (delayBetween > 0f && i < _lineGroups.Count - 1)
                yield return new WaitForSecondsRealtime(delayBetween);
        }
    }

    public void ClearMessage()
    {
        if (_messageTween != null && _messageTween.IsActive())
            _messageTween.Kill();

        _messageTween = null;
        ClearLineGroups();

        if (_messageRoot != null)
            _messageRoot.gameObject.SetActive(false);
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        FadeTo(1f, duration, onComplete);
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        FadeTo(0f, duration, () =>
        {
            ClearMessage();
            onComplete?.Invoke();
        });
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

    void ClearLineGroups()
    {
        for (int i = 0; i < _lineGroups.Count; i++)
        {
            if (_lineGroups[i] != null)
                Destroy(_lineGroups[i].gameObject);
        }

        _lineGroups.Clear();
    }

    void AddLine(string text, float fontSize, Color color, TMP_FontAsset font)
    {
        var lineGo = new GameObject("ThanksLine");
        lineGo.transform.SetParent(_messageRoot, false);

        var textComp = lineGo.AddComponent<TextMeshProUGUI>();
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontSize = fontSize;
        textComp.color = color;
        textComp.enableWordWrapping = true;
        textComp.raycastTarget = false;
        textComp.text = text;
        textComp.margin = Vector4.zero;
        textComp.lineSpacing = 0f;
        if (font != null)
            textComp.font = font;

        float height = fontSize * Mathf.Max(0.8f, lineHeightMul);
        var layoutElement = lineGo.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;

        var group = lineGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        _lineGroups.Add(group);
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

    void EnsureMessageRoot()
    {
        if (_messageRoot != null)
            return;

        EnsureOverlay();

        var rootGo = new GameObject("ThanksRoot");
        rootGo.transform.SetParent(_group.transform.parent, false);

        _messageRoot = rootGo.AddComponent<RectTransform>();
        _messageRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _messageRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _messageRoot.pivot = new Vector2(0.5f, 0.5f);
        _messageRoot.anchoredPosition = Vector2.zero;
        _messageRoot.sizeDelta = new Vector2(780f, 0f);

        var layout = rootGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = lineSpacing;
        layout.padding = new RectOffset(0, 0, 0, 0);

        var fitter = rootGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rootGo.SetActive(false);
    }
}
