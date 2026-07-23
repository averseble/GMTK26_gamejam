using UnityEngine;
using UnityEngine.SceneManagement;

public class GameRoot : PersistentSingleton<GameRoot>
{
    void Start(){
        //load settings
        LoadRunManager();
        LoadMenu();
    }

    void LoadSettings(){}
    void LoadRunManager(){}
    void LoadMenu(){        
        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
    }
}
