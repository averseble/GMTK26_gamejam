using System;
using System.Collections;
using DG.Tweening;
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
    public int shootTileIndex2;
    public int shootTileIndex3;

    public static PlanningAction CreateMove(PlanningActionType moveType)
    {
        return new PlanningAction
        {
            type = moveType,
            shootTileIndex = -1,
            shootTileIndex2 = -1,
            shootTileIndex3 = -1
        };
    }

    public static PlanningAction CreateShoot(int tileA, int tileB = -1, int tileC = -1)
    {
        return new PlanningAction
        {
            type = PlanningActionType.Shoot,
            shootTileIndex = tileA,
            shootTileIndex2 = tileB,
            shootTileIndex3 = tileC
        };
    }
}

public class BattleManager : Singleton<BattleManager>
{
    public BattleState currentState { get; private set; }

    public bool WaitingForActionSelection { get; private set; }

    public event Action ShotFired;
    public event Action<int> TileShot;
    public event Action<bool, Vector3> CharacterHit;
    public event Action<int, int> PlanningProgressChanged;
    public event Action HighNoonStarted;
    public event Action HighNoonEnded;
    public event Action<bool, PlanningActionType> CharacterDash;
    public event Action<bool, Vector3> CharacterShot;
    public event Action<BattleOutcome> BattleResolved;

    public PlayerTileSelecter playerTileSelecter;
    public BattleMenuUI battleMenuUI;
    [SerializeField] EnemyDialogueUI enemyDialogueUI;
    [SerializeField] BattleDialogueCamera dialogueCamera;
    [SerializeField] DayNightLighting dayNightLighting;
    [SerializeField] PlanningClockShake planningClock;

    private RunManager run;

    GameObject playerCharacter;
    GameObject enemyCharacter;
    Quaternion playerSpawnRotation = Quaternion.identity;
    Quaternion enemySpawnRotation = Quaternion.identity;
    Transform playerMuzzleTarget;
    Transform playerHeadTarget;
    Transform enemyMuzzleTarget;
    Transform enemyHeadTarget;

    const int DefaultPlayerPos = 5;
    const int DefaultEnemyPos = 2;

    int playerPos = DefaultPlayerPos;
    int enemyPos = DefaultEnemyPos;

    const int columnsInMap = 4;
    const int rowsInMap = 2;

    public Transform mapCenterOnScene;
    public float FieldSize = 3f;
    public float FieldOffset = 0.1f;

    public gameField[] battleMap;

    public int maxPlanningActions = 3;
    public int curPlayerPlaningAction = 0;
    [Tooltip("Пауза между шагами High Noon (только движение / общий ритм)")]
    public float highNoonStepDelay = 0.7f;
    [Tooltip("Пауза после хода перед выстрелами")]
    [SerializeField] float highNoonPreShootDelay = 0.4f;
    [Tooltip("Пауза между выстрелом игрока и врага")]
    [SerializeField] float highNoonShotStagger = 0.25f;
    [Tooltip("Пауза после выстрелов перед следующим шагом")]
    [SerializeField] float highNoonAfterShotDelay = 0.75f;
    [SerializeField] float animWaitTimeout = 2f;

    public float AnimWaitTimeout
    {
        get { return animWaitTimeout; }
    }

    [Header("Shot Trace")]
    [SerializeField] Tracer bulletCorePrefab;
    [SerializeField] Tracer bulletSmokePrefab;
    [SerializeField] float shotLineHeight = 0f;
    [SerializeField] float bulletCoreDuration = 0.12f;
    [SerializeField] float bulletSmokeDuration = 0.85f;

    [Header("Shot VFX")]
    [SerializeField] ParticleSystem muzzleFlashPrefab;
    [SerializeField] ParticleSystem tileImpactPrefab;
    [SerializeField] ParticleSystem enemyHitPrefab;
    [SerializeField] ParticleSystem playerHitPrefab;
    [SerializeField] ParticleSystem characterHitExtraPrefab;
    [SerializeField] ParticleSystem groundStainPrefab;
    [SerializeField] float shotVfxDestroyDelay = 2f;
    [SerializeField] float groundStainLifetime = 20f;
    [SerializeField] float groundStainHeight = 0.02f;

    [Header("Dash Move")]
    [SerializeField] float dashMoveDuration = 0.15f;
    [SerializeField] Ease dashMoveEase = Ease.OutCubic;

