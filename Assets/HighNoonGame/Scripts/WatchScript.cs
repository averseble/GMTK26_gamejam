using DG.Tweening;
using UnityEngine;

public class WatchScript : MonoBehaviour
{
    [SerializeField] Transform hand;
    [SerializeField] float degreesPerHour = -30f;
    [SerializeField] float rotateDuration = 0.28f;
    [SerializeField] Ease rotateEase = Ease.OutCubic;

    [Header("High Noon Spin")]
    [SerializeField] float spinSecondsPerTurn = 0.12f;
    [SerializeField] Ease spinEase = Ease.Linear;

    BattleManager _battle;
    Tween _rotateTween;

    void Awake()
    {
        if (hand == null)
            hand = transform;
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
        RefreshFromBattle(instant: true);
    }

    void OnDisable()
    {
        Unsubscribe();
        KillTween();
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.PlanningProgressChanged += OnPlanningProgressChanged;
        _battle.HighNoonStarted += OnHighNoonStarted;
    }

    void Unsubscribe()
    {
        if (_battle != null)
        {
            _battle.PlanningProgressChanged -= OnPlanningProgressChanged;
            _battle.HighNoonStarted -= OnHighNoonStarted;
        }

        _battle = null;
    }

    void OnPlanningProgressChanged(int actionsDone, int actionsMax)
    {
        SetProgress(actionsDone, actionsMax, instant: false);
    }

    void OnHighNoonStarted()
    {
        if (_rotateTween != null && _rotateTween.IsActive() && _rotateTween.IsPlaying())
        {
            _rotateTween.OnComplete(StartWildSpin);
            return;
        }

        StartWildSpin();
    }

    void RefreshFromBattle(bool instant)
    {
        if (_battle == null && !BattleManager.TryGetInstance(out _battle))
            return;

        SetProgress(_battle.curPlayerPlaningAction, _battle.maxPlanningActions, instant);
    }

    public void SetProgress(int actionsDone, int actionsMax, bool instant = false)
    {
        if (hand == null)
            return;

        if (actionsMax < 0)
            actionsMax = 0;

        if (actionsDone < 0)
            actionsDone = 0;

        if (actionsDone > actionsMax)
            actionsDone = actionsMax;

        int hoursRemaining = actionsMax - actionsDone;
        float targetZ = degreesPerHour * hoursRemaining;

        KillTween();

        Vector3 targetEuler = new Vector3(0f, 0f, targetZ);

        if (instant || rotateDuration <= 0f)
        {
            hand.localEulerAngles = targetEuler;
            return;
        }

        _rotateTween = hand
            .DOLocalRotate(targetEuler, rotateDuration, RotateMode.Fast)
            .SetEase(rotateEase);
    }

    void StartWildSpin()
    {
        if (hand == null)
            return;

        KillTween();

        float direction;
        if (degreesPerHour < 0f)
            direction = -360f;
        else
            direction = 360f;

        float duration = spinSecondsPerTurn;
        if (duration <= 0f)
            duration = 0.12f;

        _rotateTween = hand
            .DOLocalRotate(new Vector3(0f, 0f, -direction), duration, RotateMode.LocalAxisAdd)
            .SetEase(spinEase)
            .SetLoops(-1, LoopType.Restart);
    }

    void KillTween()
    {
        if (_rotateTween != null && _rotateTween.IsActive())
            _rotateTween.Kill();

        _rotateTween = null;
    }
}
