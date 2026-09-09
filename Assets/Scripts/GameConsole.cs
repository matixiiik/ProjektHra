using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  GameConsole.cs
//  Vývojářská / testovací konzole. Otevírá se klávesou ` (nad Tabem).
//  Umožňuje si přidat mince, ryby, poklady, odemknout upgrady, teleportovat se
//  a odkrýt mapu — aby se hra nemusela hrát celá znovu při testování.
//
//  Kreslí se přes Unity IMGUI (OnGUI). PlayerController se dívá na IsOpen a
//  když je konzole otevřená, zablokuje ovládání hráče.
// ─────────────────────────────────────────────────────────────────────────────

public class GameConsole : MonoBehaviour
{
    /// <summary>Je konzole zrovna otevřená? (blokuje pohyb hráče)</summary>
    public static bool IsOpen { get; private set; }

    private string        input = "";               // co hráč právě píše
    private List<string>  log   = new List<string>(); // vypsané řádky
    private Vector2       scroll;                    // pozice posuvníku ve výpisu

    private GridManager       grid;
    private PlayerController  player;
    private ShipModelSwitcher shipSwitcher;

    private GUIStyle logStyle, inputStyle, promptStyle;
    private bool     stylesReady;

    void Start()
    {
        grid         = FindFirstObjectByType<GridManager>();
        player       = FindFirstObjectByType<PlayerController>();
        shipSwitcher = FindFirstObjectByType<ShipModelSwitcher>();

        Log("<color=#44ff44>=== GAME CONSOLE ===</color>");
        Log("Napiš <color=#ffff88>help</color> pro seznam příkazů.");
    }

