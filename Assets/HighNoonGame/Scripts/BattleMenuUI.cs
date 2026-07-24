using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BattleMenuUI : MonoBehaviour
{
    [Header("Панели (внутри — свои кнопки/текст)")]
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;

    [Header("Слайд: нижняя (спрятано) → верхняя (показано)")]
    [SerializeField] Transform slideRoot;
    [Tooltip("Позиция под картой")]
    [SerializeField] Vector3 bottomLocalPosition;
    [Tooltip("Позиция когда меню видно")]
    [SerializeField] Vector3 topLocalPosition;
    [SerializeField] float slideDuration = 0.45f;

    [Header("World Space UI")]
    [SerializeField] Canvas canvas;
    [SerializeField] Camera eventCamera;

    Coroutine _slideRoutine;

    void Awake()
    {
        SetupCanvasForClicks();
        HideImmediate();
    }

    void SetupCanvasForClicks()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            if (eventCamera == null)
                eventCamera = Camera.main;
            canvas.worldCamera = eventCamera;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// Win → winPanel (внутри Next Enemy и т.п.).
    /// Lose / Draw / NoHits → losePanel (внутри Restart и т.п.).
    /// </summary>
    public void ShowResult(BattleOutcome outcome)
    {
        bool won = outcome == BattleOutcome.PlayerWin;

        if (winPanel != null)
            winPanel.SetActive(won);
        if (losePanel != null)
            losePanel.SetActive(!won);

        if (slideRoot != null && !slideRoot.gameObject.activeSelf)
            slideRoot.gameObject.SetActive(true);

        SlideToTop();
    }

    public void Hide()
    {
        SlideToBottom();
    }

    public void SlideToTop()
    {
        StartSlide(topLocalPosition);
    }

    public void SlideToBottom()
    {
        StartSlide(bottomLocalPosition);
    }

    void HideImmediate()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
        if (losePanel != null)
            losePanel.SetActive(false);

        if (slideRoot != null)
            slideRoot.localPosition = bottomLocalPosition;
    }

    void StartSlide(Vector3 targetLocalPos)
    {
        if (slideRoot == null)
            return;

        if (_slideRoutine != null)
            StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideRoutine(targetLocalPos));
    }

    IEnumerator SlideRoutine(Vector3 to)
    {
        Vector3 from = slideRoot.localPosition;
        float t = 0f;

        if (slideDuration <= 0f)
        {
            slideRoot.localPosition = to;
            _slideRoutine = null;
            yield break;
        }

        while (t < 1f)
        {
            t += Time.deltaTime / slideDuration;
            slideRoot.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        slideRoot.localPosition = to;
        _slideRoutine = null;
    }

    // --- кнопки → только через GameRoot / Run ---

    public void OnRestartClicked()
    {
        GameRoot.Instance.Run.RestartBattle();
    }

    public void OnNextEnemyClicked()
    {
        GameRoot.Instance.Run.OnBattleFinished(true);
        // Пока нет следующего врага — тот же рестарт боя через корень
        GameRoot.Instance.Run.RestartBattle();
    }

    public void OnMenuClicked()
    {
        GameRoot.Instance.Run.GoToMenu();
    }
}
