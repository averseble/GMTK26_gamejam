using DG.Tweening;
using UnityEngine;

public class BackgroundOrbit : MonoBehaviour
{
    public enum OrbitPlane
    {
        XY,
        XZ,
        YZ
    }

    [SerializeField] Transform target;
    [SerializeField] bool useLocalSpace = true;
    [SerializeField] OrbitPlane plane = OrbitPlane.XY;
    [SerializeField] float radius = 0.35f;
    [SerializeField] float secondsPerLoop = 40f;
    [SerializeField] float startAngleDegrees;
    [SerializeField] bool clockwise;
    [SerializeField] bool playOnEnable = true;
    [SerializeField] Ease ease = Ease.Linear;

    Vector3 _center;
    Tween _orbitTween;

    void Awake()
    {
        if (target == null)
            target = transform;

        CaptureCenter();
    }

    void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        Stop();
        CaptureCenter();

        float duration = secondsPerLoop;
        if (duration < 0.01f)
            duration = 0.01f;

        float from = startAngleDegrees;
        float to = startAngleDegrees + 360f;
        if (clockwise)
            to = startAngleDegrees - 360f;

        float angle = from;
        ApplyAngle(angle);

        _orbitTween = DOTween
            .To(() => angle, value =>
            {
                angle = value;
                ApplyAngle(angle);
            }, to, duration)
            .SetEase(ease)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(UpdateType.Normal)
            .SetLink(gameObject);
    }

    public void Stop()
    {
        if (_orbitTween != null && _orbitTween.IsActive())
            _orbitTween.Kill();

        _orbitTween = null;
    }

    void CaptureCenter()
    {
        if (target == null)
            return;

        if (useLocalSpace)
            _center = target.localPosition;
        else
            _center = target.position;
    }

    void ApplyAngle(float degrees)
    {
        if (target == null)
            return;

        float rad = degrees * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad) * radius;
        float y = Mathf.Sin(rad) * radius;
        Vector3 offset;

        if (plane == OrbitPlane.XY)
            offset = new Vector3(x, y, 0f);
        else if (plane == OrbitPlane.XZ)
            offset = new Vector3(x, 0f, y);
        else
            offset = new Vector3(0f, x, y);

        if (useLocalSpace)
            target.localPosition = _center + offset;
        else
            target.position = _center + offset;
    }
}
