using UnityEngine;

public struct RunState
{
}

public class RunManager : MonoBehaviour
{
    public RunState Current { get; private set; }

    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] EnemyConfig enemyConfig;

    public void StartNewGame()
    {
        GameRoot.Instance.LoadBattle();
    }

    public void ContinueGame()
    {
        GameRoot.Instance.LoadBattle();
    }

    public void OnBattleFinished(bool won)
    {
        // позже: прогресс забега / следующий враг
    }

    public void GoToMenu()
    {
        GameRoot.Instance.LoadMenu();
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
        return enemyConfig;
    }
}
