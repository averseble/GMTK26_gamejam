using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class GameRoot : PersistentSingleton<GameRoot>
{
    public const string MenuSceneName = "MenuScene";
    public const string EnemySelectSceneName = "EnemySelectScene";
    public const string BattleSceneName = "BattleScene";

    public RunManager Run { get; private set; }
    public AudioManager Audio { get; private set; }
    public ScreenFader Fader { get; private set; }

    EventSystem _ownedEventSystem;
    bool _isTransitioning;

    protected override void Awake()
    {
        base.Awake();

        Run = GetComponent<RunManager>();
        if (Run == null)
            Run = gameObject.AddComponent<RunManager>();

        Audio = GetComponent<AudioManager>();
        if (Audio == null)
            Audio = gameObject.AddComponent<AudioManager>();

        Fader = GetComponent<ScreenFader>();
        if (Fader == null)
            Fader = gameObject.AddComponent<ScreenFader>();

        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        EnsureEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SyncAudioListener();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        if (Fader != null)
            Fader.SetAlpha(1f);

        LoadSceneImmediate(MenuSceneName);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        CleanupDuplicateEventSystems();
        SyncAudioListener();
        _isTransitioning = false;

        if (Fader != null)
            Fader.FadeIn();
    }

    void SyncAudioListener()
    {
        AudioListener ours = GetComponent<AudioListener>();
        if (ours == null)
            ours = gameObject.AddComponent<AudioListener>();

        bool sceneHasOther = false;
        AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != ours)
            {
                sceneHasOther = true;
                break;
            }
        }

        ours.enabled = !sceneHasOther;
    }

    public void LoadMenu()
    {
        TransitionTo(MenuSceneName);
    }

    public void LoadEnemySelect()
    {
        TransitionTo(EnemySelectSceneName);
    }

    public void LoadBattle()
    {
        TransitionTo(BattleSceneName);
    }

    public void RestartBattle()
    {
        TransitionTo(BattleSceneName);
    }

    void TransitionTo(string sceneName)
    {
        if (_isTransitioning)
            return;

        if (Fader == null)
        {
            LoadSceneImmediate(sceneName);
            return;
        }

        if (Fader.IsFullyOpaque)
        {
            LoadSceneImmediate(sceneName);
            return;
        }

        _isTransitioning = true;
        Fader.FadeOut(-1f, () => LoadSceneImmediate(sceneName));
    }

    void LoadSceneImmediate(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
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
