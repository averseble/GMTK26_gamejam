using System.Collections;
using DG.Tweening;
using UnityEngine;

public class BattleDialogueCamera : MonoBehaviour
{
    [SerializeField] float duration = 0.55f;
    [SerializeField] Ease ease = Ease.InOutSine;
    [SerializeField] float approachOrthoSize = 3.85f;
    [SerializeField] float approachFieldOfView = 18f;
    [Range(0f, 1f)]
    [SerializeField] float positionPull = 0.22f;
    [Range(0f, 1f)]
    [SerializeField] float lookBlend = 0.35f;
    [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] bool useUnscaledTime;

    Camera _camera;
    BattleCameraShake _shake;
    Tween _focusTween;

    Vector3 _defaultPosition;
    Quaternion _defaultRotation;
    float _defaultOrthoSize;
    float _defaultFieldOfView;
    bool _defaultsCached;
    bool _focused;

    void Awake()
    {
        CacheDefaults();
    }

    void OnDisable()
    {
        KillTweens();
        RestoreInstant();
    }

    public IEnumerator FocusRoutine(Transform lookTarget)
    {
        CacheDefaults();

        if (lookTarget == null || duration <= 0f)
        {
            FocusInstant(lookTarget);
            yield break;
        }

        KillTweens();

        Vector3 lookPoint = lookTarget.position + lookOffset;
        Vector3 focusPos = Vector3.Lerp(_defaultPosition, lookPoint, positionPull);
        Quaternion focusRot = BuildFocusRotation(focusPos, lookPoint);

        Sequence seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
        seq.Join(transform.DOMove(focusPos, duration).SetEase(ease));
        seq.Join(transform.DORotateQuaternion(focusRot, duration).SetEase(ease));

        if (_camera != null)
        {
            if (_camera.orthographic)
                seq.Join(_camera.DOOrthoSize(approachOrthoSize, duration).SetEase(ease));
            else
                seq.Join(_camera.DOFieldOfView(approachFieldOfView, duration).SetEase(ease));
        }

        _focusTween = seq;
        yield return seq.WaitForCompletion();
        _focused = true;
        NotifyShakeOrigin();
    }

    public IEnumerator RestoreRoutine()
    {
        CacheDefaults();

        if (!_focused && !IsAnimating())
            yield break;

        if (duration <= 0f)
        {
            RestoreInstant();
            yield break;
        }

        KillTweens();

        Sequence seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
        seq.Join(transform.DOMove(_defaultPosition, duration).SetEase(ease));
        seq.Join(transform.DORotateQuaternion(_defaultRotation, duration).SetEase(ease));

        if (_camera != null)
        {
            if (_camera.orthographic)
                seq.Join(_camera.DOOrthoSize(_defaultOrthoSize, duration).SetEase(ease));
            else
                seq.Join(_camera.DOFieldOfView(_defaultFieldOfView, duration).SetEase(ease));
        }

        _focusTween = seq;
        yield return seq.WaitForCompletion();
        _focused = false;
        NotifyShakeOrigin();
    }

    public void FocusInstant(Transform lookTarget)
    {
        CacheDefaults();
        KillTweens();

        if (lookTarget == null)
            return;

        Vector3 lookPoint = lookTarget.position + lookOffset;
        Vector3 focusPos = Vector3.Lerp(_defaultPosition, lookPoint, positionPull);
        transform.SetPositionAndRotation(focusPos, BuildFocusRotation(focusPos, lookPoint));

        if (_camera != null)
        {
            if (_camera.orthographic)
                _camera.orthographicSize = approachOrthoSize;
            else
                _camera.fieldOfView = approachFieldOfView;
        }

        _focused = true;
        NotifyShakeOrigin();
    }

    public void RestoreInstant()
    {
        KillTweens();
        CacheDefaults();

        transform.SetPositionAndRotation(_defaultPosition, _defaultRotation);

        if (_camera != null)
        {
            if (_camera.orthographic)
                _camera.orthographicSize = _defaultOrthoSize;
            else
                _camera.fieldOfView = _defaultFieldOfView;
        }

        _focused = false;
        NotifyShakeOrigin();
    }

    Quaternion BuildFocusRotation(Vector3 from, Vector3 lookPoint)
    {
        Vector3 forward = lookPoint - from;
        if (forward.sqrMagnitude < 0.0001f)
            return _defaultRotation;

        Quaternion lookRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return Quaternion.Slerp(_defaultRotation, lookRot, lookBlend);
    }

    void CacheDefaults()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();

        if (_shake == null)
            _shake = GetComponent<BattleCameraShake>();

        if (_defaultsCached)
            return;

        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;

        if (_camera != null)
        {
            _defaultOrthoSize = _camera.orthographicSize;
            _defaultFieldOfView = _camera.fieldOfView;
        }

        _defaultsCached = true;
    }

    void NotifyShakeOrigin()
    {
        if (_shake != null)
            _shake.CaptureLocalOrigin();
    }

    bool IsAnimating()
    {
        return _focusTween != null && _focusTween.IsActive() && _focusTween.IsPlaying();
    }

    void KillTweens()
    {
        if (_focusTween != null && _focusTween.IsActive())
            _focusTween.Kill();

        _focusTween = null;
    }
}
