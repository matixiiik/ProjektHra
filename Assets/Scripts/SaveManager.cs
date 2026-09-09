using UnityEngine;
using System.IO;

// ─────────────────────────────────────────────────────────────────────────────
//  SaveManager.cs
//  Ukládání a načítání hry do/z JSON souboru na disku.
//  Statická třída = nemusí být na žádném objektu ve scéně, volá se přímo
//  SaveManager.SaveGame(...) / SaveManager.LoadGame().
//
//  Hra má 3 nezávislé sloty (0, 1, 2). Každý slot je vlastní soubor:
//      save_0.json, save_1.json, save_2.json
//  ve složce Application.persistentDataPath (na Windows: %AppData%/../LocalLow/...).
// ─────────────────────────────────────────────────────────────────────────────

public static class SaveManager
{
    /// <summary>Slot, se kterým se právě pracuje (nastavuje ho menu / GridManager).</summary>
    public static int CurrentSlot { get; set; } = 0;

    // Sestaví celou cestu k souboru daného slotu.
    private static string GetPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    /// <summary>Uloží data do souboru aktuálního slotu.</summary>
    public static void SaveGame(GameData data)
    {
        try
        {
            // JsonUtility.ToJson(data, true) → čitelný JSON s odsazením
            File.WriteAllText(GetPath(CurrentSlot), JsonUtility.ToJson(data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
        }
    }

    /// <summary>
    /// Načte data aktuálního slotu. Když soubor neexistuje nebo je poškozený,
    /// vrátí čerstvá výchozí data (= nová hra).
    /// </summary>
    public static GameData LoadGame()
    {
        if (!File.Exists(GetPath(CurrentSlot)))
            return new GameData();

        try
        {
            string json = File.ReadAllText(GetPath(CurrentSlot));
            // ?? new GameData() ošetří případ, kdy je JSON prázdný / null
            return JsonUtility.FromJson<GameData>(json) ?? new GameData();
        }
        catch (System.Exception e)
        {
            // Soubor existuje, ale nejde přečíst (poškozený) — začni novou hru.
            Debug.LogWarning($"Save slotu {CurrentSlot} je poškozený, spouštím novou hru. ({e.Message})");
            return new GameData();
        }
    }

    /// <summary>Smaže soubor aktuálního slotu (volá se před spuštěním nové hry).</summary>
    public static void DeleteSave()
    {
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
    /// Načte data slotu bez změny CurrentSlot — používá menu k zobrazení náhledu
    /// (kolik má hráč mincí, ryb...). Vrací null, když slot neexistuje.
    /// </summary>
    public static GameData PeekSlot(int slot)
    {
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
}
