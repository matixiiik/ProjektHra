using UnityEngine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ─────────────────────────────────────────────────────────────────────────────
//  SaveManager.cs
//  Ukládání a načítání hry do/z JSON souboru na disku.
//  Statická třída = nemusí být na žádném objektu ve scéně, volá se přímo
//  SaveManager.SaveGame(...) / SaveManager.LoadGame().
//
//  Hra má 3 nezávislé sloty (0, 1, 2). Každý slot je vlastní soubor:
//      save_0.json, save_1.json, save_2.json
//  ve složce Application.persistentDataPath (na Windows: %AppData%/../LocalLow/...).
//
//  Save je velký (tisíce prozkoumaných políček — několik MB), a zápis takového
//  souboru trvá stovky milisekund. Proto:
//   • JSON je kompaktní (bez odsazení) — menší soubor, rychlejší zápis;
//   • SaveGameAsync zapisuje na pozadí, hra se nezasekne;
//   • soubor se píše do dočasného .tmp a pak se atomicky vymění, takže
//     přerušený zápis nikdy nepoškodí starý save.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Malý náhled slotu pro hlavní menu (mince, ryby, poklady) — bez načítání celého světa.</summary>
[System.Serializable]
public class SlotSummary
{
    public int coins;
    public int fishCount;
    public int treasureCount;
    public PlayerState[] players; // v2+ sklady — viz PeekSlotSummary
}

public static class SaveManager
{
    /// <summary>Slot, se kterým se právě pracuje (nastavuje ho menu / GridManager).</summary>
    public static int CurrentSlot { get; set; } = 0;

    // Sestaví celou cestu k souboru daného slotu.
    private static string GetPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    // ── Zápis na disk ───────────────────────────────────────────────────────
    private static readonly object writeLock = new object(); // v jednu chvíli píše jen jeden zápis
    private static long requestCounter;                      // pořadové číslo každého požadavku na uložení
    private static long lastWrittenId;                       // číslo požadavku, který se zapsal naposled
    private static int  pendingWrites;                       // kolik zápisů ještě běží nebo čeká

    /// <summary>Uloží data do souboru aktuálního slotu a počká, až je hotovo.</summary>
    public static void SaveGame(GameData data)
    {
        string json = ToJson(data);
        if (json == null) return;

        long id = Interlocked.Increment(ref requestCounter);
        Interlocked.Increment(ref pendingWrites);
        WriteFile(GetPath(CurrentSlot), json, id);
    }

    /// <summary>
    /// Uloží data do souboru aktuálního slotu. JSON se sestaví hned (na hlavním
    /// vlákně — JsonUtility jinak nejde), ale zápis na disk běží na pozadí.
    /// </summary>
    public static void SaveGameAsync(GameData data)
    {
        string json = ToJson(data);
        if (json == null) return;

        long   id   = Interlocked.Increment(ref requestCounter);
        string path = GetPath(CurrentSlot); // slot se zapamatuje teď, ne až na pozadí
        Interlocked.Increment(ref pendingWrites);
        Task.Run(() => WriteFile(path, json, id));
    }

    /// <summary>Počká, až doběhnou všechny rozepsané zápisy na pozadí (max ~5 s).</summary>
    public static void WaitForPendingWrites()
    {
        int guard = 0;
        while (Volatile.Read(ref pendingWrites) > 0 && guard++ < 5000)
            Thread.Sleep(1);
    }

