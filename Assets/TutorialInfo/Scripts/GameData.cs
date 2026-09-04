using UnityEngine;
using System.Collections.Generic;
using System;

// ─────────────────────────────────────────────────────────────────────────────
//  GameData.cs
//  Kompletní stav jedné rozehrané hry — vše, co se ukládá do save souboru.
//  Používá se Unity JsonUtility, který umí serializovat jen [Serializable] třídy
//  a veřejná pole (proto tu nejsou properties, ale obyčejná public pole).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Jeden aktivní quest (úkol) hráče. Hráč si ho koupí v QuestShopu, plní ho
/// rybařením/těžbou a po splnění si vyzvedne odměnu.
/// </summary>
[Serializable]
public class ActiveQuest
{
    public bool   hasQuest;     // má hráč vůbec nějaký quest?
    public int    questType;    // 0 = nalovit ryby, 1 = vytěžit poklady
    public string description;  // text do HUD ("Ulov 10 ryb")
    public int    target;       // kolik je potřeba splnit
    public int    progress;     // kolik už hráč splnil
    public int    cost;         // kolik quest stál při koupi
    public int    reward;       // kolik hráč dostane za splnění (cost * multiplier)
    public int    multiplier;   // násobič odměny (jen pro zobrazení "3x")

    /// <summary>Quest je splněný, když je pokrok >= cíl.</summary>
    public bool IsComplete => hasQuest && progress >= target;

    /// <summary>Vynuluje quest (po vyzvednutí odměny).</summary>
    public void Reset()
    {
        hasQuest    = false;
        questType   = 0;
        description = "";
        target      = 0;
        progress    = 0;
        cost        = 0;
        reward      = 0;
        multiplier  = 0;
    }
}

/// <summary>
/// "Poklad na mapě" — mega quest z bedny. Bedna dá hráči mapu s vzdáleným
/// místem na moři; hráč tam dopluje, vykope poklad (dug = true) a odměnu si
/// vyzvedne v kterémkoli QuestShopu (mince + trvalý bonus na výkupní ceny).
/// </summary>
[Serializable]
public class MegaQuest
{
    public bool active;       // hráč má rozdělaný mega quest?
    public int  targetX;      // kam doplout (X)
    public int  targetY;      // kam doplout (Y)
    public bool dug;          // hráč doplul na místo a vykopal → jde vyplatit
    public int  rewardCoins;  // kolik mincí dá vyplacení

    public void Reset()
    {
        active      = false;
        targetX     = 0;
        targetY     = 0;
        dug         = false;
        rewardCoins = 0;
    }
}

/// <summary>
/// Veškerý ukládaný stav hry. Jeden objekt = jeden save slot.
/// Pole "player2..." se používají jen v multiplayeru (split screen).
/// </summary>
[Serializable]
public class GameData
{
    // ── Hráč 1 — pozice a ekonomika ───────────────────────────────────────────
    public int  playerGridX;       // pozice hráče na mřížce (X)
    public int  playerGridY;       // pozice hráče na mřížce (Y)
    public int  coins;             // mince
    public bool hasSpeedUpgrade;   // koupená rychlost lodě (pohyb o 2 pole)
    public bool hasRodUpgrade;     // koupený lepší prut (2 ryby na zátah)
    public bool hasMiningUpgrade;  // koupená rychlejší těžba
    public int  fishCount;         // nalovené ryby (k prodeji)
    public int  treasureCount;     // vytěžené poklady (k prodeji)
    public bool isOnFoot;          // true = hráč je pěšky na ostrově, ne v lodi
    public int  boatGridX;         // kde nechal zakotvenou loď (X)
    public int  boatGridY;         // kde nechal zakotvenou loď (Y)
    public int  shipLevel;         // úroveň/vzhled lodě: 0=malá, 1=střední, 2=velká
    public bool sellBonus;         // trvalý bonus k výkupním cenám (odměna za mega quest)
    public ActiveQuest activeQuest = new ActiveQuest();
    public MegaQuest   megaQuest   = new MegaQuest();
    public List<string> openedChests = new List<string>(); // klíče "x,y" už otevřených beden

    // ── Hráč 2 — oddělená ekonomika (jen multiplayer) ─────────────────────────
    public int  player2GridX;
    public int  player2GridY;
    public int  player2Coins;
    public int  player2FishCount;
    public int  player2TreasureCount;
    public bool player2HasSpeedUpgrade;
    public bool player2HasRodUpgrade;
    public bool player2HasMiningUpgrade;
    public int  player2ShipLevel;
    public bool player2SellBonus;
    public ActiveQuest player2ActiveQuest = new ActiveQuest();
    public MegaQuest   player2MegaQuest   = new MegaQuest();
    public List<string> player2OpenedChests = new List<string>();

    // ── Svět ─────────────────────────────────────────────────────────────────
    // Klíč = "x,y" (souřadnice políčka jako text), hodnota = stav políčka.
    // Ukládají se jen políčka, která už byla vygenerovaná / navštívená.
    public SerializableDictionary<string, TileStatus> tileData = new SerializableDictionary<string, TileStatus>();
}

/// <summary>
/// Slovník (Dictionary), který umí Unity JsonUtility uložit a načíst.
/// JsonUtility slovníky neumí, proto se při ukládání rozloží na dva seznamy
/// (klíče a hodnoty) a při načítání se zase složí zpátky.
/// </summary>
[Serializable]
public class SerializableDictionary<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver
{
    [SerializeField] private List<K> keys   = new List<K>();
    [SerializeField] private List<V> values = new List<V>();

    // Volá Unity těsně PŘED uložením — rozlož slovník do dvou seznamů.
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

    // Volá Unity těsně PO načtení — slož slovník zpátky ze dvou seznamů.
    public void OnAfterDeserialize()
    {
        this.Clear();
        if (keys.Count != values.Count) return; // pojistka proti poškozenému save
        for (int i = 0; i < keys.Count; i++)
            this.Add(keys[i], values[i]);
    }
}
