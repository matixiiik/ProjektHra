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
    public bool grantsHistoricalTreasure; // ~20 % — vyplacení dá i "historický poklad" (pro příběh)

    public void Reset()
    {
        active      = false;
        targetX     = 0;
        targetY     = 0;
        dug         = false;
        rewardCoins = 0;
        grantsHistoricalTreasure = false;
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
    public int  shipLevel;         // loď: 0=veslice (start), 1=malá plachetnice, 2=střední, 3=velká (viz BoatStats)
    public bool sellBonus;         // trvalý bonus k výkupním cenám (odměna za mega quest)
    public int  boatHealth   = 100;// zdraví lodě (0–100); opravuje se v přístavu / obchodě
    public int  playerHealth = 100;// zdraví hráče (0–100); 0 = smrt (respawn / menu)
    public bool boatWrecked;       // loď je rozbitá → hráč plave ve vodě, dokud ji neopraví v obchodě
    public bool boatNeedsRehome;   // po opravě rozbité lodě: přemístit ji k nejbližšímu molu
    public int  ammo;              // náboje do děla na lodi (kupují se v obchodě)
    public bool hasMap;            // koupená mapa → klávesa M v lodi otevře velkou mapu
    public bool hasWaypoint;       // hráč si na mapě klikl cíl (navádí šipka na minimapě)
    public int  waypointX;         // souřadnice cíle (X)
    public int  waypointY;         // souřadnice cíle (Y)
    public ActiveQuest activeQuest = new ActiveQuest();
    public MegaQuest   megaQuest   = new MegaQuest();
    public List<string> openedChests   = new List<string>(); // klíče "x,y" už otevřených beden
    public List<string> hostileIslands = new List<string>(); // klíče "x,y" (kotva majáku) nepřátelských ostrovů
    public List<string> clearedIslands = new List<string>(); // nepřátelské ostrovy, kterým hráč zničil dělo
    public List<string> mappedIslands  = new List<string>(); // ostrovy zahlédnuté na dálku (klíč "minX,minY") → na velké mapě
    public int          pirateKills;                          // kolik pirátů hráč potopil (jen statistika)

    // ── Příběh (starý námořník na startovním ostrově) — sdílené pro oba hráče ──
    public int  storyStep;             // 0 = start, 1 = má loď/musí se prokázat, 2 = dostal souřadnice, 3 = našel stopu, 4+ = pokračování
    public bool hasHistoricalTreasure; // "historický poklad" — padá z mega questu (~20 %), chce ho starý námořník
    public bool storyIslandActive;     // příběhový mega ostrov je vygenerovaný a hráč zná jeho polohu
    public int  storyIslandX;
    public int  storyIslandY;
    public bool storyNpcPlaced;        // starý námořník už má napevno vybrané políčko (ať se nepřesouvá)
    public int  storyNpcX;             // políčko dědy (X)
    public int  storyNpcY;             // políčko dědy (Y)

    // ── Hráč 2 — oddělená ekonomika (jen multiplayer) ─────────────────────────
    public int  player2GridX;
    public int  player2GridY;
    public bool player2IsOnFoot;    // true = P2 je pěšky na ostrově, ne v lodi
    public int  player2BoatGridX;   // kde nechal P2 zakotvenou loď (X)
    public int  player2BoatGridY;   // kde nechal P2 zakotvenou loď (Y)
    public int  player2Coins;
    public int  player2FishCount;
    public int  player2TreasureCount;
    public bool player2HasSpeedUpgrade;
    public bool player2HasRodUpgrade;
    public bool player2HasMiningUpgrade;
    public int  player2ShipLevel;
    public bool player2SellBonus;
    public int  player2BoatHealth   = 100;
    public int  player2PlayerHealth = 100;
    public bool player2BoatWrecked;
    public bool player2BoatNeedsRehome;
    public int  player2Ammo;
    public bool player2HasMap;
    public bool player2HasWaypoint;
    public int  player2WaypointX;
    public int  player2WaypointY;
    public ActiveQuest player2ActiveQuest = new ActiveQuest();
    public MegaQuest   player2MegaQuest   = new MegaQuest();
    public List<string> player2OpenedChests = new List<string>();

    // ── Svět ─────────────────────────────────────────────────────────────────
    // Klíč = "x,y" (souřadnice políčka jako text), hodnota = stav políčka.
    // Ukládají se jen políčka, která už byla vygenerovaná / navštívená.
    // Dekorace ostrovů je uložená přímo v TileStatus (viz TileData.cs) —
    // vygeneruje se jednou při vzniku ostrova a pak už zůstává.
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
