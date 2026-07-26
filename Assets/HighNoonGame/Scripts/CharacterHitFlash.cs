using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CharacterHitFlash : MonoBehaviour
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] bool isPlayer = true;
    [SerializeField] Color whiteFlash = Color.white;
    [SerializeField] Color redFlash = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] float whiteDuration = 0.08f;
    [SerializeField] float redDuration = 0.12f;
    [SerializeField] float emissionIntensity = 3f;

    Material[] _emissionMaterials;
    Sequence _flashSequence;
    BattleManager _battle;

    void Awake()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        var mats = new List<Material>();

        for (int r = 0; r < renderers.Length; r++)
        {
            var renderer = renderers[r];
            if (renderer == null)
                continue;

            var instances = renderer.materials;
            for (int m = 0; m < instances.Length; m++)
            {
                var mat = instances[m];
                if (mat == null || !mat.HasProperty(EmissionColorId))
                    continue;

                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor(EmissionColorId, Color.black);
                mats.Add(mat);
            }
        }

        _emissionMaterials = mats.ToArray();
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
        KillFlash(restore: true);
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.CharacterHit += OnCharacterHit;
    }

    void Unsubscribe()
    {
        if (_battle != null)
            _battle.CharacterHit -= OnCharacterHit;

        _battle = null;
    }

    void OnCharacterHit(bool hitPlayer, Vector3 hitDirection)
    {
        if (hitPlayer != isPlayer)
            return;

        KillFlash(restore: true);

        if (_emissionMaterials == null || _emissionMaterials.Length == 0)
            return;

        _flashSequence = DOTween.Sequence().SetUpdate(UpdateType.Late);
        _flashSequence.AppendCallback(() => ApplyEmission(whiteFlash));
        _flashSequence.AppendInterval(whiteDuration);
        _flashSequence.AppendCallback(() => ApplyEmission(redFlash));
        _flashSequence.AppendInterval(redDuration);
        _flashSequence.OnComplete(() => ClearEmission());
    }

    void KillFlash(bool restore)
    {
        if (_flashSequence != null && _flashSequence.IsActive())
            _flashSequence.Kill();

        _flashSequence = null;

        if (restore)
            ClearEmission();
    }

    void ApplyEmission(Color color)
    {
        Color emission = color * emissionIntensity;

        for (int i = 0; i < _emissionMaterials.Length; i++)
        {
            var mat = _emissionMaterials[i];
            if (mat != null)
                mat.SetColor(EmissionColorId, emission);
        }
    }

    void ClearEmission()
    {
        if (_emissionMaterials == null)
            return;

        for (int i = 0; i < _emissionMaterials.Length; i++)
        {
            var mat = _emissionMaterials[i];
            if (mat != null)
                mat.SetColor(EmissionColorId, Color.black);
        }
    }
}
