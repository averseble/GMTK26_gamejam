using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuParallax : MonoBehaviour
{
    [Serializable]
    public class ParallaxLayer
    {
        public Transform target;
        public Vector2 moveAmount = new Vector2(0.15f, 0.1f);
        public bool useLocalSpace = true;
    }

    [SerializeField] ParallaxLayer[] layers;
    [SerializeField] float followSpeed = 6f;
    [SerializeField] bool invertX;
    [SerializeField] bool invertY;

    Vector3[] _origins;
    Vector2 _currentOffset;

    void Awake()
    {
        CacheOrigins();
    }

    void OnEnable()
    {
        CacheOrigins();
        _currentOffset = Vector2.zero;
        ApplyOffset(_currentOffset);
    }

    void OnDisable()
    {
        ApplyOffset(Vector2.zero);
    }

    void CacheOrigins()
    {
        if (layers == null)
        {
            _origins = Array.Empty<Vector3>();
            return;
        }

        _origins = new Vector3[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer == null || layer.target == null)
            {
                _origins[i] = Vector3.zero;
                continue;
            }

            if (layer.useLocalSpace)
                _origins[i] = layer.target.localPosition;
            else
                _origins[i] = layer.target.position;
        }
    }

    void LateUpdate()
    {
        if (layers == null || layers.Length == 0)
            return;

        Vector2 targetOffset = ReadPointerOffset();
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        _currentOffset = Vector2.Lerp(_currentOffset, targetOffset, t);
        ApplyOffset(_currentOffset);
    }

    Vector2 ReadPointerOffset()
    {
        Vector2 screenPos;
        Mouse mouse = Mouse.current;
        if (mouse != null)
            screenPos = mouse.position.ReadValue();
        else
            screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        float x = 0f;
        float y = 0f;

        if (Screen.width > 0)
            x = (screenPos.x / Screen.width) * 2f - 1f;

        if (Screen.height > 0)
            y = (screenPos.y / Screen.height) * 2f - 1f;

        x = Mathf.Clamp(x, -1f, 1f);
        y = Mathf.Clamp(y, -1f, 1f);

        if (invertX)
            x = -x;

        if (invertY)
            y = -y;

        return new Vector2(x, y);
    }

    void ApplyOffset(Vector2 normalizedOffset)
    {
        if (layers == null || _origins == null)
            return;

        int count = layers.Length;
        if (_origins.Length < count)
            count = _origins.Length;

        for (int i = 0; i < count; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer == null || layer.target == null)
                continue;

            Vector3 origin = _origins[i];
            Vector3 offset = new Vector3(
                normalizedOffset.x * layer.moveAmount.x,
                normalizedOffset.y * layer.moveAmount.y,
                0f);

            if (layer.useLocalSpace)
                layer.target.localPosition = origin + offset;
            else
                layer.target.position = origin + offset;
        }
    }
}
