using UnityEngine;
using UnityEngine.SceneManagement;

struct RunState
{
    
}

public class RunManager : Singleton<RunManager>
{
    RunState runState;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void NewGameBtn_click()
    {
        SceneManager.LoadScene("BattleScene", LoadSceneMode.Single);        
    }

    public void SettingsBtn_click()
    {
        
    }

    public void ExitBtn_click()
    {
        
    }
}
