using System.Collections;
using UnityEngine;

public class AnimationMotionFx : MonoBehaviour
{
    [SerializeField] bool isPlayer = true;
    [SerializeField] Transform motionTarget;
    [SerializeField] float dashSpinDuration = 0.55f;
    [SerializeField] float dashSpinDegreesPerSecond = 1080f;

    BattleManager _battle;
    Animator _animator;
    Quaternion _originLocalRot;
    float _spinAngle;
    bool _spinning;
    bool _restoreRootMotion;
    Coroutine _dashRoutine;

    void Awake()
    {
        if (motionTarget == null)
            motionTarget = transform;

        _animator = GetComponentInChildren<Animator>();
        _originLocalRot = motionTarget.localRotation;
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
        StopDashFx(resetPose: true);
        Unsubscribe();
    }

    void LateUpdate()
    {
        if (!_spinning || motionTarget == null)
            return;

        _spinAngle += dashSpinDegreesPerSecond * Time.deltaTime;
        motionTarget.localRotation = _originLocalRot * Quaternion.AngleAxis(_spinAngle, Vector3.up);
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.CharacterDash += OnCharacterDash;
    }

    void Unsubscribe()
    {
        if (_battle == null)
            return;

        _battle.CharacterDash -= OnCharacterDash;
        _battle = null;
    }

    void OnCharacterDash(bool movedIsPlayer, PlanningActionType actionType)
    {
        if (movedIsPlayer != isPlayer)
            return;

        if (_dashRoutine != null)
            StopCoroutine(_dashRoutine);

        _dashRoutine = StartCoroutine(PlayDashFx());
    }

    IEnumerator PlayDashFx()
    {
        BeginSpin();

        float duration = dashSpinDuration;
        if (duration < 0.05f)
            duration = 0.05f;

        yield return new WaitForSeconds(duration);

        StopDashFx(resetPose: true);
        _dashRoutine = null;
    }

    void BeginSpin()
    {
        if (motionTarget == null)
            return;

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _restoreRootMotion = false;
        if (_animator != null && _animator.applyRootMotion)
        {
            _animator.applyRootMotion = false;
            _restoreRootMotion = true;
        }

        _originLocalRot = motionTarget.localRotation;
        _spinAngle = 0f;
        _spinning = true;
    }

    void StopDashFx(bool resetPose)
    {
        _spinning = false;
        _spinAngle = 0f;

        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
            _dashRoutine = null;
        }

        if (_restoreRootMotion && _animator != null)
            _animator.applyRootMotion = true;

        _restoreRootMotion = false;

        if (!resetPose || motionTarget == null)
            return;

        motionTarget.localRotation = _originLocalRot;
    }
}
