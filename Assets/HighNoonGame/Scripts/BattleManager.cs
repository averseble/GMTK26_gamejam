using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BattleState
{
    none,
    opening,
    planning,
    highNoon,
    ending
}

public enum BattleOutcome
{
    None,
    PlayerWin,
    EnemyWin,
    Draw,
    NoHits
}

public struct gameField
{
    public int ownerID;
    public int obstacleID;
    public Vector3 position;
}

public enum TileActionKind
{
    Move,
    Shoot
}

public enum PlanningActionType
{
    MoveLeft,
    MoveRight,
    Shoot
}

public struct PlanningAction
{
    public PlanningActionType type;
    public int shootTileIndex;
}

public class BattleManager : Singleton<BattleManager>
{
    public BattleState currentState { get; private set; }

    public bool WaitingForActionSelection { get; private set; }

    public PlayerTileSelecter playerTileSelecter;
    public BattleMenuUI battleMenuUI;

    private RunManager run;

    GameObject playerCharacter;
    GameObject enemyCharacter;

    int playerPos = 5;
    int enemyPos = 2;

    const int columnsInMap = 4;
    const int rowsInMap = 2;

    public Transform mapCenterOnScene;
    public float FieldSize = 3f;
    public float FieldOffset = 0.1f;

    public gameField[] battleMap;

    public int maxPlanningActions = 3;
    public int curPlayerPlaningAction = 0;
    [Tooltip("Пауза между шагами High Noon")]
    public float highNoonStepDelay = 0.45f;

