using System;
using UnityEngine;
using UnityEngine.Events;

public class SelactableTile : MonoBehaviour
{
    public int tileIndex;
    public TileActionKind actionKind = TileActionKind.Move;

    public MeshRenderer mr;
    public Material baseMaterial;
    public Material selectedMaterial;

    public Transform previewTransform;

    public UnityEvent<int> OnTilePressed = new UnityEvent<int>();
    public UnityEvent<int> OnTileHovered = new UnityEvent<int>();
    public UnityEvent<int> OnTileUnhovered = new UnityEvent<int>();
    public UnityEvent<int> OnTileUnpressed = new UnityEvent<int>();

    public Vector3 PreviewWorldPosition => previewTransform.position;

    void Awake()
    {
        if (mr == null)
            mr = GetComponent<MeshRenderer>();

        if (mr != null && baseMaterial == null)
            baseMaterial = mr.sharedMaterial;

        if (previewTransform == null)
            previewTransform = transform;
    }

    void OnEnable()
    {
        OnTileHovered.AddListener(TileHovered);
        OnTileUnhovered.AddListener(TileUnhovered);
    }

    void OnDisable()
    {
        OnTileHovered.RemoveListener(TileHovered);
        OnTileUnhovered.RemoveListener(TileUnhovered);
    }

    void TileHovered(int _)
    {
        if (mr != null && selectedMaterial != null)
            mr.material = selectedMaterial;
    }

    void TileUnhovered(int _)
    {
        if (mr != null && baseMaterial != null)
            mr.material = baseMaterial;
    }

}
