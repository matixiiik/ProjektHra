using UnityEngine;
using System.Collections.Generic;

public class GameConsole : MonoBehaviour
{
    private bool isOpen = false;
    private string input = "";
    private List<string> log = new List<string>();
    private Vector2 scroll;

    private GridManager grid;
    private PlayerController player;
    private ShipModelSwitcher shipSwitcher;

    private GUIStyle logStyle, inputStyle, promptStyle;
    private bool stylesReady;

    void Start()
    {
        grid        = FindFirstObjectByType<GridManager>();
        player      = FindFirstObjectByType<PlayerController>();
        shipSwitcher = FindFirstObjectByType<ShipModelSwitcher>();

        Log("<color=#44ff44>=== GAME CONSOLE ===</color>");
        Log("Napiš <color=#ffff88>help</color> pro seznam příkazů.");
    }

    void OnGUI()
    {
        // Zachyt ; zde aby se nevepsalo do inputu
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Semicolon)
        {
            isOpen = !isOpen;
            if (isOpen) input = "";
            Event.current.Use();
            return;
        }

        if (!isOpen) return;
        InitStyles();

        float w = 460, h = 300;
        float px = 10, py = 10;

        // Pozadi
        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.9f, 0.2f);
        GUI.DrawTexture(new Rect(px, py, w, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(px, py + h - 2, w, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 8, py + 8, w - 16, h - 16));

        // Log
        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUIStyle.none, GUILayout.Height(238));
        foreach (var line in log)
            GUILayout.Label(line, logStyle);
        GUILayout.EndScrollView();

        GUILayout.Space(2);

        // Input radek
        GUILayout.BeginHorizontal();
        GUILayout.Label(">", promptStyle, GUILayout.Width(14));
        GUI.SetNextControlName("ConsoleInput");
        input = GUILayout.TextField(input, inputStyle);
        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        // Fokus na input
        if (Event.current.type == EventType.Repaint)
            GUI.FocusControl("ConsoleInput");

        // Enter = spust prikaz
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && input.Trim() != "")
        {
            ExecuteCommand(input.Trim());
            input = "";
            Event.current.Use();
        }
    }

    void Log(string msg)
    {
        log.Add(msg);
        if (log.Count > 80) log.RemoveAt(0);
        scroll = new Vector2(0, float.MaxValue);
    }

    void ExecuteCommand(string raw)
    {
        Log($"<color=#88ff88>> {raw}</color>");
        string[] p = raw.ToLower().Trim().Split(' ');

        switch (p[0])
        {
            case "help":
                Log("──────────────────────────────");
                Log("<color=#ffff88>get money</color> <castka>          přidá mince");
                Log("<color=#ffff88>get fish</color> <pocet>            přidá ryby");
                Log("<color=#ffff88>get treasure</color> <pocet>        přidá poklady");
                Log("<color=#ffff88>get boat</color> small/medium/large  změní loď");
                Log("<color=#ffff88>upgrade</color> speed/rod/mining     odemkne upgrade");
                Log("<color=#ffff88>tp</color> <x> <y>                  teleport");
                Log("<color=#ffff88>explore</color> [radius]            odhalí mapu");
                Log("<color=#ffff88>reset money</color>                  vynuluje mince");
                Log("<color=#ffff88>clear</color>                        vymaže konzoli");
                Log("──────────────────────────────");
                break;

            case "get":    HandleGet(p);     break;
            case "upgrade": HandleUpgrade(p); break;
            case "tp":     HandleTp(p);      break;
            case "explore": HandleExplore(p); break;
            case "reset":  HandleReset(p);   break;
            case "clear":  log.Clear();      break;

            default:
                Log($"<color=#ff6666>Neznámý příkaz: {p[0]}</color>  (napiš <color=#ffff88>help</color>)");
                break;
        }
    }

    void HandleGet(string[] p)
    {
        if (p.Length < 2) { Log("Použití: get <money/fish/treasure/boat> ..."); return; }

        switch (p[1])
        {
            case "money":
                if (p.Length < 3 || !int.TryParse(p[2], out int coins)) { Log("Použití: get money <částka>"); return; }
                grid.gameData.coins += coins;
                Log($"<color=#ffdd44>+{coins} mincí</color>  (celkem: {grid.gameData.coins})");
                break;

            case "fish":
                if (p.Length < 3 || !int.TryParse(p[2], out int fish)) { Log("Použití: get fish <počet>"); return; }
                grid.gameData.fishCount += fish;
                Log($"<color=#44ddff>+{fish} ryb</color>  (celkem: {grid.gameData.fishCount})");
                break;

            case "treasure":
                if (p.Length < 3 || !int.TryParse(p[2], out int tr)) { Log("Použití: get treasure <počet>"); return; }
                grid.gameData.treasureCount += tr;
                Log($"<color=#ffaa22>+{tr} pokladů</color>  (celkem: {grid.gameData.treasureCount})");
                break;

            case "boat":
                if (p.Length < 3) { Log("Použití: get boat <small/medium/large>"); return; }
                int level = p[2] == "small" ? 0 : p[2] == "medium" ? 1 : p[2] == "large" ? 2 : -1;
                if (level < 0) { Log("Neznámá loď — použij: small, medium, large"); return; }
                grid.gameData.shipLevel = level;
                shipSwitcher?.Apply();
                Log($"Loď změněna na: <color=#44ff44>{p[2]}</color>");
                break;

            default:
                Log($"Neznámý typ: {p[1]}");
                return;
        }

        grid.Save();
        grid.NotifyWorldChanged();
    }

    void HandleUpgrade(string[] p)
    {
        if (p.Length < 2) { Log("Použití: upgrade <speed/rod/mining>"); return; }

        switch (p[1])
        {
            case "speed":   grid.gameData.hasSpeedUpgrade  = true; Log("✓ Upgrade: <color=#44ff44>rychlost lodi</color> odemčena"); break;
            case "rod":     grid.gameData.hasRodUpgrade    = true; Log("✓ Upgrade: <color=#44ff44>lepší prut</color> odemčen");    break;
            case "mining":  grid.gameData.hasMiningUpgrade = true; Log("✓ Upgrade: <color=#44ff44>rychlost těžby</color> odemčena"); break;
            default: Log($"Neznámý upgrade: {p[1]}  (speed / rod / mining)"); return;
        }

        grid.Save();
        grid.NotifyWorldChanged();
    }

    void HandleTp(string[] p)
    {
        if (p.Length < 3 || !int.TryParse(p[1], out int x) || !int.TryParse(p[2], out int y))
        { Log("Použití: tp <x> <y>"); return; }

        player.TeleportTo(x, y);
        Log($"Teleport → [{x}, {y}]");
    }

    void HandleExplore(string[] p)
    {
        int radius = 25;
        if (p.Length >= 2) int.TryParse(p[1], out radius);
        grid.MarkAreaExplored(grid.gameData.playerGridX, grid.gameData.playerGridY, radius);
        Log($"Odkryto oblast {radius * 2 + 1}×{radius * 2 + 1}");
    }

    void HandleReset(string[] p)
    {
        if (p.Length < 2) { Log("Použití: reset money"); return; }
        if (p[1] == "money") { grid.gameData.coins = 0; grid.Save(); grid.NotifyWorldChanged(); Log("Mince vynulovány."); }
        else Log($"Neznámý reset: {p[1]}");
    }

    void InitStyles()
    {
        if (stylesReady) return;

        logStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            richText = true,
            wordWrap = true,
            normal = { textColor = new Color(0.85f, 0.95f, 0.85f) }
        };
        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.3f, 1f, 0.3f) }
        };
        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal   = { textColor = Color.green, background = MakeTex(new Color(0.04f, 0.1f, 0.04f)) },
            focused  = { textColor = Color.green, background = MakeTex(new Color(0.04f, 0.1f, 0.04f)) }
        };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
