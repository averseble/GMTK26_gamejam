using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerTileSelecter : MonoBehaviour
{
    [Tooltip("Если пусто то соберёт SelactableTile из детей")]
    public List<SelactableTile> Tiles = new List<SelactableTile>();

    public Camera cam;
    public LayerMask tileMask = ~0;
    public float maxDistance = 100f;

    [Header("Превью действий")]
    [Tooltip("Образ игрока для тайлов Move (полупрозрачный префаб/силуэт)")]
    public GameObject movePreviewPrefab;
    [Tooltip("Прицел / взрыв для тайлов Shoot")]
    public GameObject shootPreviewPrefab;
    public Vector3 previewOffset = new Vector3(0f, 0.1f, 0f);

    SelactableTile _hovered;
    bool _pointerDown;
    SelactableTile _selected;

    GameObject _movePreview;
    GameObject _shootPreview;
    readonly List<GameObject> _committedPreviews = new List<GameObject>();

    public UnityEvent<int, TileActionKind> OnActionSelected;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        EnsureTiles();
        EnsureLivePreviews();
    }

    void OnEnable()
    {
        EnsureTiles();

        for (int i = 0; i < Tiles.Count; i++)
        {
            var t = Tiles[i];
            if (t == null) continue;
            t.OnTilePressed.AddListener(TilePressed);
            t.OnTileHovered.AddListener(TileHover);
            t.OnTileUnhovered.AddListener(TileUnhovered);
            t.OnTileUnpressed.AddListener(TileUnpressed);
        }
    }

    void OnDisable()
    {
        if (Tiles == null) return;

        for (int i = 0; i < Tiles.Count; i++)
        {
            var t = Tiles[i];
            if (t == null) continue;
            t.OnTilePressed.RemoveListener(TilePressed);
            t.OnTileHovered.RemoveListener(TileHover);
            t.OnTileUnhovered.RemoveListener(TileUnhovered);
            t.OnTileUnpressed.RemoveListener(TileUnpressed);
        }

        _hovered = null;
        _pointerDown = false;
        HideLivePreviews();
    }

    void EnsureTiles()
    {
        if (Tiles == null)
            Tiles = new List<SelactableTile>();

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null) continue;

            var tile = col.GetComponent<SelactableTile>();
            if (tile == null)
                tile = col.gameObject.AddComponent<SelactableTile>();

            if (!Tiles.Contains(tile))
                Tiles.Add(tile);
        }

        for (int i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i] == null) continue;
            Tiles[i].tileIndex = i;
            Tiles[i].actionKind = i <= 3 ? TileActionKind.Shoot : TileActionKind.Move;
        }
    }

    void EnsureLivePreviews()
    {
        if (_movePreview == null && movePreviewPrefab != null)
        {
            _movePreview = Instantiate(movePreviewPrefab);
            _movePreview.name = "MovePreview_Live";
            DisableColliders(_movePreview);
            _movePreview.SetActive(false);
        }

        if (_shootPreview == null && shootPreviewPrefab != null)
        {
            _shootPreview = Instantiate(shootPreviewPrefab);
            _shootPreview.name = "ShootPreview_Live";
            DisableColliders(_shootPreview);
            _shootPreview.SetActive(false);
        }
    }

    static void DisableColliders(GameObject root)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;
    }

    bool IsInputAllowed()
    {
        return BattleManager.TryGetInstance(out var battle) && battle.WaitingForActionSelection;
    }

    bool IsValidTileAction(SelactableTile tile)
    {
        if (tile == null || !BattleManager.TryGetInstance(out var battle))
            return false;
        return battle.IsPlayerActionValid(tile.tileIndex, tile.actionKind);
    }

    void Update()
    {
        if (!IsInputAllowed())
        {
            if (_hovered != null)
            {
                SafeInvoke(_hovered.OnTileUnhovered, _hovered.tileIndex);
                _hovered = null;
            }
            _pointerDown = false;
            HideLivePreviews();
            return;
        }

        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        var pointer = Pointer.current;
        if (pointer == null)
            return;

        Vector2 screenPos = pointer.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);

        SelactableTile tileUnderPointer = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, tileMask))
            tileUnderPointer = hit.collider.GetComponentInParent<SelactableTile>();

        UpdateHover(tileUnderPointer);

        if (pointer.press.wasPressedThisFrame)
            _pointerDown = true;

        if (pointer.press.wasReleasedThisFrame && _pointerDown)
        {
            _pointerDown = false;
            if (_hovered != null)
                SafeInvoke(_hovered.OnTileUnpressed, _hovered.tileIndex);
        }
    }

    void UpdateHover(SelactableTile tileUnderPointer)
    {
        if (tileUnderPointer == _hovered)
            return;

        if (_hovered != null)
            SafeInvoke(_hovered.OnTileUnhovered, _hovered.tileIndex);

        _hovered = tileUnderPointer;

        if (_hovered != null)
            SafeInvoke(_hovered.OnTileHovered, _hovered.tileIndex);

        RefreshLivePreview();
    }

    void RefreshLivePreview()
    {
        EnsureLivePreviews();

        var tile = _selected != null ? _selected : _hovered;
        if (tile == null)
        {
            HideLivePreviews();
            return;
        }

        if (!IsValidTileAction(tile))
        {
            HideLivePreviews();
            return;
        }

        Vector3 pos = tile.PreviewWorldPosition + previewOffset;

        if (tile.actionKind == TileActionKind.Move)
        {
            if (_shootPreview != null)
                _shootPreview.SetActive(false);

            if (_movePreview != null)
            {
                _movePreview.transform.SetPositionAndRotation(pos, tile.transform.rotation);
                _movePreview.SetActive(true);
            }
        }
        else
        {
            if (_movePreview != null)
                _movePreview.SetActive(false);

            if (_shootPreview != null)
            {
                _shootPreview.transform.SetPositionAndRotation(pos, tile.transform.rotation);
                _shootPreview.SetActive(true);
            }
        }
    }

    void HideLivePreviews()
    {
        if (_movePreview != null)
            _movePreview.SetActive(false);
        if (_shootPreview != null)
            _shootPreview.SetActive(false);
    }

    public void CommitActionPreview(TileActionKind kind, int tileIndex)
    {
        EnsureTiles();
        if (tileIndex < 0 || tileIndex >= Tiles.Count || Tiles[tileIndex] == null)
            return;

        SelactableTile tile = Tiles[tileIndex];
        GameObject prefab = kind == TileActionKind.Move ? movePreviewPrefab : shootPreviewPrefab;
        if (prefab == null)
            return;

        var go = Instantiate(prefab);
        go.name = kind == TileActionKind.Move ? "MovePreview_Committed" : "ShootPreview_Committed";
        DisableColliders(go);
        go.transform.SetPositionAndRotation(
            tile.PreviewWorldPosition + previewOffset,
            tile.transform.rotation);
        go.SetActive(true);
        _committedPreviews.Add(go);
    }

    public bool TryGetTileWorldPosition(int tileIndex, out Vector3 worldPos)
    {
        EnsureTiles();
        if (tileIndex < 0 || tileIndex >= Tiles.Count || Tiles[tileIndex] == null)
        {
            worldPos = default;
            return false;
        }

        worldPos = Tiles[tileIndex].PreviewWorldPosition;
        return true;
    }

    public bool WritePositionsToBattleMap(gameField[] map)
    {
        EnsureTiles();
        if (map == null || Tiles.Count < map.Length)
        {
            Debug.LogError($"PlayerTileSelecter: need at least {map?.Length ?? 0} tiles, have {Tiles.Count}.");
            return false;
        }

        for (int i = 0; i < map.Length; i++)
        {
            if (Tiles[i] == null)
            {
                Debug.LogError($"PlayerTileSelecter: tile {i} is null.");
                return false;
            }
            map[i].position = Tiles[i].PreviewWorldPosition;
        }

        return true;
    }

    public void ClearCommittedPreviews()
    {
        for (int i = 0; i < _committedPreviews.Count; i++)
        {
            if (_committedPreviews[i] != null)
                Destroy(_committedPreviews[i]);
        }
        _committedPreviews.Clear();
    }

    public void ClearSelection()
    {
        _selected = null;
        RefreshLivePreview();
    }

    static void SafeInvoke(UnityEvent<int> evt, int tileIndex)
    {
        if (evt != null)
            evt.Invoke(tileIndex);
    }

    void TilePressed(int tileIndex) { }

    void TileHover(int tileIndex) { }

    void TileUnhovered(int tileIndex) { }

    void TileUnpressed(int tileIndex)
    {
        if (!IsInputAllowed())
            return;
        if (tileIndex < 0 || tileIndex >= Tiles.Count)
            return;

        var tile = Tiles[tileIndex];
        if (tile == null)
            return;

        if (!IsValidTileAction(tile))
            return;

        _selected = tile;
        RefreshLivePreview();
        OnActionSelected?.Invoke(tileIndex, tile.actionKind);
    }
}
