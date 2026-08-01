using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BattleMenuUI : MonoBehaviour
{
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] GameObject bothDeadPanel;

    [SerializeField] Transform slideRoot;
    [SerializeField] Vector3 bottomLocalPosition;
    [SerializeField] Vector3 topLocalPosition;
    [SerializeField] float slideDuration = 0.45f;

    [SerializeField] Canvas canvas;
    [SerializeField] Camera eventCamera;

    [Header("Ending Thanks")]
    [SerializeField] float thanksFadeDuration = 1.1f;
    [SerializeField] float thanksTextFadeDuration = 1f;
    [SerializeField] float thanksLineDelay = 0.25f;
    [SerializeField] TMP_FontAsset thanksTitleFont;
    [SerializeField] TMP_FontAsset thanksGameNameFont;
    [SerializeField] string thanksMessage = "Thanks for playing";
    [SerializeField] string thanksGameName = "Be Ready by Noon";
    [SerializeField] [TextArea(4, 10)] string thanksCredits =
        "Made for GMTK Game Jam 2026\n\n" +
        "3D Models — baladune, mihanemoi, pduster, aver14\n" +
        "Art — mihanemoi\n" +
        "Audio — bombotska\n" +
        "Code & Design — aver14";

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

    public void ShowResult(BattleOutcome outcome)
    {
        bool draw = outcome == BattleOutcome.Draw;
        bool won = outcome == BattleOutcome.PlayerWin;

        if (winPanel != null)
            winPanel.SetActive(won && !draw);
        if (losePanel != null)
            losePanel.SetActive(!won && !draw);
        if (bothDeadPanel != null)
            bothDeadPanel.SetActive(draw);

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
        if (bothDeadPanel != null)
            bothDeadPanel.SetActive(false);

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

    public IEnumerator PlayThanksEndingRoutine()
    {
        ScreenFader fader = null;
        if (GameRoot.Instance != null)
            fader = GameRoot.Instance.Fader;

        if (fader == null)
            fader = FindFirstObjectByType<ScreenFader>();

        if (fader != null)
        {
            fader.PrepareMessageLines(
                thanksMessage,
                thanksGameName,
                thanksCredits,
                thanksTitleFont,
                thanksGameNameFont);

            bool screenFadeDone = false;
            fader.FadeOut(thanksFadeDuration, () => screenFadeDone = true);
            while (!screenFadeDone)
                yield return null;

            yield return fader.FadeInLinesSequential(thanksTextFadeDuration, thanksLineDelay);
        }

        yield return null;
        while (!WasAnyQuitInputPressed())
            yield return null;

        QuitGame();
    }

    static bool WasAnyQuitInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame
                || mouse.middleButton.wasPressedThisFrame)
                return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame
                || gamepad.selectButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    static void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnRestartClicked()
    {
        GameRoot.Instance.Run.RestartBattle();
    }

    public void OnNextEnemyClicked()
    {
        GameRoot.Instance.Run.GoToEnemySelect();
    }

    public void OnMenuClicked()
    {
        GameRoot.Instance.Run.GoToMenu();
    }
}
