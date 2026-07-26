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

    [Header("Lines")]
    [TextArea(2, 4)]
    [SerializeField] string[] lines;
    [TextArea(2, 4)]
    [SerializeField] string[] postBattleWinLines;
    [TextArea(2, 4)]
    [SerializeField] string[] postBattleLoseLines;

    [Header("Talk Sounds")]
    [SerializeField] AudioClip[] talkSounds;
    [SerializeField] [Range(0f, 2f)] float talkSoundVolume = 1f;

    public string EnemyName => enemyName;

    public string[] Lines => lines;

    public string[] PostBattleWinLines => postBattleWinLines;

    public string[] PostBattleLoseLines => postBattleLoseLines;

    public AudioClip[] TalkSounds => talkSounds;

    public float TalkSoundVolume => talkSoundVolume;

    public AudioClip GetRandomTalkSound()
    {
        if (talkSounds == null || talkSounds.Length == 0)
            return null;

        return talkSounds[UnityEngine.Random.Range(0, talkSounds.Length)];
    }

    public string[] GetPostBattleLines(bool playerWon)
    {
        if (playerWon)
            return postBattleWinLines;

        return postBattleLoseLines;
    }

    public GameObject GetEnemyPrefab()
    {
        return enemyCharacterPrefab;
    }

    public EnemyAiType GetAiType()
    {
        return AiType;
    }

    public string GetRandomLine()
    {
        if (lines == null || lines.Length == 0)
            return string.Empty;

        return lines[UnityEngine.Random.Range(0, lines.Length)];
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
