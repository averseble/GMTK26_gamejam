using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public class DayNightLighting : MonoBehaviour
{
    [Serializable]
    public class LightingState
    {
        public Color lightColor = Color.white;
        public float intensity = 2f;
        public float colorTemperature = 5000f;
        public Vector3 eulerAngles = new Vector3(50f, -30f, 0f);
        public Color ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
        public Color ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        public Color ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        public float ambientIntensity = 1f;
    }

    [SerializeField] Light sunLight;
    [SerializeField] LightingState highNoon = new LightingState
    {
        lightColor = Color.white,
        intensity = 2f,
        colorTemperature = 5000f,
        eulerAngles = new Vector3(50f, -30f, 0f),
        ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f),
        ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f),
        ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f),
        ambientIntensity = 1f
    };
    [SerializeField] LightingState dusk = new LightingState
    {
        lightColor = new Color(1f, 0.45f, 0.2f, 1f),
        intensity = 1.1f,
        colorTemperature = 2500f,
        eulerAngles = new Vector3(8f, 20f, 0f),
        ambientSkyColor = new Color(0.35f, 0.18f, 0.12f, 1f),
        ambientEquatorColor = new Color(0.22f, 0.12f, 0.1f, 1f),
        ambientGroundColor = new Color(0.08f, 0.04f, 0.03f, 1f),
        ambientIntensity = 0.75f
    };
    [SerializeField] LightingState night = new LightingState
    {
        lightColor = new Color(0.55f, 0.68f, 1f, 1f),
        intensity = 0.65f,
        colorTemperature = 9500f,
        eulerAngles = new Vector3(-40f, 150f, 0f),
        ambientSkyColor = new Color(0.04f, 0.06f, 0.12f, 1f),
        ambientEquatorColor = new Color(0.03f, 0.04f, 0.08f, 1f),
        ambientGroundColor = new Color(0.01f, 0.015f, 0.03f, 1f),
        ambientIntensity = 0.55f
    };
    [SerializeField] LightingState dawn = new LightingState
    {
        lightColor = new Color(1f, 0.65f, 0.4f, 1f),
        intensity = 1.25f,
        colorTemperature = 3500f,
        eulerAngles = new Vector3(12f, -100f, 0f),
        ambientSkyColor = new Color(0.28f, 0.2f, 0.22f, 1f),
        ambientEquatorColor = new Color(0.18f, 0.12f, 0.14f, 1f),
        ambientGroundColor = new Color(0.06f, 0.04f, 0.04f, 1f),
        ambientIntensity = 0.8f
    };
    [SerializeField] float fullCycleDuration = 3.2f;
    [SerializeField] Ease cycleEase = Ease.InOutSine;
    [SerializeField] bool captureHighNoonFromSceneOnAwake = true;
    [SerializeField] bool applyHighNoonOnAwake = true;

    Tween _transitionTween;

    void Awake()
    {
        if (sunLight == null)
            sunLight = GetComponent<Light>();

        if (sunLight == null)
            sunLight = FindFirstObjectByType<Light>();

        if (captureHighNoonFromSceneOnAwake)
            CaptureCurrentInto(highNoon);

        if (applyHighNoonOnAwake)
            ApplyInstant(highNoon);
    }

    void OnDisable()
    {
        KillTransition();
    }

    public IEnumerator PlayFullCycleToHighNoonRoutine()
    {
        if (fullCycleDuration <= 0f)
        {
            ApplyInstant(highNoon);
            yield break;
        }

        KillTransition();

        LightingState[] keys =
        {
            highNoon,
            dusk,
            night,
            dawn,
            highNoon
        };

        float t = 0f;
        _transitionTween = DOTween
            .To(() => t, value =>
            {
                t = value;
                ApplyCycleProgress(keys, t);
            }, 1f, fullCycleDuration)
            .SetEase(cycleEase);

        yield return _transitionTween.WaitForCompletion();
        ApplyInstant(highNoon);
        _transitionTween = null;
    }

    public void ApplyInstant(LightingState state)
    {
        if (state == null)
            return;

        if (sunLight != null)
        {
            sunLight.color = state.lightColor;
            sunLight.intensity = state.intensity;
            sunLight.colorTemperature = state.colorTemperature;
            sunLight.useColorTemperature = true;
            sunLight.transform.rotation = Quaternion.Euler(state.eulerAngles);
        }

        RenderSettings.ambientSkyColor = state.ambientSkyColor;
        RenderSettings.ambientEquatorColor = state.ambientEquatorColor;
        RenderSettings.ambientGroundColor = state.ambientGroundColor;
        RenderSettings.ambientIntensity = state.ambientIntensity;
    }

    void ApplyCycleProgress(LightingState[] keys, float progress)
    {
        if (keys == null || keys.Length == 0)
            return;

        if (keys.Length == 1)
        {
            ApplyInstant(keys[0]);
            return;
        }

        float clamped = Mathf.Clamp01(progress);
        float scaled = clamped * (keys.Length - 1);
        int index = Mathf.FloorToInt(scaled);
        if (index >= keys.Length - 1)
        {
            ApplyInstant(keys[keys.Length - 1]);
            return;
        }

        float localT = scaled - index;
        ApplyLerp(keys[index], keys[index + 1], localT);
    }

    void CaptureCurrentInto(LightingState state)
    {
        if (state == null)
            return;

        if (sunLight != null)
        {
            state.lightColor = sunLight.color;
            state.intensity = sunLight.intensity;
            state.colorTemperature = sunLight.colorTemperature;
            state.eulerAngles = sunLight.transform.eulerAngles;
        }

        state.ambientSkyColor = RenderSettings.ambientSkyColor;
        state.ambientEquatorColor = RenderSettings.ambientEquatorColor;
        state.ambientGroundColor = RenderSettings.ambientGroundColor;
        state.ambientIntensity = RenderSettings.ambientIntensity;
    }

    void ApplyLerp(LightingState from, LightingState to, float t)
    {
        if (from == null || to == null)
            return;

        if (sunLight != null)
        {
            sunLight.color = Color.Lerp(from.lightColor, to.lightColor, t);
            sunLight.intensity = Mathf.Lerp(from.intensity, to.intensity, t);
            sunLight.colorTemperature = Mathf.Lerp(from.colorTemperature, to.colorTemperature, t);
            sunLight.useColorTemperature = true;
            sunLight.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(from.eulerAngles),
                Quaternion.Euler(to.eulerAngles),
                t);
        }

        RenderSettings.ambientSkyColor = Color.Lerp(from.ambientSkyColor, to.ambientSkyColor, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(from.ambientEquatorColor, to.ambientEquatorColor, t);
        RenderSettings.ambientGroundColor = Color.Lerp(from.ambientGroundColor, to.ambientGroundColor, t);
        RenderSettings.ambientIntensity = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, t);
    }

    void KillTransition()
    {
        if (_transitionTween != null && _transitionTween.IsActive())
            _transitionTween.Kill();

        _transitionTween = null;
    }
}
