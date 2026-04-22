using UnityEngine;
using System.IO;

public static class SaveManager
{
    private static string savePath = Path.Combine(Application.persistentDataPath, "gamedata.json");

    public static void SaveGame(GameData data)
    {
        string json = JsonUtility.ToJson(data, true);
        try
        {
            File.WriteAllText(savePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Chyba při ukládání hry: {e.Message}");
        }
    }

    public static GameData LoadGame()
    {
        try
        {
            string json = File.ReadAllText(savePath);
            return JsonUtility.FromJson<GameData>(json);
        }
        catch
        {
            return new GameData();
        }
    }

    public static void DeleteSave()
    {
        try
        {
            File.Delete(savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Chyba při mazání save: {e.Message}");
        }
    }
}
