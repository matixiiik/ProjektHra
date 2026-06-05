using UnityEngine;
using System.IO;

public static class SaveManager
{
    public static int CurrentSlot { get; set; } = 0;

    private static string GetPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    public static void SaveGame(GameData data)
    {
        try { File.WriteAllText(GetPath(CurrentSlot), JsonUtility.ToJson(data, true)); }
        catch (System.Exception e) { Debug.LogError($"Save error: {e.Message}"); }
    }

    public static GameData LoadGame()
    {
        try
        {
            string json = File.ReadAllText(GetPath(CurrentSlot));
            return JsonUtility.FromJson<GameData>(json) ?? new GameData();
        }
        catch { return new GameData(); }
    }

    public static void DeleteSave()
    {
        try { File.Delete(GetPath(CurrentSlot)); }
        catch (System.Exception e) { Debug.LogError($"Delete error: {e.Message}"); }
    }

    public static bool SlotExists(int slot) => File.Exists(GetPath(slot));

    // Načte data slotu bez změny CurrentSlot (pro náhled v menu)
    public static GameData PeekSlot(int slot)
    {
        try
        {
            string json = File.ReadAllText(GetPath(slot));
            return JsonUtility.FromJson<GameData>(json);
        }
        catch { return null; }
    }
}
