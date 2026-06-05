using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class ActiveQuest
{
    public bool hasQuest;
    public int questType; // 0=ryby, 1=poklady
    public string description;
    public int target;
    public int progress;
    public int cost;
    public int reward;
    public int multiplier;

    public bool IsComplete => hasQuest && progress >= target;

    public void Reset()
    {
        hasQuest = false;
        questType = 0;
        description = "";
        target = 0;
        progress = 0;
        cost = 0;
        reward = 0;
    }
}

[Serializable]
public class GameData
{
    public int playerGridX;
    public int playerGridY;
    public int coins;
    public bool hasSpeedUpgrade;
    public bool hasRodUpgrade;
    public bool hasMiningUpgrade;
    public int fishCount;
    public int treasureCount;
    public bool isOnFoot;
    public int boatGridX;
    public int boatGridY;
    public int shipLevel; // 0=small, 1=medium, 2=large
    public ActiveQuest activeQuest = new ActiveQuest();

    // ── Hráč 2 — oddělená ekonomika (multiplayer) ──────────────────────────────
    public int player2GridX;
    public int player2GridY;
    public int player2Coins;
    public int player2FishCount;
    public int player2TreasureCount;
    public bool player2HasSpeedUpgrade;
    public bool player2HasRodUpgrade;
    public bool player2HasMiningUpgrade;
    public int  player2ShipLevel;
    public ActiveQuest player2ActiveQuest = new ActiveQuest();
    public SerializableDictionary<string, TileStatus> tileData = new SerializableDictionary<string, TileStatus>();
}

[Serializable]
public class SerializableDictionary<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver
{
    [SerializeField] private List<K> keys = new List<K>();
    [SerializeField] private List<V> values = new List<V>();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (KeyValuePair<K, V> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        this.Clear();
        if (keys.Count != values.Count) return;
        for (int i = 0; i < keys.Count; i++)
            this.Add(keys[i], values[i]);
    }
}
