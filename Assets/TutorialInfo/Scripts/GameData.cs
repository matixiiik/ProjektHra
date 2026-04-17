using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class GameData
{
    public int playerGridX = 0;
    public int playerGridY = 0;
    public int coins = 0;

    public bool hasSpeedUpgrade = false;
    public bool hasRodUpgrade = false;

    // Změna: Nyní ukládáme TileStatus objekt
    public SerializableDictionary<string, TileStatus> tileData = new SerializableDictionary<string, TileStatus>();

    public GameData()
    {
        playerGridX = 0;
        playerGridY = 0;
        coins = 0;
    }
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
        {
            this.Add(keys[i], values[i]);
        }
    }
}