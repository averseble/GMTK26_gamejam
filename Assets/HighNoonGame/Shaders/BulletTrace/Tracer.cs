using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Tracer : MonoBehaviour
{
    const string PathDissolveProperty = "_Path_dissolve";
    const string DissolveProperty = "_Dissolve";

    [Header("Raycast")]
    [SerializeField] float maxDistance = 50f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Line")]
    [SerializeField] LineRenderer tracer;
    [SerializeField] Material tracerMaterial;
    [SerializeField] float pointsPerUnit = 8f;
    [SerializeField] int minPoints = 8;
    [SerializeField] int maxPoints = 128;

    [Header("Noise distortion")]
    [SerializeField] float noiseScale = 3f;
    [SerializeField] float noisePower = 0.45f;
    [Tooltip("Насколько сильно виляние нарастает к концу dissolve (1 = максимум к концу)")]
    [SerializeField] float distortionFadeIn = 1f;
    [SerializeField] float noiseScrollSpeed = 2.5f;

    [Header("Dissolve")]
    [SerializeField] float dissolveDuration = 1f;

    Material _matInstance;
    Vector3 _start;
    Vector3 _end;
    Vector3 _axis;
    Vector3 _perpA;
    Vector3 _perpB;
    float _seed;
    int _pointCount;
    Coroutine _routine;
    bool _hasLine;
    bool _useForcedEnd;
    Vector3 _forcedEnd;
    bool _destroyWhenDone;

    void Reset()
    {
        tracer = GetComponent<LineRenderer>();
    }

    void Awake()
    {
        if (tracer == null)
            tracer = GetComponent<LineRenderer>();
    }

    void OnEnable()
    {
        BuildAndPlay();
    }

    void Start()
    {
        // На случай, если объект уже был enabled до подписки / первый кадр
        if (!_hasLine)
            BuildAndPlay();
    }

    void OnDisable()
    {
        StopPlay();
        ClearLine();
    }

    void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
    }

    /// <summary>Спавн выстрела между двумя точками (без raycast). По умолчанию уничтожает объект в конце.</summary>
    public void Play(Vector3 worldStart, Vector3 worldEnd, bool destroyWhenDone = true)
    {
        _destroyWhenDone = destroyWhenDone;
        _useForcedEnd = true;
        _forcedEnd = worldEnd;
        transform.position = worldStart;

        Vector3 dir = worldEnd - worldStart;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (gameObject.activeInHierarchy)
            BuildAndPlay();
        else
            gameObject.SetActive(true);
    }

    public void BuildAndPlay()
    {
        StopPlay();
        ClearLine();

        if (tracer == null)
            tracer = GetComponent<LineRenderer>();
        if (tracer == null)
            return;

        EnsureMaterialInstance();

        _start = transform.position;
        _end = _useForcedEnd ? _forcedEnd : RaycastEndPoint();
        _useForcedEnd = false;

        _axis = _end - _start;
        float distance = _axis.magnitude;
        if (distance < 0.0001f)
        {
            FinishTracer();
            return;
        }

        _axis /= distance;
        BuildPerpendiculars(_axis, out _perpA, out _perpB);

        _seed = Random.Range(0f, 100f);
        _pointCount = Mathf.Clamp(Mathf.CeilToInt(distance * pointsPerUnit) + 1, minPoints, maxPoints);

        tracer.useWorldSpace = true;
        tracer.positionCount = _pointCount;
        SetStraightLine();

        SetDissolveProps(0f, 0f);
        _hasLine = true;
        _routine = StartCoroutine(PlayTracerRoutine());
    }

    IEnumerator PlayTracerRoutine()
    {
        float t = 0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dissolveDuration);

            // К концу dissolve виляние сильнее
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / Mathf.Max(0.0001f, distortionFadeIn)));
            float distortAmount = noisePower * fade;

            ApplyDistortedLine(distortAmount, t);

            SetDissolveProps(u, u);

            yield return null;
        }

        SetDissolveProps(1f, 1f);
        ClearLine();
        _hasLine = false;
        _routine = null;
        FinishTracer();
    }

    void FinishTracer()
    {
        if (_destroyWhenDone)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    void ApplyDistortedLine(float power, float time)
    {
        if (tracer == null || _pointCount < 2)
            return;

        for (int i = 0; i < _pointCount; i++)
        {
            float t = i / (float)(_pointCount - 1);
            Vector3 straight = Vector3.Lerp(_start, _end, t);

            // Сильнее в середине луча, слабее у концов
            float envelope = Mathf.Sin(t * Mathf.PI);
            envelope *= envelope; // чуть мягче

            // Шум в локальном пространстве линии + время → видимое «виляние»
            float nx = t * noiseScale + _seed;
            float ny = time * noiseScrollSpeed + _seed * 1.618f;
            float n1 = SampleSmoothNoise(nx, ny);
            float n2 = SampleSmoothNoise(nx + 17.3f, ny + 9.1f);

            Vector3 offset = (_perpA * (n1 * 2f - 1f) + _perpB * (n2 * 2f - 1f)) * (power * envelope);
            tracer.SetPosition(i, straight + offset);
        }
    }

    void SetStraightLine()
    {
        for (int i = 0; i < _pointCount; i++)
        {
            float t = i / (float)(_pointCount - 1);
            tracer.SetPosition(i, Vector3.Lerp(_start, _end, t));
        }
    }

    /// <summary>Гладкий шум (два слоя Perlin) — без огромных world-координат.</summary>
    float SampleSmoothNoise(float x, float y)
    {
        float a = Mathf.PerlinNoise(x, y);
        float b = Mathf.PerlinNoise(x * 2.13f + 5.2f, y * 2.13f + 3.7f);
        return a * 0.65f + b * 0.35f;
    }

    Vector3 RaycastEndPoint()
    {
        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, hitMask, triggerInteraction))
            return hit.point;

        return origin + dir * maxDistance;
    }

    void EnsureMaterialInstance()
    {
        if (_matInstance != null)
        {
            tracer.material = _matInstance;
            return;
        }

        Material source = tracerMaterial != null
            ? tracerMaterial
            : (tracer.sharedMaterial != null ? tracer.sharedMaterial : null);

        if (source == null)
            return;

        _matInstance = new Material(source);
        _matInstance.name = source.name + " (Tracer Instance)";
        tracer.material = _matInstance;
    }

    void SetDissolveProps(float pathDissolve, float dissolve)
    {
        if (_matInstance == null)
            return;

        if (_matInstance.HasProperty(PathDissolveProperty))
            _matInstance.SetFloat(PathDissolveProperty, pathDissolve);
        if (_matInstance.HasProperty(DissolveProperty))
            _matInstance.SetFloat(DissolveProperty, dissolve);
    }

    static void BuildPerpendiculars(Vector3 axis, out Vector3 a, out Vector3 b)
    {
        Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        a = Vector3.Normalize(Vector3.Cross(axis, up));
        b = Vector3.Normalize(Vector3.Cross(axis, a));
    }

    void StopPlay()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    void ClearLine()
    {
        if (tracer != null)
            tracer.positionCount = 0;
        _pointCount = 0;
        _hasLine = false;
    }
}
