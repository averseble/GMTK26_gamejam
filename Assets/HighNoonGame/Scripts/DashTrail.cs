using DG.Tweening;
using UnityEngine;

public class DashTrail : MonoBehaviour
{
    [SerializeField] ParticleSystem particles;
    [SerializeField] float stopEmitGrace = 0.35f;
    [SerializeField] Ease moveEase = Ease.OutCubic;

    Tween _moveTween;

    void Awake()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();

        if (particles == null)
            particles = GetComponentInChildren<ParticleSystem>();
    }

    public void Play(Vector3 from, Vector3 to, float duration)
    {
        transform.position = from;

        Vector3 direction = to - from;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized);

        if (particles != null)
            particles.Play(true);

        if (_moveTween != null && _moveTween.IsActive())
            _moveTween.Kill();

        if (duration <= 0f)
        {
            transform.position = to;
            StopAndDestroy();
            return;
        }

        _moveTween = transform
            .DOMove(to, duration)
            .SetEase(moveEase)
            .OnComplete(StopAndDestroy);
    }

    void StopAndDestroy()
    {
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float destroyDelay = stopEmitGrace;
        if (particles != null)
        {
            var main = particles.main;
            destroyDelay = Mathf.Max(stopEmitGrace, main.startLifetime.constantMax);
        }

        Destroy(gameObject, destroyDelay);
    }

    void OnDisable()
    {
        if (_moveTween != null && _moveTween.IsActive())
            _moveTween.Kill();

        _moveTween = null;
    }
}
