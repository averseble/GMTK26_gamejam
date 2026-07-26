using System;
using UnityEngine;

public enum EnemyAiType
{
    random,
    rightEye,
    leftLeg,
    forwardShotWander,
    doubleRandomShot,
    openingShotLane8,
    volleyThenMove
}

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Scriptable Objects/EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("Basics")]
    [SerializeField] string enemyName = "john";
    [SerializeField] GameObject enemyCharacterPrefab;
    [SerializeField] EnemyAiType AiType;

    [Header("Visual Parts")]
    [Tooltip("Child object names on the shared enemy model to disable for this variant. Examples: Shlapa, mustache, LegNormal, LegInvalid, NetGlazChel, NetGlazaChel, Cig.002, KurenieUbiwaet")]
    [SerializeField] string[] disabledParts;

    public string EnemyName => enemyName;

    public GameObject GetEnemyPrefab()
    {
        return enemyCharacterPrefab;
    }

    public EnemyAiType GetAiType()
    {
        return AiType;
    }

    public void ApplyVisualParts(GameObject instance)
    {
        if (instance == null || disabledParts == null || disabledParts.Length == 0)
            return;

        for (int i = 0; i < disabledParts.Length; i++)
        {
            string partName = disabledParts[i];
            if (string.IsNullOrWhiteSpace(partName))
                continue;

            Transform part = FindNamedChild(instance.transform, partName.Trim());
            if (part == null)
            {
                Debug.LogWarning($"EnemyConfig '{name}': visual part '{partName}' not found on '{instance.name}'", this);
                continue;
            }

            part.gameObject.SetActive(false);
        }
    }

    static Transform FindNamedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedChild(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
