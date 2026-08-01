using TMPro;
using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] SettingsMenuUI settingsMenu;
    [SerializeField] TMP_Text newGameButtonLabel;
    [SerializeField] string newGameLabel = "New Game";
    [SerializeField] string continueLabel = "Continue";

    void OnEnable()
    {
        RefreshNewGameLabel();
    }

    void Start()
    {
        RefreshNewGameLabel();
    }

    void RefreshNewGameLabel()
    {
        if (newGameButtonLabel == null)
            newGameButtonLabel = FindNewGameLabel();

        if (newGameButtonLabel == null)
            return;

        bool hasProgress = false;
        if (GameRoot.Instance != null && GameRoot.Instance.Run != null)
            hasProgress = GameRoot.Instance.Run.LevelsCleared > 0;

        if (hasProgress)
            newGameButtonLabel.text = continueLabel;
        else
            newGameButtonLabel.text = newGameLabel;
    }

    static TMP_Text FindNewGameLabel()
    {
        GameObject buttonGo = GameObject.Find("New Game Btn");
        if (buttonGo == null)
            return null;

        return buttonGo.GetComponentInChildren<TMP_Text>(true);
    }

    public void OnNewGame()
    {
        PlayClick();

        if (GameRoot.Instance == null || GameRoot.Instance.Run == null)
            return;

        if (GameRoot.Instance.Run.LevelsCleared > 0)
            GameRoot.Instance.Run.ContinueGame();
        else
            GameRoot.Instance.Run.StartNewGame();
    }

    public void OnContinue()
    {
        PlayClick();
        GameRoot.Instance.Run.ContinueGame();
    }

    public void OnSettings()
    {
        if (settingsMenu == null)
            settingsMenu = FindFirstObjectByType<SettingsMenuUI>(FindObjectsInactive.Include);

        if (settingsMenu != null)
            settingsMenu.Open();
        else
            PlayClick();
    }

    public void OnExit()
    {
        PlayClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    static void PlayClick()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Audio != null)
            GameRoot.Instance.Audio.PlayUiClick();
    }
}
