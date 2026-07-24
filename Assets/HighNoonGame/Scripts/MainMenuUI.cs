using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void OnNewGame()
    {
        GameRoot.Instance.Run.StartNewGame();
    }
    
    public void OnContinue()
    {
        GameRoot.Instance.Run.StartNewGame();
    }

    public void OnSettings()
    {
        
    }

    public void OnExit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif   
    }

}