    void Update()
    {
        // Klávesa ` (BackQuote) — otevři/zavři konzoli.
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            IsOpen = !IsOpen;
            if (IsOpen) input = "";
        }
    }

    void OnGUI()
    {
        if (!IsOpen) return;
        InitStyles();

        // Odchycení Enteru přes Event (v OnGUI spolehlivější než Input v Update).
        Event e = Event.current;
        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
            input.Trim() != "")
        {
            ExecuteCommand(input.Trim());
            input = "";
            e.Use(); // "spotřebuj" událost, ať Enter nedělá nic dalšího
        }

        float w = 460, h = 300;
        float px = 10, py = 10;

        // Tmavé pozadí konzole.
        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 8, py + 8, w - 16, h - 16));

        // Výpis řádků (posuvatelný).
        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUIStyle.none, GUILayout.Height(238));
        foreach (var line in log)
            GUILayout.Label(line, logStyle);
        GUILayout.EndScrollView();

        GUILayout.Space(2);

        // Řádek pro psaní.
        GUILayout.BeginHorizontal();
        GUILayout.Label(">", promptStyle, GUILayout.Width(14));
        GUI.SetNextControlName("ConsoleInput");
        input = GUILayout.TextField(input, inputStyle);
        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        // Drž kurzor pořád v textovém poli, ať může hráč hned psát.
        GUI.FocusControl("ConsoleInput");
    }

    // Přidá řádek do výpisu a odscrolluje dolů. Drží max 80 řádků.
    void Log(string msg)
    {
        log.Add(msg);
        if (log.Count > 80) log.RemoveAt(0);
        scroll = new Vector2(0, float.MaxValue);
    }

    // ── Zpracování příkazu ───────────────────────────────────────────────────
    void ExecuteCommand(string raw)
    {
        Log($"<color=#88ff88>> {raw}</color>");

        // Rozděl vstup na slova: p[0] = příkaz, p[1..] = argumenty.
        string[] p = raw.ToLower().Trim().Split(' ');

        switch (p[0])
        {
            case "help":
                Log("──────────────────────────────");
                Log("<color=#ffff88>get money</color> <castka>           přidá mince");
                Log("<color=#ffff88>get fish</color> <pocet>             přidá ryby");
                Log("<color=#ffff88>get treasure</color> <pocet>         přidá poklady");
                Log("<color=#ffff88>get boat</color> row/small/medium/large   změní loď");
                Log("<color=#ffff88>get item</color> map/ammo/histtreasure/sellbonus/megamap");
                Log("<color=#ffff88>upgrade</color> speed/rod/mining      odemkne upgrade");
                Log("<color=#ffff88>tp</color> <x> <y>                   teleport");
                Log("<color=#ffff88>explore</color> [radius]             odhalí mapu");
                Log("<color=#ffff88>locate</color> [fish/treasure/chest/island/quest]  najde nejbližší");
                Log("<color=#ffff88>respawn</color>                       oživí hráče u nejbližšího ostrova");
                Log("<color=#ffff88>story</color> [krok / island / histtreasure]   příběh (test)");
                Log("<color=#ffff88>reset money</color>                   vynuluje mince");
                Log("<color=#ffff88>clear</color>                         vymaže konzoli");
                Log("──────────────────────────────");
                break;

            case "get":     HandleGet(p);     break;
            case "upgrade": HandleUpgrade(p); break;
            case "tp":      HandleTp(p);      break;
            case "explore": HandleExplore(p); break;
            case "locate":  HandleLocate(p);  break;
            case "respawn": HandleRespawn();  break;
            case "story":   HandleStory(p);   break;
            case "reset":   HandleReset(p);   break;
            case "clear":   log.Clear();      break;

            default:
                Log($"<color=#ff6666>Neznámý příkaz: {p[0]}</color>  (napiš <color=#ffff88>help</color>)");
                break;
        }
    }

    // get money/fish/treasure/boat/item <hodnota>
    void HandleGet(string[] p)
    {
        if (p.Length < 2) { Log("Použití: get <money/fish/treasure/boat/item> ..."); return; }

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
                if (p.Length < 3) { Log("Použití: get boat <row/small/medium/large>"); return; }
                int level = p[2] == "row" ? 0 : p[2] == "small" ? 1 : p[2] == "medium" ? 2 : p[2] == "large" ? 3 : -1;
                if (level < 0) { Log("Neznámá loď — použij: row, small, medium, large"); return; }
                grid.gameData.shipLevel = level;
                shipSwitcher?.Apply();
                Log($"Loď změněna na: <color=#44ff44>{p[2]}</color>");
                break;

            case "item":
                if (!HandleGetItem(p)) return; // vypsalo si vlastní chybu
                break;

            default:
                Log($"Neznámý typ: {p[1]}  (money/fish/treasure/boat/item)");
                return;
        }

        // Po každé úspěšné změně ulož a dej vědět HUD/minimapě.
        grid.Save();
        grid.NotifyWorldChanged();
    }

    // get item <map/ammo/histtreasure/sellbonus/megamap/map>
    // Vrací false, když nic nepřidal (chybu si vypíše sám).
    bool HandleGetItem(string[] p)
    {
        var d = grid.gameData;
        if (p.Length < 3)
        {
            Log("Použití: get item <map / ammo [pocet] / histtreasure / sellbonus / megamap>");
            return false;
        }

        switch (p[2])
        {
            case "map":
                d.hasMap = true;
                Log("<color=#44ddff>Mapa</color> — v lodi klávesa M otevře velkou mapu.");
                return true;

            case "ammo":
                int n = 20;
                if (p.Length >= 4) int.TryParse(p[3], out n);
                d.ammo += n;
                Log($"<color=#cccccc>+{n} nábojů</color>  (celkem: {d.ammo})");
                return true;

            case "histtreasure":
            case "historicky":
                d.hasHistoricalTreasure = true;
                Log("<color=#88ddff>Historický poklad</color> — chce ho starý námořník (příběh).");
                return true;

            case "sellbonus":
                d.sellBonus = true;
                Log("<color=#66ff66>Trvalý bonus na výkup</color> odemčen.");
                return true;

            case "megamap":
            case "megaquest":
                if (d.megaQuest.active) { Log("Mega quest už máš rozdělaný."); return false; }
                d.megaQuest.active      = true;
                d.megaQuest.dug         = false;
                d.megaQuest.targetX     = d.playerGridX + 20;
                d.megaQuest.targetY     = d.playerGridY + 15;
                d.megaQuest.rewardCoins = 600;
                d.megaQuest.grantsHistoricalTreasure = true;
                Log($"<color=#ffcc44>Mega quest (mapa)</color> — poklad na [{d.megaQuest.targetX}, {d.megaQuest.targetY}], historický.");
                return true;

            default:
                Log($"Neznámá věc: {p[2]}  (map / ammo / histtreasure / sellbonus / megamap)");
                return false;
        }
    }

    // upgrade speed/rod/mining
    void HandleUpgrade(string[] p)
    {
        if (p.Length < 2) { Log("Použití: upgrade <speed/rod/mining>"); return; }

        switch (p[1])
        {
            case "speed":  grid.gameData.hasSpeedUpgrade  = true; Log("✓ <color=#44ff44>Rychlost lodi</color> odemčena"); break;
            case "rod":    grid.gameData.hasRodUpgrade    = true; Log("✓ <color=#44ff44>Lepší prut</color> odemčen");    break;
            case "mining": grid.gameData.hasMiningUpgrade = true; Log("✓ <color=#44ff44>Rychlost těžby</color> odemčena"); break;
            default: Log($"Neznámý upgrade: {p[1]}  (speed / rod / mining)"); return;
        }

        grid.Save();
        grid.NotifyWorldChanged();
    }

    // tp <x> <y>
    void HandleTp(string[] p)
    {
        if (p.Length < 3 || !int.TryParse(p[1], out int x) || !int.TryParse(p[2], out int y))
        { Log("Použití: tp <x> <y>"); return; }

        player.TeleportTo(x, y);
        Log($"Teleport → [{x}, {y}]");
    }

    // explore [radius] — odkryje mlhu kolem hráče
    void HandleExplore(string[] p)
    {
        int radius = 25;
        if (p.Length >= 2) int.TryParse(p[1], out radius);
        grid.MarkAreaExplored(grid.gameData.playerGridX, grid.gameData.playerGridY, radius);
        Log($"Odkryto oblast {radius * 2 + 1}×{radius * 2 + 1}");
    }

    // locate [typ] — vypíše, kde je nejbližší hledaná věc (směr + vzdálenost)
    void HandleLocate(string[] p)
    {
        int px = grid.gameData.playerGridX;
        int py = grid.gameData.playerGridY;

        // Bez argumentu → vypiš nejbližší od každého druhu.
        if (p.Length < 2)
        {
            ReportNearest("ryby",    grid.NearestTileOfType(px, py, TileType.Water_Fish), px, py);
            ReportNearest("poklad",  grid.NearestTileOfType(px, py, TileType.Treasure),   px, py);
            ReportNearest("bedna",   grid.NearestTileOfType(px, py, TileType.Chest),      px, py);
            ReportNearest("ostrov",  grid.NearestTileOfType(px, py, TileType.Harbor),     px, py);
            return;
        }

        switch (p[1])
        {
            case "fish":     ReportNearest("ryby",   grid.NearestTileOfType(px, py, TileType.Water_Fish), px, py); break;
            case "treasure": ReportNearest("poklad", grid.NearestTileOfType(px, py, TileType.Treasure),   px, py); break;
            case "chest":    ReportNearest("bedna",  grid.NearestTileOfType(px, py, TileType.Chest),      px, py); break;
            case "island":   ReportNearest("ostrov", grid.NearestTileOfType(px, py, TileType.Harbor),     px, py); break;
            case "quest":
                var mq = grid.gameData.megaQuest;
                if (mq != null && mq.active && !mq.dug)
                    ReportNearest("poklad z mapy", new Vector2Int(mq.targetX, mq.targetY), px, py);
                else
                    Log("<color=#ffcc66>Žádný rozdělaný mega quest.</color>");
                break;
            default:
                Log("Použití: locate <fish/treasure/chest/island/quest>");
                break;
        }
    }

    // Vypíše jeden řádek "název: směr, vzdálenost" (nebo že nic není).
    void ReportNearest(string name, Vector2Int? target, int px, int py)
    {
        if (target == null) { Log($"<color=#888888>{name}: nic v okolí není</color>"); return; }

        int dx = target.Value.x - px;
        int dy = target.Value.y - py;
        int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        // Světová strana (S = +Y nahoru na mapě).
        string ns = dy > 0 ? "S" : dy < 0 ? "J" : "";
        string ew = dx > 0 ? "V" : dx < 0 ? "Z" : "";
        string dir = (ns + ew) == "" ? "tady" : ns + ew;

        Log($"<color=#88ddff>{name}</color>: {dir}, {dist} políček  <color=#888888>[{target.Value.x}, {target.Value.y}]</color>");
    }

    // respawn — oživí hráče u nejbližšího ostrova (jako tlačítko na obrazovce smrti)
    void HandleRespawn()
    {
        grid.RespawnPlayerAtNearestIsland(0);
        if (player != null) player.ReloadFromData();
        Log("<color=#44ff44>Respawn</color> — veslice u nejbližšího ostrova (kořist a vylepšení pryč, mince zůstaly).");
    }

    // story — testovací ovládání příběhu
    void HandleStory(string[] p)
    {
        var d = grid.gameData;
        if (p.Length < 2)
        {
            Log($"storyStep = {d.storyStep}, historicky poklad = {d.hasHistoricalTreasure}, "
              + $"mega ostrov = {(d.storyIslandActive ? $"[{d.storyIslandX}, {d.storyIslandY}]" : "-")}");
            return;
        }

        if (p[1] == "histtreasure")
        {
            d.hasHistoricalTreasure = true;
            grid.Save(); grid.NotifyWorldChanged();
            Log("Máš historický poklad.");
        }
        else if (p[1] == "island")
        {
            int sx = d.playerGridX + 60, sy = d.playerGridY + 40;
            grid.PlaceMegaIsland(sx, sy);
            d.storyStep = 2; d.hasWaypoint = true; d.waypointX = sx; d.waypointY = sy;
            grid.Save(); grid.NotifyWorldChanged();
            Log($"Mega ostrov na [{sx}, {sy}], storyStep=2, waypoint nastaven.");
        }
        else if (int.TryParse(p[1], out int step))
        {
            d.storyStep = Mathf.Clamp(step, 0, 9);
            grid.Save(); grid.NotifyWorldChanged();
            Log($"storyStep = {d.storyStep}");
        }
        else Log("Použití: story  |  story <krok 0-9>  |  story island  |  story histtreasure");
    }

    // reset money — vynuluje mince
    void HandleReset(string[] p)
    {
        if (p.Length < 2) { Log("Použití: reset money"); return; }
        if (p[1] == "money")
        {
            grid.gameData.coins = 0;
            grid.Save();
            grid.NotifyWorldChanged();
            Log("Mince vynulovány.");
        }
        else Log($"Neznámý reset: {p[1]}");
    }

    // ── Styly (vytvoří se jen jednou) ────────────────────────────────────────
    void InitStyles()
    {
        if (stylesReady) return;

        logStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            richText = true,   // povolí <color=...> tagy
            wordWrap = true,
            normal   = { textColor = new Color(0.85f, 0.95f, 0.85f) }
        };
        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(0.3f, 1f, 0.3f) }
        };
        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            normal  = { textColor = Color.green, background = MakeTex(new Color(0.04f, 0.1f, 0.04f)) },
            focused = { textColor = Color.green, background = MakeTex(new Color(0.04f, 0.1f, 0.04f)) }
        };

        stylesReady = true;
    }

    // Vytvoří jednobarevnou texturu 1×1 (pro pozadí prvků).
    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
