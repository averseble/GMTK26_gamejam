using System;
using UnityEngine;

public enum EnemyAiType{
    random,
    rightEye,
    leftLeg
}

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Scriptable Objects/EnemyConfig")]
public class EnemyConfig : ScriptableObject
{    
    [Header("Основные параметры")]
    [SerializeField] private string enemyName = "john";
    [SerializeField] private GameObject enemyCharacterPrefab;
    [SerializeField] private EnemyAiType AiType;


    public GameObject GetEnemyPrefab()
    {
        return enemyCharacterPrefab;
    }

    public EnemyAiType GetAiType()
    {
        return AiType;
    }
}
