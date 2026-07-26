using DG.Tweening;
using UnityEngine;

public class BattleCameraShake : MonoBehaviour
{
    [SerializeField] float duration = 0.18f;
    [SerializeField] float strength = 0.22f;
    [SerializeField] int vibrato = 18;
    [SerializeField] float randomness = 90f;
    [SerializeField] bool snapping = false;
    [SerializeField] bool fadeOut = true;

    Vector3 _localOrigin;
    Tween _shakeTween;
    BattleManager _battle;

    void Awake()
    {
        CaptureLocalOrigin();
    }

    public void CaptureLocalOrigin()
    {
        _localOrigin = transform.localPosition;
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        KillShake(resetPosition: true);
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.ShotFired += OnShotFired;
    }

    void Unsubscribe()
    {
        if (_battle != null)
            _battle.ShotFired -= OnShotFired;

        _battle = null;
    }

    void OnShotFired()
    {
        KillShake(resetPosition: true);

        _shakeTween = transform
            .DOShakePosition(duration, strength, vibrato, randomness, snapping, fadeOut)
            .SetUpdate(UpdateType.Late)
            .OnComplete(() => transform.localPosition = _localOrigin);
    }

    void KillShake(bool resetPosition)
    {
        if (_shakeTween != null && _shakeTween.IsActive())
            _shakeTween.Kill();

        _shakeTween = null;

        if (resetPosition)
            transform.localPosition = _localOrigin;
    }
}
