using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] SettingsMenuUI settingsMenu;

    public void OnNewGame()
    {
        PlayClick();
        GameRoot.Instance.Run.StartNewGame();
    }

    public void OnContinue()
    {
        PlayClick();
        GameRoot.Instance.Run.StartNewGame();
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
