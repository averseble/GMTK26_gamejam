using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int ShootHash = Animator.StringToHash("Shoot");
    static readonly int VictoryHash = Animator.StringToHash("Victory");
    static readonly int IsDashingHash = Animator.StringToHash("IsDashing");

    [SerializeField] bool isPlayer = true;
    [SerializeField] Animator animator;
    [SerializeField] string dashStateName = "Armature_CrauchDash";
    [SerializeField] int animatorLayer;
    [SerializeField] float dashCrossFade = 0.04f;
    [SerializeField] [Range(0.05f, 0.95f)] float dashMoveAtNormalized = 0.5f;
    [SerializeField] float dashFallbackDuration = 0.45f;

    [Header("Shoot Aim")]
    [SerializeField] float shootTurnDuration = 0.12f;
    [SerializeField] Ease shootTurnEase = Ease.OutQuad;
    [SerializeField] float shootAimYawOffset = 90f;

    [Header("Ragdoll")]
    [SerializeField] Transform headTarget;
    [SerializeField] Rigidbody headRigidbody;
    [SerializeField] float headHitForce = 28f;
    [SerializeField] float headHitUpForce = 6f;
    [SerializeField] ForceMode headHitForceMode = ForceMode.Impulse;

    BattleManager _battle;
    bool _isDead;
    Coroutine _dashRoutine;
    Tween _shootTurnTween;
    Rigidbody[] _ragdollBodies;
    Collider[] _ragdollColliders;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (headTarget == null)
            headTarget = FindNamedChild(transform, "HeadTarget");

        CacheRagdoll();
        if (_ragdollBodies != null && _ragdollBodies.Length > 0)
            SetRagdollActive(false);
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
        StopDashRoutine();
        KillShootTurn();
        Unsubscribe();
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.CharacterDash += OnDash;
        _battle.CharacterShot += OnCharacterShot;
        _battle.CharacterHit += OnCharacterHit;
        _battle.BattleResolved += OnBattleResolved;
    }

    void Unsubscribe()
    {
        if (_battle == null)
            return;

        _battle.CharacterDash -= OnDash;
        _battle.CharacterShot -= OnCharacterShot;
        _battle.CharacterHit -= OnCharacterHit;
        _battle.BattleResolved -= OnBattleResolved;
        _battle = null;
    }

    void CacheRagdoll()
    {
        _ragdollBodies = GetComponentsInChildren<Rigidbody>(true);

        var colliders = new List<Collider>();
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            Rigidbody body = _ragdollBodies[i];
            if (body == null)
                continue;

            Collider[] bodyColliders = body.GetComponents<Collider>();
            for (int c = 0; c < bodyColliders.Length; c++)
            {
                if (bodyColliders[c] != null)
                    colliders.Add(bodyColliders[c]);
            }
        }

        _ragdollColliders = colliders.ToArray();

        if (headRigidbody == null && headTarget != null)
            headRigidbody = headTarget.GetComponentInParent<Rigidbody>();

        if (headRigidbody == null)
            headRigidbody = FindHeadRigidbody();
    }

    Rigidbody FindHeadRigidbody()
    {
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            Rigidbody body = _ragdollBodies[i];
            if (body == null)
                continue;

            string name = body.gameObject.name;
            if (name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return body;
        }

        return null;
    }

    void SetRagdollActive(bool active)
    {
        if (_ragdollBodies != null)
        {
            for (int i = 0; i < _ragdollBodies.Length; i++)
            {
                Rigidbody body = _ragdollBodies[i];
                if (body == null)
                    continue;

                body.isKinematic = !active;
                body.detectCollisions = active;

                if (active)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        if (_ragdollColliders != null)
        {
            for (int i = 0; i < _ragdollColliders.Length; i++)
            {
                Collider col = _ragdollColliders[i];
                if (col != null)
                    col.enabled = active;
            }
        }

        if (animator != null)
            animator.enabled = !active;
    }

    void ActivateRagdoll(Vector3 hitDirection)
    {
        KillShootTurn();
        transform.DOKill();

        if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            return;

        SetRagdollActive(true);
        StartCoroutine(ApplyHeadHitForce(hitDirection));
    }

    IEnumerator ApplyHeadHitForce(Vector3 hitDirection)
    {
        yield return new WaitForFixedUpdate();

        if (headRigidbody == null)
            yield break;

        Vector3 dir = hitDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        else
            dir = dir.normalized;

        Vector3 force = dir * headHitForce + Vector3.up * headHitUpForce;
        headRigidbody.AddForce(force, headHitForceMode);
    }

    void OnDash(bool movedIsPlayer, PlanningActionType actionType)
    {
        if (movedIsPlayer != isPlayer)
            return;

        if (!isPlayer)
            return;

        if (_isDead || animator == null)
        {
            NotifyMidpoint();
            NotifyFinished();
            return;
        }

        StopDashRoutine();
        _dashRoutine = StartCoroutine(PlayDash());
    }

    IEnumerator PlayDash()
    {
        SetBool(IsDashingHash, true);
        animator.ResetTrigger(DashHash);
        animator.SetTrigger(DashHash);
        animator.CrossFadeInFixedTime(dashStateName, dashCrossFade, animatorLayer, 0f);

        yield return null;

        int stateHash = Animator.StringToHash(dashStateName);
        float moveAt = Mathf.Clamp(dashMoveAtNormalized, 0.05f, 0.95f);
        float elapsed = 0f;
        float timeout = dashFallbackDuration;
        if (_battle != null)
            timeout = Mathf.Max(dashFallbackDuration, _battle.AnimWaitTimeout);

        bool entered = false;
        bool midpointSent = false;

        while (elapsed < timeout)
        {
            if (IsInState(stateHash))
            {
                entered = true;
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(animatorLayer);
                float normalized = info.normalizedTime;

                if (!midpointSent && normalized >= moveAt)
                {
                    midpointSent = true;
                    NotifyMidpoint();
                }

                if (normalized >= 1f && !animator.IsInTransition(animatorLayer))
                    break;
            }
            else if (entered && !animator.IsInTransition(animatorLayer))
            {
                break;
            }
            else if (!entered && !midpointSent && elapsed >= dashFallbackDuration * moveAt)
            {
                midpointSent = true;
                NotifyMidpoint();
            }

            if (!entered && elapsed >= dashFallbackDuration)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!midpointSent)
            NotifyMidpoint();

        SetBool(IsDashingHash, false);
        NotifyFinished();
        _dashRoutine = null;
    }

    bool IsInState(int stateHash)
    {
        if (animator == null)
            return false;

        if (animator.IsInTransition(animatorLayer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(animatorLayer);
            if (next.shortNameHash == stateHash)
                return true;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        return current.shortNameHash == stateHash;
    }

    void NotifyMidpoint()
    {
        if (_battle != null)
            _battle.NotifyDashMidpoint(isPlayer);
    }

    void NotifyFinished()
    {
        if (_battle != null)
            _battle.NotifyDashFinished(isPlayer);
    }

    void StopDashRoutine()
    {
        if (_dashRoutine == null)
            return;

        StopCoroutine(_dashRoutine);
        _dashRoutine = null;
    }

    void OnCharacterShot(bool shooterIsPlayer, Vector3 aimWorldPos)
    {
        if (shooterIsPlayer != isPlayer)
            return;

        if (_isDead)
            return;

        FaceAim(aimWorldPos);
        PlayTrigger(ShootHash);
    }

    void FaceAim(Vector3 aimWorldPos)
    {
        Vector3 toAim = aimWorldPos - transform.position;
        toAim.y = 0f;

        if (toAim.sqrMagnitude < 0.0001f)
            return;

        // Mesh is authored ~ -90° yaw under the root, so compensate LookRotation.
        Quaternion targetRotation = Quaternion.LookRotation(toAim.normalized, Vector3.up)
            * Quaternion.Euler(0f, shootAimYawOffset, 0f);
        KillShootTurn();

        float duration = shootTurnDuration;
        if (duration <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        _shootTurnTween = transform
            .DORotateQuaternion(targetRotation, duration)
            .SetEase(shootTurnEase)
            .SetUpdate(UpdateType.Late);
    }

    void KillShootTurn()
    {
        if (_shootTurnTween != null && _shootTurnTween.IsActive())
            _shootTurnTween.Kill();

        _shootTurnTween = null;
    }

    void OnCharacterHit(bool hitPlayer, Vector3 hitDirection)
    {
        if (hitPlayer != isPlayer)
            return;

        if (_isDead)
            return;

        _isDead = true;
        bool wasDashing = _dashRoutine != null;
        StopDashRoutine();
        SetBool(IsDashingHash, false);

        if (wasDashing)
        {
            NotifyMidpoint();
            NotifyFinished();
        }

        ActivateRagdoll(hitDirection);
    }

    void OnBattleResolved(BattleOutcome outcome)
    {
        if (_isDead)
            return;

        if (isPlayer && outcome == BattleOutcome.PlayerWin)
        {
            PlayTrigger(VictoryHash);
            return;
        }

        if (!isPlayer && outcome == BattleOutcome.EnemyWin)
            PlayTrigger(VictoryHash);
    }

    void PlayTrigger(int hash)
    {
        if (animator == null || !animator.enabled)
            return;

        animator.SetTrigger(hash);
    }

    void SetBool(int hash, bool value)
    {
        if (animator == null || !animator.enabled)
            return;

        animator.SetBool(hash, value);
    }

    static Transform FindNamedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedChild(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
