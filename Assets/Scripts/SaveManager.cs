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
            // ?? new GameData() ošetří případ, kdy je JSON prázdný / null
            GameData data = JsonUtility.FromJson<GameData>(json) ?? new GameData();
            MigrateIfNeeded(data);
            return data;
        }
        catch (System.Exception e)
        {
            // Soubor existuje, ale nejde přečíst (poškozený) — začni novou hru.
            Debug.LogWarning($"Save slotu {CurrentSlot} je poškozený, spouštím novou hru. ({e.Message})");
            return new GameData();
        }
    }

    /// <summary>
    /// Posune stará save data na aktuální verzi formátu. Zatím žádná verze
    /// nevyžaduje skutečnou přeměnu dat (nová pole si JsonUtility doplní sama
    /// jako 0/false) — jen se poznamená, že save je "normalizovaný". Až
    /// nějaká budoucí změna bude vyžadovat přemapování existujícího pole
    /// (ne jen přidání nového), přibude sem konkrétní krok podle
    /// data.saveVersion.
    /// </summary>
    private static void MigrateIfNeeded(GameData data)
    {
        if (data.saveVersion >= GameData.CURRENT_SAVE_VERSION) return;

        Debug.Log($"Save slotu {CurrentSlot}: migrace z verze {data.saveVersion} na {GameData.CURRENT_SAVE_VERSION}.");
        data.saveVersion = GameData.CURRENT_SAVE_VERSION;
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
    /// JsonUtility do malé třídy načte jen tři pole a zbytek (tisíce políček
    /// světa) přeskočí. Vrací null, když slot neexistuje.
    /// </summary>
    public static SlotSummary PeekSlotSummary(int slot)
    {
        WaitForPendingWrites();
        try
        {
            string path = GetPath(slot);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SlotSummary>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }
}
