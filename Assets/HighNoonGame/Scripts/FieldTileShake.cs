using DG.Tweening;
using UnityEngine;

public class FieldTileShake : MonoBehaviour
{
    [SerializeField] SelactableTile tile;
    [SerializeField] float duration = 0.2f;
    [SerializeField] Vector3 strength = new Vector3(0.04f, 0.12f, 0.04f);
    [SerializeField] int vibrato = 16;
    [SerializeField] float randomness = 90f;
    [SerializeField] bool snapping = false;
    [SerializeField] bool fadeOut = true;

    Vector3 _localOrigin;
    Tween _shakeTween;
    BattleManager _battle;

    void Awake()
    {
        if (tile == null)
            tile = GetComponent<SelactableTile>();

        _localOrigin = transform.localPosition;
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
        KillShake(resetPosition: true);
    }

    void TrySubscribe()
    {
        if (!BattleManager.TryGetInstance(out var battle))
            return;

        if (_battle == battle)
            return;

        Unsubscribe();
        _battle = battle;
        _battle.TileShot += OnTileShot;
    }

    void Unsubscribe()
    {
        if (_battle != null)
            _battle.TileShot -= OnTileShot;

        _battle = null;
    }

    void OnTileShot(int tileIndex)
    {
        if (tile == null || tile.tileIndex != tileIndex)
            return;

        KillShake(resetPosition: true);

        _shakeTween = transform
            .DOShakePosition(duration, strength, vibrato, randomness, snapping, fadeOut)
            .SetUpdate(UpdateType.Late)
            .OnComplete(() => transform.localPosition = _localOrigin);
    }

    void KillShake(bool resetPosition)
    {
        if (_shakeTween != null && _shakeTween.IsActive())
            _shakeTween.Kill();

        _shakeTween = null;

        if (resetPosition)
            transform.localPosition = _localOrigin;
    }
}
