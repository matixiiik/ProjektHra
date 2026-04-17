// Soubor: SaveManager.cs
using UnityEngine;
using System.IO; // Důležité: Potřebné pro File.Exists a Path.Combine

public static class SaveManager
{
    // Cesta k souboru pro uložení dat
    private static string savePath = Path.Combine(Application.persistentDataPath, "gamedata.json");

    // === Uložení hry ===
    public static void SaveGame(GameData data)
    {
        // Převeď třídu GameData do formátu JSON (serializace)
        string json = JsonUtility.ToJson(data, true); 

        try
        {
            File.WriteAllText(savePath, json);
            Debug.Log("Hra Uložena do: " + savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Chyba při ukládání hry: {e.Message}");
        }
    }

    // === Načtení hry ===
    public static GameData LoadGame()
    {
        if (File.Exists(savePath))
        {
            try
            {
                // Načti JSON řetězec ze souboru
                string json = File.ReadAllText(savePath); 
                
                // Převeď JSON zpět do třídy GameData (deserializace)
                GameData loadedData = JsonUtility.FromJson<GameData>(json); 
                
                Debug.Log("Hra Načtena.");
                return loadedData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Chyba při načítání hry, vracím nová data: {e.Message}");
                // V případě chyby (např. poškozený soubor) se vrátí nová hra
                return new GameData(); 
            }
        }
        else
        {
            Debug.Log("Soubor Uložení neexistuje. Vytvářím novou hru.");
            // Pokud soubor neexistuje, začíná se nová hra
            return new GameData(); 
        }
    }
    public static void DeleteSave()
{
    try
    {
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("Save soubor smazán: " + savePath);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Chyba při mazání save: {e.Message}");
    }
}
}