    [Header("Выстрел (LineRenderer)")]
    [SerializeField] float shotLineHeight = 1.2f;
    [SerializeField] float shotLineWidth = 0.06f;
    [SerializeField] float shotLineLifetime = 0.35f;
    [SerializeField] Color playerShotColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] Color enemyShotColor = new Color(1f, 0.25f, 0.15f, 1f);
    [SerializeField] Material shotLineMaterial;

    private PlanningAction[] playerPlanningList;
    private PlanningAction[] enemyPlanningList;

    Coroutine _highNoonRoutine;
    bool _playerPlanActive;
    bool _enemyPlanActive;
    bool _playerAlive = true;
    bool _enemyAlive = true;

    protected override void Awake()
    {
        base.Awake();
        currentState = BattleState.none;

        if (GameRoot.Instance != null)
            run = GameRoot.Instance.Run;
    }

    void Start()
    {
        InitBattleData();
    }

    void InitBattleData()
    {
        if (run == null && GameRoot.Instance != null)
            run = GameRoot.Instance.Run;

        if (playerTileSelecter == null)
            playerTileSelecter = FindFirstObjectByType<PlayerTileSelecter>();

        if (playerTileSelecter != null)
            playerTileSelecter.OnActionSelected.AddListener(OnPlayerSelectedAction);

        if (run == null)
        {
            Debug.LogError("BattleManager: RunManager is missing");
            return;
        }

        var playerConfig = run.GetPlayerSO();
        var enemyConfig = run.GetEnemySO();
        if (playerConfig == null || enemyConfig == null)
        {
            Debug.LogError("BattleManager: assign PlayerConfig and EnemyConfig on RunManager");
            return;
        }

        var playerPrefab = playerConfig.GetPlayerPrefab();
        var enemyPrefab = enemyConfig.GetEnemyPrefab();
        if (playerPrefab == null || enemyPrefab == null)
        {
            Debug.LogError("BattleManager: player or enemy prefab is missing in config");
            return;
        }

        if (mapCenterOnScene == null && playerTileSelecter == null)
        {
            Debug.LogError("BattleManager: PlayerTileSelecter is missing");
            return;
        }

        playerCharacter = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        enemyCharacter = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity);

        int mapSize = columnsInMap * rowsInMap;
        battleMap = new gameField[mapSize];

        if (!SyncBattleMapFromTiles())
        {
            Debug.LogError("BattleManager: failed to sync tile positions from PlayerTileSelecter.");
            return;
        }

        battleMap[playerPos].ownerID = 1;
        battleMap[enemyPos].ownerID = 2;

        playerPlanningList = new PlanningAction[maxPlanningActions];
        enemyPlanningList = new PlanningAction[maxPlanningActions];

        playerCharacter.transform.position = battleMap[playerPos].position;
        enemyCharacter.transform.position = battleMap[enemyPos].position;

        StartBattle();
    }

    bool SyncBattleMapFromTiles()
    {
        if (playerTileSelecter == null)
            playerTileSelecter = FindFirstObjectByType<PlayerTileSelecter>();

        if (playerTileSelecter == null)
        {
            Debug.LogError("BattleManager: PlayerTileSelecter not assigned and not found in scene.");
            return false;
        }

        return playerTileSelecter.WritePositionsToBattleMap(battleMap);
    }

    public void StartBattle()
    {
        ShowOpening();
        StartPlanningPhase();
    }

    void StartHighNoonPhase()
    {
        if (_highNoonRoutine != null)
            StopCoroutine(_highNoonRoutine);

        currentState = BattleState.highNoon;
        WaitingForActionSelection = false;
        _playerPlanActive = true;
        _enemyPlanActive = true;
        _playerAlive = true;
        _enemyAlive = true;

        if (playerTileSelecter != null)
            playerTileSelecter.ClearCommittedPreviews();

        _highNoonRoutine = StartCoroutine(HighNoonRoutine());
    }

    IEnumerator HighNoonRoutine()
    {
        int steps = maxPlanningActions;
        if (playerPlanningList != null)
            steps = Mathf.Min(steps, playerPlanningList.Length);
        if (enemyPlanningList != null)
            steps = Mathf.Min(steps, enemyPlanningList.Length);

        for (int step = 0; step < steps; step++)
        {
            if (!_playerAlive && !_enemyAlive)
                break;

            if (_playerPlanActive && _playerAlive)
                TryExecuteMove(true, playerPlanningList[step]);
            if (_enemyPlanActive && _enemyAlive)
                TryExecuteMove(false, enemyPlanningList[step]);

            RefreshCharacterTransforms();
            yield return new WaitForSeconds(highNoonStepDelay);

            bool playerHitEnemy = false;
            bool enemyHitPlayer = false;

            if (_playerPlanActive && _playerAlive)
                playerHitEnemy = TryExecuteShoot(true, playerPlanningList[step]);
            if (_enemyPlanActive && _enemyAlive)
                enemyHitPlayer = TryExecuteShoot(false, enemyPlanningList[step]);

            if (playerHitEnemy && enemyHitPlayer)
            {
                OnEnemyHit();
                OnPlayerHit();
                OnDraw();
                _enemyAlive = false;
                _playerAlive = false;
                _enemyPlanActive = false;
                _playerPlanActive = false;
                yield return new WaitForSeconds(highNoonStepDelay);
                break;
            }

            if (playerHitEnemy)
            {
                OnEnemyHit();
                _enemyAlive = false;
                _enemyPlanActive = false;
                yield return new WaitForSeconds(highNoonStepDelay);
                break;
            }

            if (enemyHitPlayer)
            {
                OnPlayerHit();
                _playerAlive = false;
                _playerPlanActive = false;
                yield return new WaitForSeconds(highNoonStepDelay);
                break;
            }

            yield return new WaitForSeconds(highNoonStepDelay * 0.5f);
        }

        EndHighNoonPhase();
        _highNoonRoutine = null;
    }

    void TryExecuteMove(bool isPlayer, PlanningAction action)
    {
        if (action.type != PlanningActionType.MoveLeft && action.type != PlanningActionType.MoveRight)
            return;

        if (isPlayer)
        {
            ClearOwnerAt(playerPos);
            playerPos = ApplyActionToPos(playerPos, action);
            battleMap[playerPos].ownerID = 1;
        }
        else
        {
            ClearOwnerAt(enemyPos);
            enemyPos = ApplyActionToPos(enemyPos, action);
            battleMap[enemyPos].ownerID = 2;
        }
    }

    bool TryExecuteShoot(bool isPlayer, PlanningAction action)
    {
        if (action.type != PlanningActionType.Shoot)
            return false;

        int target = action.shootTileIndex;
        if (target < 0 || battleMap == null || target >= battleMap.Length)
            return false;

        int fromTile = isPlayer ? playerPos : enemyPos;
        SpawnShotLine(fromTile, target, isPlayer);

        if (isPlayer)
            return _enemyAlive && target == enemyPos;

        return _playerAlive && target == playerPos;
    }

    void SpawnShotLine(int fromTile, int toTile, bool isPlayer)
    {
        Vector3 start = battleMap[fromTile].position + Vector3.up * shotLineHeight;
        Vector3 end = battleMap[toTile].position + Vector3.up * shotLineHeight;

        var go = new GameObject(isPlayer ? "PlayerShotLine" : "EnemyShotLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = shotLineWidth;
        lr.endWidth = shotLineWidth * 0.5f;
        lr.useWorldSpace = true;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (shotLineMaterial != null)
            lr.material = shotLineMaterial;
        else
            lr.material = new Material(Shader.Find("Sprites/Default"));

        Color c = isPlayer ? playerShotColor : enemyShotColor;
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, 0.15f);

        StartCoroutine(DespawnShotLine(go, shotLineLifetime));
    }

    IEnumerator DespawnShotLine(GameObject lineGo, float lifetime)
    {
        if (lifetime > 0f)
            yield return new WaitForSeconds(lifetime);
        if (lineGo != null)
            Destroy(lineGo);
    }

    void ClearOwnerAt(int tileIndex)
    {
        if (battleMap == null || tileIndex < 0 || tileIndex >= battleMap.Length)
            return;
        battleMap[tileIndex].ownerID = 0;
    }

    void RefreshCharacterTransforms()
    {
        if (playerCharacter != null && battleMap != null)
            playerCharacter.transform.position = battleMap[playerPos].position;
        if (enemyCharacter != null && battleMap != null)
            enemyCharacter.transform.position = battleMap[enemyPos].position;
    }

    void OnPlayerHit()
    {
        Debug.Log("High Noon: player hit");
    }

    void OnEnemyHit()
    {
        Debug.Log("High Noon: enemy hit");
    }

    void OnDraw()
    {
        Debug.Log("High Noon: draw — both hit");
    }

    void EndHighNoonPhase()
    {
        currentState = BattleState.ending;

        BattleOutcome outcome;
        if (!_playerAlive && !_enemyAlive)
            outcome = BattleOutcome.Draw; // показываем как lose
        else if (_playerAlive && !_enemyAlive)
            outcome = BattleOutcome.PlayerWin;
        else if (!_playerAlive && _enemyAlive)
            outcome = BattleOutcome.EnemyWin;
        else
            outcome = BattleOutcome.NoHits;

        Debug.Log($"High Noon ended: {outcome}");

        // Никто не попал — сразу реванш, без меню
        if (outcome == BattleOutcome.NoHits)
        {
            GameRoot.Instance.RestartBattle();
            return;
        }

        if (battleMenuUI == null)
            battleMenuUI = FindFirstObjectByType<BattleMenuUI>();

        if (battleMenuUI != null)
            battleMenuUI.ShowResult(outcome);

        if (GameRoot.Instance != null && GameRoot.Instance.Run != null)
            GameRoot.Instance.Run.OnBattleFinished(outcome == BattleOutcome.PlayerWin);
    }

    void StartPlanningPhase()
    {
        currentState = BattleState.planning;
        curPlayerPlaningAction = 0;
        if (playerTileSelecter != null)
            playerTileSelecter.ClearCommittedPreviews();
        BeginActionSelection();
        FillEnemyPlanningList();
    }

    public void ShowOpening()
    {
        currentState = BattleState.opening;
    }

    public void BeginActionSelection()
    {
        currentState = BattleState.planning;
        WaitingForActionSelection = true;
    }

    public void OnPlayerSelectedAction(int tileIndex, TileActionKind action)
    {
        if (!TryBuildPlayerAction(tileIndex, action, out PlanningAction planAction))
            return;

        int simPosBefore = SimulatePos(playerPos, playerPlanningList, curPlayerPlaningAction);
        playerPlanningList[curPlayerPlaningAction] = planAction;
        curPlayerPlaningAction++;

        if (playerTileSelecter != null)
        {
            int previewTile = planAction.type == PlanningActionType.Shoot
                ? planAction.shootTileIndex
                : ApplyActionToPos(simPosBefore, planAction);

            playerTileSelecter.CommitActionPreview(action, previewTile);
            playerTileSelecter.ClearSelection();
        }

        if (curPlayerPlaningAction >= maxPlanningActions)
        {
            EndActionSelection();
            StartHighNoonPhase();
        }
    }

    public void EndActionSelection()
    {
        WaitingForActionSelection = false;
    }

    public bool IsPlayerActionValid(int tileIndex, TileActionKind action)
    {
        return TryBuildPlayerAction(tileIndex, action, out _);
    }

    bool TryBuildPlayerAction(int tileIndex, TileActionKind action, out PlanningAction planAction)
    {
        planAction = default;
        int simPos = SimulatePos(playerPos, playerPlanningList, curPlayerPlaningAction);

        if (action == TileActionKind.Shoot)
        {
            if (!IsPlayerShootTile(tileIndex))
                return false;

            planAction = new PlanningAction
            {
                type = PlanningActionType.Shoot,
                shootTileIndex = tileIndex
            };
            return true;
        }

        if (!IsPlayerMoveTile(tileIndex))
            return false;

        int curCol = GetColumn(simPos);
        int targetCol = GetColumn(tileIndex);
        int delta = targetCol - curCol;

        if (delta != -1 && delta != 1)
            return false;

        if (delta < 0 && curCol <= 0)
            return false;
        if (delta > 0 && curCol >= columnsInMap - 1)
            return false;

        planAction = new PlanningAction
        {
            type = delta < 0 ? PlanningActionType.MoveLeft : PlanningActionType.MoveRight,
            shootTileIndex = -1
        };
        return true;
    }

    public void FillEnemyPlanningList()
    {
        if (enemyPlanningList == null || enemyPlanningList.Length != maxPlanningActions)
            enemyPlanningList = new PlanningAction[maxPlanningActions];

        EnemyAiType aiType = EnemyAiType.random;
        if (run != null)
        {
            var enemyConfig = run.GetEnemySO();
            if (enemyConfig != null)
                aiType = enemyConfig.GetAiType();
        }

        int simPos = enemyPos;
        for (int i = 0; i < maxPlanningActions; i++)
        {
            enemyPlanningList[i] = CreateEnemyAction(aiType, simPos);
            simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
        }
    }

    PlanningAction CreateEnemyAction(EnemyAiType aiType, int simPos)
    {
        int col = GetColumn(simPos);
        bool canLeft = col > 0;
        bool canRight = col < columnsInMap - 1;

        switch (aiType)
        {
            case EnemyAiType.rightEye:
                if (Random.value < 0.5f || !canRight)
                {
                    return new PlanningAction
                    {
                        type = PlanningActionType.Shoot,
                        shootTileIndex = PlayerRowTileFromColumn(Mathf.Min(col + 1, columnsInMap - 1))
                    };
                }
                return new PlanningAction
                {
                    type = PlanningActionType.MoveRight,
                    shootTileIndex = -1
                };

            case EnemyAiType.leftLeg:
                if (Random.value < 0.5f || !canLeft)
                {
                    return new PlanningAction
                    {
                        type = PlanningActionType.Shoot,
                        shootTileIndex = PlayerRowTileFromColumn(col)
                    };
                }
                return new PlanningAction
                {
                    type = PlanningActionType.MoveLeft,
                    shootTileIndex = -1
                };

            case EnemyAiType.random:
            default:
                if (Random.value < 0.5f)
                {
                    return new PlanningAction
                    {
                        type = PlanningActionType.Shoot,
                        shootTileIndex = PlayerRowTileFromColumn(Random.Range(0, columnsInMap))
                    };
                }

                if (canLeft && canRight)
                {
                    return new PlanningAction
                    {
                        type = Random.value < 0.5f ? PlanningActionType.MoveLeft : PlanningActionType.MoveRight,
                        shootTileIndex = -1
                    };
                }
                if (canLeft)
                    return new PlanningAction { type = PlanningActionType.MoveLeft, shootTileIndex = -1 };
                if (canRight)
                    return new PlanningAction { type = PlanningActionType.MoveRight, shootTileIndex = -1 };

                return new PlanningAction
                {
                    type = PlanningActionType.Shoot,
                    shootTileIndex = PlayerRowTileFromColumn(col)
                };
        }
    }

    int SimulatePos(int startPos, PlanningAction[] plan, int count)
    {
        int pos = startPos;
        for (int i = 0; i < count; i++)
            pos = ApplyActionToPos(pos, plan[i]);
        return pos;
    }

    int ApplyActionToPos(int pos, PlanningAction action)
    {
        int col = GetColumn(pos);
        int row = GetRow(pos);

        switch (action.type)
        {
            case PlanningActionType.MoveLeft:
                if (col > 0) col--;
                break;
            case PlanningActionType.MoveRight:
                if (col < columnsInMap - 1) col++;
                break;
        }

        return row * columnsInMap + col;
    }

    static int GetColumn(int tileIndex) => tileIndex % columnsInMap;
    static int GetRow(int tileIndex) => tileIndex / columnsInMap;

    static bool IsPlayerMoveTile(int tileIndex) => tileIndex >= 4 && tileIndex <= 7;
    static bool IsPlayerShootTile(int tileIndex) => tileIndex >= 0 && tileIndex <= 3;

    static int PlayerRowTileFromColumn(int column) =>
        columnsInMap + Mathf.Clamp(column, 0, columnsInMap - 1);

    void Update()
    {
        if (battleMap == null) return;

        for (int i = 0; i < battleMap.Length; i++)
            Debug.DrawRay(battleMap[i].position, Vector3.up);
    }
}