    private static string ToJson(GameData data)
    {
        try
        {
            // false = kompaktní JSON (bez odsazení), načíst ho umí JsonUtility stejně.
            return JsonUtility.ToJson(data, false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
            return null;
        }
    }

    // Skutečný zápis (může běžet na pozadí). Novější požadavek vždy vyhrává nad
    // starším, i kdyby se vlákna předběhla.
    private static void WriteFile(string path, string json, long id)
    {
        try
        {
            lock (writeLock)
            {
                if (id < lastWrittenId) return; // mezitím se zapsala novější verze
                lastWrittenId = id;

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                try
                {
                    if (File.Exists(path)) File.Replace(tmp, path, null); // atomicky vymění obsah
                    else                   File.Move(tmp, path);
                }
                catch (IOException)
                {
                    // Něco (antivirus, indexer) soubor na chvíli drží — zapiš napřímo.
                    File.WriteAllText(path, json);
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref pendingWrites);
        }
    }

    // ── Načtení ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Načte data aktuálního slotu. Když soubor neexistuje nebo je poškozený,
    /// vrátí čerstvá výchozí data (= nová hra).
    /// </summary>
    public static GameData LoadGame()
    {
        WaitForPendingWrites();

        if (!File.Exists(GetPath(CurrentSlot)))
            return new GameData();

        try
        {
            string json = File.ReadAllText(GetPath(CurrentSlot));
            return ParseAndMigrate(json);
        }
        catch (System.Exception e)
        {
            // Soubor existuje, ale nejde přečíst (poškozený) — začni novou hru.
            Debug.LogWarning($"Save slotu {CurrentSlot} je poškozený, spouštím novou hru. ({e.Message})");
            return new GameData();
        }
    }

    /// <summary>
    /// Převede JSON savu na GameData a rovnou ho zmigruje. Public kvůli testům.
    /// </summary>
    public static GameData ParseAndMigrate(string json)
    {
        // ?? new GameData() ošetří případ, kdy je JSON prázdný / null
        GameData data = JsonUtility.FromJson<GameData>(json) ?? new GameData();

        // JsonUtility u chybějícího pole nechá výchozí hodnotu z inicializátoru
        // (= aktuální verze), takže nejstarší savy bez "saveVersion" by se tvářily
        // jako nové a migrace by se přeskočila. Bez pole v JSONu = verze 0.
        if (!json.Contains("\"saveVersion\"")) data.saveVersion = 0;

        MigrateIfNeeded(data);
        return data;
    }

    /// <summary>
    /// Posune stará save data na aktuální verzi formátu. Až nějaká budoucí
    /// změna bude vyžadovat přemapování existujícího pole (ne jen přidání
    /// nového — to JsonUtility zvládne sama), přibude sem další krok podle
    /// data.saveVersion.
    /// </summary>
    private static void MigrateIfNeeded(GameData data)
    {
        if (data.saveVersion >= GameData.CURRENT_SAVE_VERSION) return;

        Debug.Log($"Save slotu {CurrentSlot}: migrace z verze {data.saveVersion} na {GameData.CURRENT_SAVE_VERSION}.");

        if (data.saveVersion < 2) MigrateToV2_PlayerState(data);

        data.saveVersion = GameData.CURRENT_SAVE_VERSION;
    }

    // v2: P1/P2 ekonomika a výbava byly zdvojené ploché fieldy v GameData
    // ("coins"/"player2Coins" apod.) — sjednoceno do `GameData.players[0]`/`[1]`
    // (viz GameData.PlayerState). Staré fieldy zůstávají v GameData jen jako
    // zdroj TÉHLE migrace (JsonUtility je z JSON pořád načte), nový kód s nimi
    // už nepracuje. Přečtou se tu naposledy a pak se na ně dál nesahá.
    private static void MigrateToV2_PlayerState(GameData d)
    {
        var p1 = d.players[0];
        p1.gridX             = d.playerGridX;
        p1.gridY             = d.playerGridY;
        p1.isOnFoot           = d.isOnFoot;
        p1.boatGridX          = d.boatGridX;
        p1.boatGridY          = d.boatGridY;
        p1.coins              = d.coins;
        p1.fishCount          = d.fishCount;
        p1.treasureCount      = d.treasureCount;
        p1.hasSpeedUpgrade    = d.hasSpeedUpgrade;
        p1.hasRodUpgrade      = d.hasRodUpgrade;
        p1.hasMiningUpgrade   = d.hasMiningUpgrade;
        p1.shipLevel          = d.shipLevel;
        p1.sellBonus          = d.sellBonus;
        p1.boatHealth         = d.boatHealth;
        p1.playerHealth       = d.playerHealth;
        p1.boatWrecked        = d.boatWrecked;
        p1.boatNeedsRehome    = d.boatNeedsRehome;
        p1.ammo               = d.ammo;
        p1.hasMap             = d.hasMap;
        p1.hasWaypoint        = d.hasWaypoint;
        p1.waypointX          = d.waypointX;
        p1.waypointY          = d.waypointY;
        p1.activeQuest        = d.activeQuest    ?? new ActiveQuest();
        p1.megaQuest          = d.megaQuest      ?? new MegaQuest();
        p1.openedChests       = d.openedChests   ?? new System.Collections.Generic.List<string>();
        p1.hasHandWeapon      = d.hasHandWeapon;
        p1.handAmmo           = d.handAmmo;
        p1.activeHotbarSlot   = d.activeHotbarSlot;

        var p2 = d.players[1];
        p2.gridX             = d.player2GridX;
        p2.gridY             = d.player2GridY;
        p2.isOnFoot           = d.player2IsOnFoot;
        p2.boatGridX          = d.player2BoatGridX;
        p2.boatGridY          = d.player2BoatGridY;
        p2.coins              = d.player2Coins;
        p2.fishCount          = d.player2FishCount;
        p2.treasureCount      = d.player2TreasureCount;
        p2.hasSpeedUpgrade    = d.player2HasSpeedUpgrade;
        p2.hasRodUpgrade      = d.player2HasRodUpgrade;
        p2.hasMiningUpgrade   = d.player2HasMiningUpgrade;
        p2.shipLevel          = d.player2ShipLevel;
        p2.sellBonus          = d.player2SellBonus;
        p2.boatHealth         = d.player2BoatHealth;
        p2.playerHealth       = d.player2PlayerHealth;
        p2.boatWrecked        = d.player2BoatWrecked;
        p2.boatNeedsRehome    = d.player2BoatNeedsRehome;
        p2.ammo               = d.player2Ammo;
        p2.hasMap             = d.player2HasMap;
        p2.hasWaypoint        = d.player2HasWaypoint;
        p2.waypointX          = d.player2WaypointX;
        p2.waypointY          = d.player2WaypointY;
        p2.activeQuest        = d.player2ActiveQuest  ?? new ActiveQuest();
        p2.megaQuest          = d.player2MegaQuest    ?? new MegaQuest();
        p2.openedChests       = d.player2OpenedChests ?? new System.Collections.Generic.List<string>();
        p2.hasHandWeapon      = d.player2HasHandWeapon;
        p2.handAmmo           = d.player2HandAmmo;
        p2.activeHotbarSlot   = d.player2ActiveHotbarSlot;
    }

    /// <summary>Smaže soubor aktuálního slotu (volá se před spuštěním nové hry).</summary>
    public static void DeleteSave()
    {
        WaitForPendingWrites(); // rozepsaný zápis by jinak smazaný soubor "oživil"
        try
        {
            File.Delete(GetPath(CurrentSlot));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Delete error: {e.Message}");
        }
    }

    /// <summary>Existuje v daném slotu uložená hra?</summary>
    public static bool SlotExists(int slot) => File.Exists(GetPath(slot));

    /// <summary>
    /// Načte celá data slotu bez změny CurrentSlot. Vrací null, když slot
    /// neexistuje. (Pro náhled v menu je lehčí PeekSlotSummary.)
    /// </summary>
    public static GameData PeekSlot(int slot)
    {
        WaitForPendingWrites();
        try
        {
            string json = File.ReadAllText(GetPath(slot));
            return JsonUtility.FromJson<GameData>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Rychlý náhled slotu pro hlavní menu (kolik má hráč mincí, ryb, pokladů).
    /// JsonUtility do malé třídy načte jen pár polí a zbytek (tisíce políček
    /// světa) přeskočí. Vrací null, když slot neexistuje.
    /// </summary>
    public static SlotSummary PeekSlotSummary(int slot)
    {
        WaitForPendingWrites();
        try
        {
            string path = GetPath(slot);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            var summary = JsonUtility.FromJson<SlotSummary>(json);
            // v2+ sklady (viz PlayerState) mají čísla pod players[0], ne na
            // nejvyšší úrovni JSONu — top-level pole výš čtou jen staré (v1) savy.
            if (summary.players != null && summary.players.Length > 0)
            {
                summary.coins         = summary.players[0].coins;
                summary.fishCount     = summary.players[0].fishCount;
                summary.treasureCount = summary.players[0].treasureCount;
            }
            return summary;
        }
        catch
        {
            return null;
        }
    }
}
