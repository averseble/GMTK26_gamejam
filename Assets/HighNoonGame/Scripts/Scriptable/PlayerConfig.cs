using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Scriptable Objects/PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("Основные параметры")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private GameObject playerCharacterPrefab;

    public GameObject GetPlayerPrefab()
    {
        return playerCharacterPrefab;
    }
}
