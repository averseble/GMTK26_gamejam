using System;
using DG.Tweening;
using UnityEngine;

public class WantedBoardCamera : MonoBehaviour
{
    [SerializeField] float moveDuration = 0.55f;
    [SerializeField] Ease moveEase = Ease.InOutCubic;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Confirm Approach")]
    [SerializeField] float approachDuration = 0.4f;
    [SerializeField] float approachOrthoSize = 3.2f;
    [SerializeField] float approachFieldOfView = 35f;
    [SerializeField] Ease approachEase = Ease.InOutSine;

    Camera _camera;
    Tween _moveTween;
    Tween _rotateTween;
    Tween _lensTween;

    float _defaultOrthoSize;
    float _defaultFieldOfView;
    bool _defaultsCached;

    public bool IsMoving
    {
        get
        {
            if (_moveTween != null && _moveTween.IsActive() && _moveTween.IsPlaying())
                return true;

            if (_rotateTween != null && _rotateTween.IsActive() && _rotateTween.IsPlaying())
                return true;

            return false;
        }
    }

    public bool IsApproaching
    {
        get { return _lensTween != null && _lensTween.IsActive() && _lensTween.IsPlaying(); }
    }

    public bool IsBusy
    {
        get { return IsMoving || IsApproaching; }
    }

    public event Action Arrived;

    void Awake()
    {
        CacheCameraDefaults();
    }

    public void Focus(Transform anchor, bool instant = false)
    {
        if (anchor == null)
            return;

        KillMoveTweens();
        RestoreLensInstant();

        if (instant || moveDuration <= 0f)
        {
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            Arrived?.Invoke();
            return;
        }

        _moveTween = transform
            .DOMove(anchor.position, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(useUnscaledTime);

        _rotateTween = transform
            .DORotateQuaternion(anchor.rotation, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() => Arrived?.Invoke());
    }

    public void PlayApproach(Action onComplete = null)
    {
        CacheCameraDefaults();
        KillLensTween();

        if (_camera == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (approachDuration <= 0f)
        {
            if (_camera.orthographic)
                _camera.orthographicSize = approachOrthoSize;
            else
                _camera.fieldOfView = approachFieldOfView;

            onComplete?.Invoke();
            return;
        }

        if (_camera.orthographic)
        {
            _lensTween = _camera
                .DOOrthoSize(approachOrthoSize, approachDuration)
                .SetEase(approachEase)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            _lensTween = _camera
                .DOFieldOfView(approachFieldOfView, approachDuration)
                .SetEase(approachEase)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onComplete?.Invoke());
        }
    }

    void OnDisable()
    {
        KillMoveTweens();
        KillLensTween();
    }

    void CacheCameraDefaults()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();

        if (_camera == null || _defaultsCached)
            return;

        _defaultOrthoSize = _camera.orthographicSize;
        _defaultFieldOfView = _camera.fieldOfView;
        _defaultsCached = true;
    }

    void RestoreLensInstant()
    {
        KillLensTween();
        CacheCameraDefaults();

        if (_camera == null || !_defaultsCached)
            return;

        if (_camera.orthographic)
            _camera.orthographicSize = _defaultOrthoSize;
        else
            _camera.fieldOfView = _defaultFieldOfView;
    }

    void KillMoveTweens()
    {
        if (_moveTween != null && _moveTween.IsActive())
            _moveTween.Kill();

        if (_rotateTween != null && _rotateTween.IsActive())
            _rotateTween.Kill();

        _moveTween = null;
        _rotateTween = null;
    }

    void KillLensTween()
    {
        if (_lensTween != null && _lensTween.IsActive())
            _lensTween.Kill();

        _lensTween = null;
    }
}