    [Header("Dash VFX")]
    [SerializeField] ParticleSystem dashTrailPrefab;
    [SerializeField] float dashTrailHeight = 1f;
    [SerializeField] float dashTrailDestroyDelay = 1.2f;

    private PlanningAction[] playerPlanningList;
    private PlanningAction[] enemyPlanningList;

    Coroutine _highNoonRoutine;
    bool _playerPlanActive;
    bool _enemyPlanActive;
    bool _playerAlive = true;
    bool _enemyAlive = true;
    int _pendingAnimWaits;
    bool _playerDashMidpointReached;
    bool _playerDashFinished;
    bool _skipOpeningDialogue;

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

        playerSpawnRotation = playerPrefab.transform.rotation;
        enemySpawnRotation = enemyPrefab.transform.rotation;

        playerCharacter = Instantiate(playerPrefab, Vector3.zero, playerSpawnRotation);
        enemyCharacter = Instantiate(enemyPrefab, Vector3.zero, enemySpawnRotation);
        enemyConfig.ApplyVisualParts(enemyCharacter);
        CacheCharacterAimTargets();

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
        OrientCombatantsTowardEachOther();

        StartBattle();
    }

    void OrientCombatantsTowardEachOther()
    {
        if (playerCharacter == null || enemyCharacter == null)
            return;

        PlayerController playerController = playerCharacter.GetComponent<PlayerController>();
        PlayerController enemyController = enemyCharacter.GetComponent<PlayerController>();

        if (playerController != null)
            playerController.FaceToward(enemyCharacter.transform.position);

        if (enemyController != null)
            enemyController.FaceToward(playerCharacter.transform.position);
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
        StartCoroutine(StartBattleRoutine());
    }

    IEnumerator StartBattleRoutine()
    {
        if (_skipOpeningDialogue)
        {
            _skipOpeningDialogue = false;
            if (enemyDialogueUI == null)
                enemyDialogueUI = FindFirstObjectByType<EnemyDialogueUI>(FindObjectsInactive.Include);
            if (enemyDialogueUI != null)
                enemyDialogueUI.HideImmediate();
        }
        else
            yield return ShowOpeningRoutine();

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

        HighNoonStarted?.Invoke();
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

            yield return ExecuteMovesForStep(step);

            bool playerShoots = _playerPlanActive && _playerAlive
                && playerPlanningList[step].type == PlanningActionType.Shoot;
            bool enemyShoots = _enemyPlanActive && _enemyAlive
                && enemyPlanningList[step].type == PlanningActionType.Shoot;

            if (!playerShoots && !enemyShoots)
            {
                yield return WaitHighNoon(highNoonStepDelay);
                continue;
            }

            yield return WaitHighNoon(highNoonPreShootDelay);

            bool playerHitEnemy = false;
            bool enemyHitPlayer = false;

            if (playerShoots)
                playerHitEnemy = TryExecuteShoot(true, playerPlanningList[step]);

            if (playerShoots && enemyShoots)
                yield return WaitHighNoon(highNoonShotStagger);

            if (enemyShoots)
                enemyHitPlayer = TryExecuteShoot(false, enemyPlanningList[step]);

            yield return WaitHighNoon(highNoonAfterShotDelay);

            if (playerHitEnemy && enemyHitPlayer)
            {
                OnEnemyHit();
                OnPlayerHit();
                OnDraw();
                _enemyAlive = false;
                _playerAlive = false;
                _enemyPlanActive = false;
                _playerPlanActive = false;
                break;
            }

            if (playerHitEnemy)
            {
                OnEnemyHit();
                _enemyAlive = false;
                _enemyPlanActive = false;
                break;
            }

            if (enemyHitPlayer)
            {
                OnPlayerHit();
                _playerAlive = false;
                _playerPlanActive = false;
                break;
            }
        }

        EndHighNoonPhase();
        _highNoonRoutine = null;
    }

    static IEnumerator WaitHighNoon(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        yield return new WaitForSeconds(seconds);
    }

    IEnumerator ExecuteMovesForStep(int step)
    {
        bool playerWillMove = false;
        bool enemyWillMove = false;
        PlanningAction playerMove = default;
        PlanningAction enemyMove = default;

        if (_playerPlanActive && _playerAlive && IsMoveAction(playerPlanningList[step]))
        {
            playerWillMove = true;
            playerMove = playerPlanningList[step];
        }

        if (_enemyPlanActive && _enemyAlive && IsMoveAction(enemyPlanningList[step]))
        {
            enemyWillMove = true;
            enemyMove = enemyPlanningList[step];
        }

        if (!playerWillMove && !enemyWillMove)
            yield break;

        if (enemyWillMove)
            StartCoroutine(DashMoveEnemySimple(enemyMove));

        if (playerWillMove)
            yield return DashMovePlayer(playerMove);
    }

    IEnumerator DashMovePlayer(PlanningAction move)
    {
        Vector3 from = GetCharacterWorldPos(true);
        Vector3 to = GetMoveTargetWorldPos(true, move);
        ParticleSystem trail = BeginDashTrail(from, to);

        _playerDashMidpointReached = false;
        _playerDashFinished = false;
        CharacterDash?.Invoke(true, move.type);

        yield return WaitUntilPlayerDashMidpoint();

        ApplyMove(true, move);

        bool moveDone = playerCharacter == null;
        LaunchDashTrail(trail, to, dashMoveDuration);
        if (playerCharacter != null)
            MoveCharacterVisual(playerCharacter, from, to, () => moveDone = true);

        while (!moveDone || !_playerDashFinished)
            yield return null;
    }

    IEnumerator DashMoveEnemySimple(PlanningAction move)
    {
        Vector3 from = GetCharacterWorldPos(false);
        Vector3 to = GetMoveTargetWorldPos(false, move);
        ParticleSystem trail = BeginDashTrail(from, to);

        CharacterDash?.Invoke(false, move.type);

        float halfDelay = dashMoveDuration;
        if (halfDelay < 0.05f)
            halfDelay = 0.05f;

        yield return new WaitForSeconds(halfDelay);

        ApplyMove(false, move);
        LaunchDashTrail(trail, to, dashMoveDuration);

        bool moveDone = enemyCharacter == null;
        if (enemyCharacter != null)
            MoveCharacterVisual(enemyCharacter, from, to, () => moveDone = true);

        while (!moveDone)
            yield return null;
    }

    public void NotifyDashMidpoint(bool isPlayerSide)
    {
        if (isPlayerSide)
            _playerDashMidpointReached = true;
    }

    public void NotifyDashFinished(bool isPlayerSide)
    {
        if (isPlayerSide)
            _playerDashFinished = true;
    }

    IEnumerator WaitUntilPlayerDashMidpoint()
    {
        float elapsed = 0f;

        while (elapsed < animWaitTimeout)
        {
            if (_playerDashMidpointReached)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _playerDashMidpointReached = true;
    }

    void MoveCharacterVisual(GameObject character, Vector3 from, Vector3 to, TweenCallback onComplete)
    {
        Transform t = character.transform;
        t.DOKill();
        t.position = from;

        Animator animator = character.GetComponentInChildren<Animator>();
        bool restoreRootMotion = false;
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            restoreRootMotion = true;
        }

        float duration = dashMoveDuration;
        if (duration <= 0f)
        {
            t.position = to;
            if (restoreRootMotion)
                animator.applyRootMotion = true;
            onComplete?.Invoke();
            return;
        }

        t.DOMove(to, duration)
            .SetEase(dashMoveEase)
            .OnComplete(() =>
            {
                if (restoreRootMotion && animator != null)
                    animator.applyRootMotion = true;
                onComplete?.Invoke();
            });
    }

    Vector3 GetCharacterWorldPos(bool isPlayerSide)
    {
        if (isPlayerSide)
        {
            if (playerCharacter != null)
                return playerCharacter.transform.position;

            if (battleMap != null && playerPos >= 0 && playerPos < battleMap.Length)
                return battleMap[playerPos].position;

            return Vector3.zero;
        }

        if (enemyCharacter != null)
            return enemyCharacter.transform.position;

        if (battleMap != null && enemyPos >= 0 && enemyPos < battleMap.Length)
            return battleMap[enemyPos].position;

        return Vector3.zero;
    }

    Vector3 GetMoveTargetWorldPos(bool isPlayerSide, PlanningAction action)
    {
        int currentPos;
        if (isPlayerSide)
            currentPos = playerPos;
        else
            currentPos = enemyPos;

        int nextPos = ApplyActionToPos(currentPos, action);
        if (battleMap == null || nextPos < 0 || nextPos >= battleMap.Length)
            return GetCharacterWorldPos(isPlayerSide);

        return battleMap[nextPos].position;
    }

    ParticleSystem BeginDashTrail(Vector3 from, Vector3 to)
    {
        if (dashTrailPrefab == null)
            return null;

        Vector3 start = from + Vector3.up * dashTrailHeight;
        Vector3 end = to + Vector3.up * dashTrailHeight;
        Vector3 direction = end - start;

        Quaternion rotation = Quaternion.identity;
        if (direction.sqrMagnitude > 0.0001f)
            rotation = Quaternion.LookRotation(direction.normalized);

        ParticleSystem fx = Instantiate(dashTrailPrefab, start, rotation);

        var main = fx.main;
        main.loop = true;
        main.stopAction = ParticleSystemStopAction.None;

        fx.Play(true);
        return fx;
    }

    void LaunchDashTrail(ParticleSystem fx, Vector3 to, float moveDuration)
    {
        if (fx == null)
            return;

        Vector3 end = to + Vector3.up * dashTrailHeight;

        if (!fx.isPlaying)
            fx.Play(true);

        if (moveDuration <= 0f)
        {
            fx.transform.position = end;
            FinishDashTrail(fx);
            return;
        }

        fx.transform
            .DOMove(end, moveDuration)
            .SetEase(dashMoveEase)
            .OnComplete(() => FinishDashTrail(fx));
    }

    void FinishDashTrail(ParticleSystem fx)
    {
        if (fx == null)
            return;

        fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float destroyDelay = dashTrailDestroyDelay;
        if (destroyDelay <= 0f)
        {
            var main = fx.main;
            destroyDelay = main.startLifetime.constantMax + 0.1f;
        }

        Destroy(fx.gameObject, destroyDelay);
    }

    public void BeginAnimWait(int count)
    {
        if (count < 0)
            count = 0;

        _pendingAnimWaits = count;
    }

    public void NotifyAnimFinished()
    {
        _pendingAnimWaits--;
        if (_pendingAnimWaits < 0)
            _pendingAnimWaits = 0;
    }

    IEnumerator WaitForPendingAnims()
    {
        float elapsed = 0f;

        while (_pendingAnimWaits > 0 && elapsed < animWaitTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _pendingAnimWaits = 0;
    }

    static bool IsMoveAction(PlanningAction action)
    {
        if (action.type == PlanningActionType.MoveLeft)
            return true;

        if (action.type == PlanningActionType.MoveRight)
            return true;

        return false;
    }

    void ApplyMove(bool isPlayer, PlanningAction action)
    {
        if (!IsMoveAction(action))
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

    void CacheCharacterAimTargets()
    {
        playerMuzzleTarget = FindNamedChild(playerCharacter, "MuzzleTarget");
        playerHeadTarget = FindNamedChild(playerCharacter, "HeadTarget");
        enemyMuzzleTarget = FindNamedChild(enemyCharacter, "MuzzleTarget");
        enemyHeadTarget = FindNamedChild(enemyCharacter, "HeadTarget");
    }

    static Transform FindNamedChild(GameObject root, string name)
    {
        if (root == null)
            return null;

        Transform direct = root.transform.Find(name);
        if (direct != null)
            return direct;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }

    Vector3 GetMuzzleWorldPos(bool isPlayerShooter)
    {
        Transform muzzle;
        if (isPlayerShooter)
            muzzle = playerMuzzleTarget;
        else
            muzzle = enemyMuzzleTarget;

        if (muzzle != null)
            return muzzle.position;

        int fromTile;
        if (isPlayerShooter)
            fromTile = playerPos;
        else
            fromTile = enemyPos;

        return battleMap[fromTile].position + Vector3.up * shotLineHeight;
    }

    Vector3 GetImpactWorldPos(bool hitCharacter, bool isPlayerShooter, int targetTile)
    {
        if (hitCharacter)
        {
            Transform head;
            if (isPlayerShooter)
                head = enemyHeadTarget;
            else
                head = playerHeadTarget;

            if (head != null)
                return head.position;
        }

        return battleMap[targetTile].position + Vector3.up * shotLineHeight;
    }

    ParticleSystem GetCharacterHitPrefab(bool isPlayerShooter)
    {
        if (isPlayerShooter)
            return enemyHitPrefab;

        if (playerHitPrefab != null)
            return playerHitPrefab;

        return enemyHitPrefab;
    }

    bool TryExecuteShoot(bool isPlayer, PlanningAction action)
    {
        if (action.type != PlanningActionType.Shoot)
            return false;

        if (battleMap == null)
            return false;

        int[] targets = CollectShootTargets(action);
        if (targets.Length == 0)
            return false;

        bool anyHit = false;
        bool announcedShot = false;

        for (int i = 0; i < targets.Length; i++)
        {
            int target = targets[i];
            if (target < 0 || target >= battleMap.Length)
                continue;

            bool hitCharacter;
            if (isPlayer)
                hitCharacter = _enemyAlive && target == enemyPos;
            else
                hitCharacter = _playerAlive && target == playerPos;

            Vector3 impactPos = GetImpactWorldPos(hitCharacter, isPlayer, target);
            Vector3 groundPos = battleMap[target].position + Vector3.up * groundStainHeight;

            if (!announcedShot)
            {
                CharacterShot?.Invoke(isPlayer, impactPos);
                announcedShot = true;
            }

            Vector3 muzzlePos = GetMuzzleWorldPos(isPlayer);

            SpawnShotVfx(muzzleFlashPrefab, muzzlePos, shotVfxDestroyDelay);
            SpawnShotLine(muzzlePos, impactPos);
            PlayShotAudio();
            ShotFired?.Invoke();
            TileShot?.Invoke(target);

            if (hitCharacter)
            {
                Quaternion shotRotation = GetShotRotation(muzzlePos, impactPos);
                SpawnShotVfx(GetCharacterHitPrefab(isPlayer), impactPos, shotRotation, shotVfxDestroyDelay);
                SpawnShotVfx(characterHitExtraPrefab, impactPos, shotVfxDestroyDelay);
                PlayCharacterHitAudio();

                if (!anyHit)
                {
                    bool hitPlayerSide = !isPlayer;
                    Vector3 hitDirection = impactPos - muzzlePos;
                    if (hitDirection.sqrMagnitude < 0.0001f)
                        hitDirection = Vector3.forward;
                    else
                        hitDirection = hitDirection.normalized;

                    CharacterHit?.Invoke(hitPlayerSide, hitDirection);
                    anyHit = true;
                }
            }
            else
            {
                SpawnShotVfx(groundStainPrefab, groundPos, groundStainLifetime);
                SpawnShotVfx(tileImpactPrefab, impactPos, shotVfxDestroyDelay);
                PlayTileImpactAudio();
            }
        }

        return anyHit;
    }

    static int[] CollectShootTargets(PlanningAction action)
    {
        int count = 0;
        if (action.shootTileIndex >= 0) count++;
        if (action.shootTileIndex2 >= 0) count++;
        if (action.shootTileIndex3 >= 0) count++;

        if (count == 0)
            return System.Array.Empty<int>();

        int[] targets = new int[count];
        int write = 0;

        if (action.shootTileIndex >= 0)
        {
            targets[write] = action.shootTileIndex;
            write++;
        }

        if (action.shootTileIndex2 >= 0)
        {
            targets[write] = action.shootTileIndex2;
            write++;
        }

        if (action.shootTileIndex3 >= 0)
            targets[write] = action.shootTileIndex3;

        return targets;
    }

    void SpawnShotLine(Vector3 start, Vector3 end)
    {
        SpawnTracer(bulletCorePrefab, start, end, bulletCoreDuration, enableNoise: false);
        SpawnTracer(bulletSmokePrefab, start, end, bulletSmokeDuration, enableNoise: true);
    }

    static void PlayShotAudio()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Audio != null)
            GameRoot.Instance.Audio.PlayShot();
    }

    static void PlayCharacterHitAudio()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Audio != null)
            GameRoot.Instance.Audio.PlayCharacterHit();
    }

    static void PlayTileImpactAudio()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Audio != null)
            GameRoot.Instance.Audio.PlayTileImpact();
    }

    void SpawnTracer(Tracer prefab, Vector3 start, Vector3 end, float duration, bool enableNoise)
    {
        if (prefab == null)
            return;

        var tracer = Instantiate(prefab);
        tracer.gameObject.SetActive(false);
        tracer.Play(start, end, true);
    }

    static Quaternion GetShotRotation(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction.normalized);
    }

    void SpawnShotVfx(ParticleSystem prefab, Vector3 worldPos, float destroyDelay)
    {
        SpawnShotVfx(prefab, worldPos, Quaternion.identity, destroyDelay);
    }

    void SpawnShotVfx(ParticleSystem prefab, Vector3 worldPos, Quaternion worldRotation, float destroyDelay)
    {
        if (prefab == null)
            return;

        var fx = Instantiate(prefab, worldPos, worldRotation);
        fx.Play(true);
        if (destroyDelay > 0f)
            Destroy(fx.gameObject, destroyDelay);
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
        StartCoroutine(EndHighNoonPhaseRoutine());
    }

    IEnumerator EndHighNoonPhaseRoutine()
    {
        currentState = BattleState.ending;
        WaitingForActionSelection = false;
        HighNoonEnded?.Invoke();

        BattleOutcome outcome;
        if (!_playerAlive && !_enemyAlive)
            outcome = BattleOutcome.Draw;
        else if (_playerAlive && !_enemyAlive)
            outcome = BattleOutcome.PlayerWin;
        else if (!_playerAlive && _enemyAlive)
            outcome = BattleOutcome.EnemyWin;
        else
            outcome = BattleOutcome.NoHits;

        Debug.Log($"High Noon ended: {outcome}");

        if (outcome == BattleOutcome.NoHits)
        {
            yield return RematchAfterNoHitsRoutine();
            yield break;
        }

        BattleResolved?.Invoke(outcome);

        yield return ShowPostBattleDialogueRoutine(outcome == BattleOutcome.PlayerWin);

        if (battleMenuUI == null)
            battleMenuUI = FindFirstObjectByType<BattleMenuUI>();

        bool wonFinalEnemy = outcome == BattleOutcome.PlayerWin
            && GameRoot.Instance != null
            && GameRoot.Instance.Run != null
            && GameRoot.Instance.Run.IsFinalEnemySelected;

        if (battleMenuUI != null && !wonFinalEnemy)
            battleMenuUI.ShowResult(outcome);

        if (GameRoot.Instance != null && GameRoot.Instance.Run != null)
            GameRoot.Instance.Run.OnBattleFinished(outcome == BattleOutcome.PlayerWin);

        if (wonFinalEnemy && battleMenuUI != null)
            yield return battleMenuUI.PlayThanksEndingRoutine();
    }

    IEnumerator RematchAfterNoHitsRoutine()
    {
        if (dayNightLighting == null)
            dayNightLighting = FindFirstObjectByType<DayNightLighting>();

        if (dayNightLighting != null)
            yield return dayNightLighting.PlayFullCycleToHighNoonRoutine();

        ResetBattleForRematch();
        _skipOpeningDialogue = true;
        yield return StartBattleRoutine();
    }

    void ResetBattleForRematch()
    {
        if (_highNoonRoutine != null)
        {
            StopCoroutine(_highNoonRoutine);
            _highNoonRoutine = null;
        }

        if (battleMap != null)
        {
            for (int i = 0; i < battleMap.Length; i++)
                battleMap[i].ownerID = 0;
        }

        playerPos = DefaultPlayerPos;
        enemyPos = DefaultEnemyPos;

        if (battleMap != null)
        {
            if (playerPos >= 0 && playerPos < battleMap.Length)
                battleMap[playerPos].ownerID = 1;
            if (enemyPos >= 0 && enemyPos < battleMap.Length)
                battleMap[enemyPos].ownerID = 2;
        }

        playerPlanningList = new PlanningAction[maxPlanningActions];
        enemyPlanningList = new PlanningAction[maxPlanningActions];
        curPlayerPlaningAction = 0;
        _playerPlanActive = true;
        _enemyPlanActive = true;
        _playerAlive = true;
        _enemyAlive = true;
        WaitingForActionSelection = false;

        if (playerCharacter != null && battleMap != null && playerPos >= 0 && playerPos < battleMap.Length)
            playerCharacter.transform.position = battleMap[playerPos].position;

        if (enemyCharacter != null && battleMap != null && enemyPos >= 0 && enemyPos < battleMap.Length)
            enemyCharacter.transform.position = battleMap[enemyPos].position;

        OrientCombatantsTowardEachOther();

        if (playerTileSelecter != null)
            playerTileSelecter.ClearCommittedPreviews();

        if (enemyDialogueUI == null)
            enemyDialogueUI = FindFirstObjectByType<EnemyDialogueUI>(FindObjectsInactive.Include);
        if (enemyDialogueUI != null)
            enemyDialogueUI.HideImmediate();

        if (dialogueCamera == null)
            dialogueCamera = FindFirstObjectByType<BattleDialogueCamera>();
        if (dialogueCamera != null)
            dialogueCamera.RestoreInstant();
    }

    IEnumerator ShowPostBattleDialogueRoutine(bool playerWon)
    {
        if (enemyDialogueUI == null)
            enemyDialogueUI = FindFirstObjectByType<EnemyDialogueUI>(FindObjectsInactive.Include);

        EnemyConfig enemyConfig = null;
        if (run != null)
            enemyConfig = run.GetEnemySO();

        if (enemyDialogueUI == null || enemyConfig == null)
            yield break;

        string[] lines = enemyConfig.GetPostBattleLines(playerWon);
        if (lines == null || lines.Length == 0)
        {
            enemyDialogueUI.HideImmediate();
            yield break;
        }

        if (!enemyDialogueUI.gameObject.activeSelf)
            enemyDialogueUI.gameObject.SetActive(true);

        yield return FocusDialogueCameraRoutine();
        yield return enemyDialogueUI.PlaySequence(
            enemyConfig.EnemyName,
            lines,
            enemyConfig.TalkSounds,
            enemyConfig.TalkSoundVolume);
        yield return RestoreDialogueCameraRoutine();
    }

    void StartPlanningPhase()
    {
        currentState = BattleState.planning;
        curPlayerPlaningAction = 0;
        if (playerTileSelecter != null)
            playerTileSelecter.ClearCommittedPreviews();
        NotifyPlanningProgress();
        BeginActionSelection();
        FillEnemyPlanningList();
    }

    void NotifyPlanningProgress()
    {
        PlanningProgressChanged?.Invoke(curPlayerPlaningAction, maxPlanningActions);
    }

    IEnumerator ShowOpeningRoutine()
    {
        currentState = BattleState.opening;
        WaitingForActionSelection = false;

        if (enemyDialogueUI == null)
            enemyDialogueUI = FindFirstObjectByType<EnemyDialogueUI>(FindObjectsInactive.Include);

        EnemyConfig enemyConfig = null;
        if (run != null)
            enemyConfig = run.GetEnemySO();

        if (enemyDialogueUI != null)
        {
            if (!enemyDialogueUI.gameObject.activeSelf)
                enemyDialogueUI.gameObject.SetActive(true);

            if (enemyConfig != null && enemyConfig.Lines != null && enemyConfig.Lines.Length > 0)
            {
                yield return FocusDialogueCameraRoutine();
                yield return enemyDialogueUI.PlaySequence(
                    enemyConfig.EnemyName,
                    enemyConfig.Lines,
                    enemyConfig.TalkSounds,
                    enemyConfig.TalkSoundVolume);
                yield return RestoreDialogueCameraRoutine();
            }
            else
                enemyDialogueUI.HideImmediate();
        }
    }

    IEnumerator FocusDialogueCameraRoutine()
    {
        if (dialogueCamera == null)
            dialogueCamera = FindFirstObjectByType<BattleDialogueCamera>();

        if (dialogueCamera == null)
            yield break;

        Transform focus = enemyHeadTarget;
        if (focus == null && enemyCharacter != null)
            focus = enemyCharacter.transform;

        if (focus == null)
            yield break;

        yield return dialogueCamera.FocusRoutine(focus);
    }

    IEnumerator RestoreDialogueCameraRoutine()
    {
        if (dialogueCamera == null)
            dialogueCamera = FindFirstObjectByType<BattleDialogueCamera>();

        if (dialogueCamera == null)
            yield break;

        yield return dialogueCamera.RestoreRoutine();
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
        NotifyPlanningProgress();

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
            StartCoroutine(BeginHighNoonPhaseRoutine());
        }
    }

    IEnumerator BeginHighNoonPhaseRoutine()
    {
        if (planningClock == null)
            planningClock = FindFirstObjectByType<PlanningClockShake>();

        if (planningClock != null)
            yield return planningClock.PlayNoonStrikeRoutine();

        StartHighNoonPhase();
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

            planAction = PlanningAction.CreateShoot(tileIndex);
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

        if (delta < 0)
            planAction = PlanningAction.CreateMove(PlanningActionType.MoveLeft);
        else
            planAction = PlanningAction.CreateMove(PlanningActionType.MoveRight);

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

        // Player-row tiles in 1-based UI terms: 5,6,7,8 => indices 4,5,6,7
        const int tile5 = 4;
        const int tile6 = 5;
        const int tile7 = 6;
        const int tile8 = 7;

        int simPos = enemyPos;

        switch (aiType)
        {
            case EnemyAiType.forwardShotWander:
                for (int i = 0; i < maxPlanningActions; i++)
                {
                    enemyPlanningList[i] = CreateForwardShotOrWander(simPos);
                    simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
                }
                break;

            case EnemyAiType.doubleRandomShot:
                for (int i = 0; i < maxPlanningActions; i++)
                {
                    enemyPlanningList[i] = CreateDoubleSideShotAction(simPos);
                    simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
                }
                break;

            case EnemyAiType.openingShotLane8:
                for (int i = 0; i < maxPlanningActions; i++)
                {
                    if (i == 0)
                        enemyPlanningList[i] = PlanningAction.CreateShoot(tile8);
                    else
                        enemyPlanningList[i] = CreateRandomMove(simPos);

                    simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
                }
                break;

            case EnemyAiType.volleyThenMove:
                for (int i = 0; i < maxPlanningActions; i++)
                {
                    if (i == 0)
                        enemyPlanningList[i] = PlanningAction.CreateShoot(tile5, tile6, tile8);
                    else if (i == 1)
                        enemyPlanningList[i] = PlanningAction.CreateShoot(tile5, tile6, tile7);
                    else
                        enemyPlanningList[i] = CreateRandomMove(simPos);

                    simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
                }
                break;

            default:
                for (int i = 0; i < maxPlanningActions; i++)
                {
                    enemyPlanningList[i] = CreateEnemyAction(aiType, simPos);
                    simPos = ApplyActionToPos(simPos, enemyPlanningList[i]);
                }
                break;
        }
    }

    PlanningAction CreateForwardShotOrWander(int simPos)
    {
        int col = GetColumn(simPos);
        if (Random.value < 0.5f)
            return PlanningAction.CreateShoot(PlayerRowTileFromColumn(col));

        return CreateRandomMove(simPos);
    }

    PlanningAction CreateDoubleSideShotAction(int simPos)
    {
        int col = GetColumn(simPos);

        // Stay off edge columns so both side shots always exist.
        if (col <= 0)
            return PlanningAction.CreateMove(PlanningActionType.MoveRight);

        if (col >= columnsInMap - 1)
            return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

        bool canStepLeft = col > 1;
        bool canStepRight = col < columnsInMap - 2;

        if ((canStepLeft || canStepRight) && Random.value < 0.35f)
        {
            if (canStepLeft && canStepRight)
            {
                if (Random.value < 0.5f)
                    return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

                return PlanningAction.CreateMove(PlanningActionType.MoveRight);
            }

            if (canStepLeft)
                return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

            return PlanningAction.CreateMove(PlanningActionType.MoveRight);
        }

        return PlanningAction.CreateShoot(
            PlayerRowTileFromColumn(col - 1),
            PlayerRowTileFromColumn(col + 1));
    }

    PlanningAction CreateRandomMove(int simPos)
    {
        int col = GetColumn(simPos);
        bool canLeft = col > 0;
        bool canRight = col < columnsInMap - 1;

        if (canLeft && canRight)
        {
            if (Random.value < 0.5f)
                return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

            return PlanningAction.CreateMove(PlanningActionType.MoveRight);
        }

        if (canLeft)
            return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

        if (canRight)
            return PlanningAction.CreateMove(PlanningActionType.MoveRight);

        return PlanningAction.CreateShoot(PlayerRowTileFromColumn(col));
    }

    PlanningAction CreateEnemyAction(EnemyAiType aiType, int simPos)
    {
        int col = GetColumn(simPos);
        bool canLeft = col > 0;
        bool canRight = col < columnsInMap - 1;

        switch (aiType)
        {
            case EnemyAiType.rightEye:
            {
                bool canShootEnemyRight = col > 0;
                if (canShootEnemyRight && (Random.value < 0.5f || !canLeft))
                {
                    int shootCol = Random.Range(0, col);
                    return PlanningAction.CreateShoot(PlayerRowTileFromColumn(shootCol));
                }

                if (canLeft)
                    return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

                if (canRight)
                    return PlanningAction.CreateMove(PlanningActionType.MoveRight);

                return PlanningAction.CreateShoot(PlayerRowTileFromColumn(Mathf.Max(col - 1, 0)));
            }

            case EnemyAiType.leftLeg:
                if (Random.value < 0.5f || !canLeft)
                    return PlanningAction.CreateShoot(PlayerRowTileFromColumn(col));

                return PlanningAction.CreateMove(PlanningActionType.MoveLeft);

            case EnemyAiType.random:
            default:
                if (Random.value < 0.5f)
                {
                    return PlanningAction.CreateShoot(
                        PlayerRowTileFromColumn(Random.Range(0, columnsInMap)));
                }

                return CreateRandomMove(simPos);
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
