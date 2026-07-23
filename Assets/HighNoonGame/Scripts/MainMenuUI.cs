using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void OnNewGame()
    {
        
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
