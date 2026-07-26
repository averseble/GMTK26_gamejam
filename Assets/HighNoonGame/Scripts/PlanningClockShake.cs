using System.Collections;
using DG.Tweening;
using UnityEngine;

public class PlanningClockShake : MonoBehaviour
{
    [Header("Move Shake")]
    [SerializeField] Transform target;
    [SerializeField] float duration = 0.28f;
    [SerializeField] Vector3 positionStrength = new Vector3(0.03f, 0.05f, 0.03f);
    [SerializeField] Vector3 rotationStrength = new Vector3(4f, 4f, 10f);
    [SerializeField] int vibrato = 18;
    [SerializeField] float randomness = 90f;
    [SerializeField] bool fadeOut = true;
    [SerializeField] bool shakePosition = true;
    [SerializeField] bool shakeRotation = true;
    [SerializeField] AudioClip moveConfirmClip;
    [SerializeField] [Range(0f, 2f)] float moveConfirmVolume = 1f;

    [Header("Noon Strike")]
    [SerializeField] float noonDuration = 0.85f;
    [SerializeField] Vector3 noonPositionStrength = new Vector3(0.08f, 0.14f, 0.08f);
    [SerializeField] Vector3 noonRotationStrength = new Vector3(10f, 10f, 22f);
    [SerializeField] int noonVibrato = 28;
    [SerializeField] float noonDelay = 0.9f;
    [SerializeField] AudioClip noonClockClip;
    [SerializeField] [Range(0f, 2f)] float noonClockVolume = 1f;

    Vector3 _localOrigin;
    Quaternion _localRotationOrigin;
    Tween _positionTween;
    Tween _rotationTween;
    BattleManager _battle;

    void Awake()
    {
        if (target == null)
            target = transform;

        CaptureOrigin();
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
        KillShake(resetTransform: true);
    }

    void CaptureOrigin()
    {
        if (target == null)
            return;

        _localOrigin = target.localPosition;
        _localRotationOrigin = target.localRotation;
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
    }

    void Unsubscribe()
    {
        if (_battle != null)
            _battle.PlanningProgressChanged -= OnPlanningProgressChanged;

        _battle = null;
    }

    void OnPlanningProgressChanged(int current, int max)
    {
        if (current <= 0)
            return;

        PlayMoveConfirmSound();

        // Last action uses the stronger noon strike from BattleManager.
        if (max > 0 && current >= max)
            return;

        PlayShake();
    }

    void PlayMoveConfirmSound()
    {
        if (moveConfirmClip == null)
            return;

        if (GameRoot.Instance == null || GameRoot.Instance.Audio == null)
            return;

        GameRoot.Instance.Audio.PlaySfx(moveConfirmClip, moveConfirmVolume);
    }

    public IEnumerator PlayNoonStrikeRoutine()
    {
        PlayNoonClockSound();

        PlayShake(
            noonDuration,
            noonPositionStrength,
            noonRotationStrength,
            noonVibrato);

        float wait = noonDelay;
        if (wait < noonDuration)
            wait = noonDuration;

        if (noonClockClip != null && noonClockClip.length > wait)
            wait = noonClockClip.length;

        if (wait < 0f)
            wait = 0f;

        if (wait > 0f)
            yield return new WaitForSeconds(wait);
    }

    public void PlayShake()
    {
        PlayShake(duration, positionStrength, rotationStrength, vibrato);
    }

    void PlayShake(float shakeDuration, Vector3 posStrength, Vector3 rotStrength, int shakeVibrato)
    {
        if (target == null)
            return;

        KillShake(resetTransform: true);

        if (shakePosition)
        {
            _positionTween = target
                .DOShakePosition(shakeDuration, posStrength, shakeVibrato, randomness, false, fadeOut)
                .SetUpdate(UpdateType.Late)
                .OnComplete(() => target.localPosition = _localOrigin);
        }

        if (shakeRotation)
        {
            _rotationTween = target
                .DOShakeRotation(shakeDuration, rotStrength, shakeVibrato, randomness, fadeOut)
                .SetUpdate(UpdateType.Late)
                .OnComplete(() => target.localRotation = _localRotationOrigin);
        }
    }

    void PlayNoonClockSound()
    {
        if (noonClockClip == null)
            return;

        if (GameRoot.Instance == null || GameRoot.Instance.Audio == null)
            return;

        GameRoot.Instance.Audio.PlaySfx(noonClockClip, noonClockVolume);
    }

    void KillShake(bool resetTransform)
    {
        if (_positionTween != null && _positionTween.IsActive())
            _positionTween.Kill();

        if (_rotationTween != null && _rotationTween.IsActive())
            _rotationTween.Kill();

        _positionTween = null;
        _rotationTween = null;

        if (resetTransform && target != null)
        {
            target.localPosition = _localOrigin;
            target.localRotation = _localRotationOrigin;
        }
    }
}
