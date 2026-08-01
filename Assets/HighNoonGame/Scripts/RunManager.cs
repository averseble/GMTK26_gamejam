using UnityEngine;

public struct RunState
{
}

public class RunManager : MonoBehaviour
{
    const string PrefLevelsCleared = "hn_levels_cleared";

    public RunState Current { get; private set; }

    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] EnemyConfig enemyConfig;

    EnemyConfig _selectedEnemy;
    int _selectedLevelIndex = -1;
    int _levelsCleared;
    [SerializeField] int totalEnemyCount = 6;

    public int LevelsCleared
    {
        get { return _levelsCleared; }
    }

    public int TotalEnemyCount
    {
        get { return totalEnemyCount; }
    }

    public bool IsFinalEnemySelected
    {
        get
        {
            if (_selectedLevelIndex < 0)
                return false;

            if (totalEnemyCount <= 0)
                return false;

            return _selectedLevelIndex >= totalEnemyCount - 1;
        }
    }

    public int MaxUnlockedIndex
    {
        get { return _levelsCleared; }
    }

    public int LastCompletedLevelIndex
    {
        get
        {
            if (_levelsCleared <= 0)
                return 0;

            return _levelsCleared - 1;
        }
    }

    void Awake()
    {
        _levelsCleared = PlayerPrefs.GetInt(PrefLevelsCleared, 0);
    }

    public void StartNewGame()
    {
        GameRoot.Instance.LoadEnemySelect();
    }

    public void ContinueGame()
    {
        GameRoot.Instance.LoadEnemySelect();
    }

    public void SelectEnemyAndStartBattle(EnemyConfig selected, int levelIndex)
    {
        _selectedEnemy = selected;
        _selectedLevelIndex = levelIndex;
        GameRoot.Instance.LoadBattle();
    }

    public void OnBattleFinished(bool won)
    {
        if (!won)
            return;

        if (_selectedLevelIndex < 0)
            return;

        int newCleared = _selectedLevelIndex + 1;
        if (newCleared <= _levelsCleared)
            return;

        _levelsCleared = newCleared;
        PlayerPrefs.SetInt(PrefLevelsCleared, _levelsCleared);
        PlayerPrefs.Save();
    }

    public void GoToMenu()
    {
        GameRoot.Instance.LoadMenu();
    }

    public void GoToEnemySelect()
    {
        GameRoot.Instance.LoadEnemySelect();
    }

    public void RestartBattle()
    {
        GameRoot.Instance.RestartBattle();
    }

    public PlayerConfig GetPlayerSO()
    {
        return playerConfig;
    }

    public EnemyConfig GetEnemySO()
    {
        if (_selectedEnemy != null)
            return _selectedEnemy;

        return enemyConfig;
    }

    public bool IsLevelUnlocked(int levelIndex)
    {
        if (levelIndex < 0)
            return false;

        return levelIndex <= _levelsCleared;
    }
}
