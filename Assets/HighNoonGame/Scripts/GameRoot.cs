using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class GameRoot : PersistentSingleton<GameRoot>
{
    public const string MenuSceneName = "MenuScene";
    public const string BattleSceneName = "BattleScene";

    public RunManager Run { get; private set; }

    EventSystem _ownedEventSystem;

    protected override void Awake()
    {
        base.Awake();
        Run = GetComponent<RunManager>();
        if (Run == null)
            Run = gameObject.AddComponent<RunManager>();

        EnsureEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        LoadMenu();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        CleanupDuplicateEventSystems();
    }

    public void LoadMenu()
    {
        SceneManager.LoadScene(MenuSceneName, LoadSceneMode.Single);
    }

    public void LoadBattle()
    {
        SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
    }

    public void RestartBattle()
    {
        LoadBattle();
    }

    void EnsureEventSystem()
    {
        if (_ownedEventSystem != null)
            return;

        var go = new GameObject("EventSystem (GameRoot)");
        DontDestroyOnLoad(go);
        _ownedEventSystem = go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    void CleanupDuplicateEventSystems()
    {
        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i] != _ownedEventSystem)
                Destroy(systems[i].gameObject);
        }
    }
}
