using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  EndScreen.cs
//  Závěrečná obrazovka příběhu (fáze C): pergamen "Konec" se zvoleným koncem,
//  statistikami celé hry a pomalu rolujícími titulky. Hraje k ní klidná hudba
//  (StoryAtmosphere ji pustí, když je obrazovka otevřená).
//
//  Otevře ji StoryNpc, když hráč po finále poprvé dojede za dědou a dočte jeho
//  poslední repliky (GameData.storyEpilogueSeen). Znovu se dá zobrazit z Deníku.
//  Tlačítka: "Plout dál" (volné hraní) a přepínač Těžkého moře (GameData.hardMode
//  — silnější piráti, víc mincí a častější kořist, viz EconomyConfig.Hard…).
//
//  Dokud je otevřená, hra stojí (Time.timeScale = 0) — i ve split-screenu, je to
//  společný konec pro oba hráče.
// ─────────────────────────────────────────────────────────────────────────────

public class EndScreen : MonoBehaviour
{
    private static EndScreen instance;

    /// <summary>Je závěrečná obrazovka otevřená? (blokuje ovládání, pauzu i kameru)</summary>
    public static bool IsOpen => instance != null && instance.open;

    private bool    open;
    private float   openedAt;       // Time.unscaledTime otevření (rolování titulků)
    private float   prevTimeScale = 1f;
    private Vector2 scroll;          // titulky (rolují samy)
    private Vector2 statsScroll;    // statistiky (v malém okně se dají posunout)

    private GUIStyle creditStyle;

    /// <summary>Otevře závěrečnou obrazovku.</summary>
    public static void Show()
    {
        if (instance == null) instance = new GameObject("EndScreen").AddComponent<EndScreen>();
        if (instance.open) return;
        instance.open          = true;
        instance.openedAt      = Time.unscaledTime;
        instance.scroll        = Vector2.zero;
        instance.prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        SoundManager.PlayChapterChord();
    }

    private void Close()
    {
        open = false;
        Time.timeScale = prevTimeScale;
    }

    // Kdyby se scéna zavřela s otevřenou obrazovkou, ať nezůstane zastavený čas.
    void OnDestroy()
    {
        if (open) Time.timeScale = prevTimeScale;
        if (instance == this) instance = null;
    }

    void OnGUI()
    {
        if (!open) return;
        GameData d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null) { Close(); return; }

        GUI.depth = -950; // nad vším kromě dopisu
        HudSkin.UseUiFont();

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float s = HudSkin.GuiScale;
        ShopUI.Prepare(s);
        InitStyles(s);

        float w = Mathf.Min(820f * s, Screen.width - 60f);
        float h = Mathf.Min(760f * s, Screen.height - 50f);
        var sheet = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.DrawTexture(sheet, HudSkin.Parchment(), ScaleMode.StretchToFill);

        float pad = 44f * s;
        GUILayout.BeginArea(new Rect(sheet.x + pad, sheet.y + 24f * s, sheet.width - 2f * pad, sheet.height - 48f * s));

        GUILayout.Label(Loc.T("KONEC", "THE END"), ShopUI.Title);
        GUILayout.Label(EndingLine(d), ShopUI.Subtitle);
        GUILayout.Space(12f * s);

        // Levý sloupec: statistiky. Pravý: rolující titulky. Oba mají stejnou výšku
        // (v malém okně se statistiky dají posunout kolečkem, tlačítka dole zůstanou vidět).
        float creditsH = Mathf.Max(120f, sheet.height - 330f * s);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((sheet.width - 2f * pad) * 0.48f));
        GUILayout.Label(Loc.T("TVOJE PLAVBA", "YOUR VOYAGE"), ShopUI.Section);
        statsScroll = GUILayout.BeginScrollView(statsScroll, GUIStyle.none, GUIStyle.none, GUILayout.Height(creditsH));
        foreach (string line in StatLines(d)) GUILayout.Label(line, ShopUI.Row);
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(16f * s);
        GUILayout.BeginVertical();
        GUILayout.Label(Loc.T("TITULKY", "CREDITS"), ShopUI.Section);
        // Titulky vyjedou zespodu, pomalu projedou nahoru a pak to začne znovu
        // (nad i pod textem je prázdné místo na výšku okna, odtud se text "vynoří").
        float colW     = (sheet.width - 2f * pad) * 0.5f;
        float textH    = creditStyle.CalcHeight(new GUIContent(CreditsText()), colW);
        scroll.y = ((Time.unscaledTime - openedAt) * 22f * s) % (textH + creditsH);
        scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUIStyle.none, GUILayout.Height(creditsH));
        GUILayout.Space(creditsH);
        GUILayout.Label(CreditsText(), creditStyle);
        GUILayout.Space(creditsH);
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();

        // Těžké moře — odemčené dohráním příběhu.
        string hardLabel = d.hardMode
            ? Loc.T("Těžké moře: ZAPNUTO (silnější piráti, víc mincí a kořisti)", "Rough Seas: ON (stronger pirates, more coins and loot)")
            : Loc.T("Těžké moře: vypnuto — klikni pro silnější piráty a lepší odměny", "Rough Seas: off — click for stronger pirates and better rewards");
        if (SoundManager.Click(GUILayout.Button(hardLabel, ShopUI.Button, GUILayout.Height(34f * s))))
        {
            d.hardMode = !d.hardMode;
            var grid = FindFirstObjectByType<GridManager>();
            if (grid != null) grid.Save(); else if (GameSession.Instance != null) GameSession.Instance.Save();
        }
        GUILayout.Space(6f * s);
        if (SoundManager.Click(GUILayout.Button(Loc.T("Plout dál", "Keep sailing"), ShopUI.ClaimButton, GUILayout.Height(40f * s))))
            Close();
        GUILayout.EndArea();
    }

    // Věta pod nadpisem podle zvoleného konce.
    private static string EndingLine(GameData d)
    {
        return d.storyEnding == 1
            ? Loc.T("Bratr se po padesáti letech vrátil domů. Na starém ostrově teď sedí dva.",
                    "After fifty years, the brother came home. Two old men now sit on the old island.")
            : Loc.T("Dědictví je doma. Na útesu Posledního ostrova zůstal jen kříž.",
                    "The inheritance is home. On the cliff of the Final Island, only a cross remains.");
    }

    /// <summary>Řádky statistik (veřejné kvůli testům/Deníku). Oba hráči dohromady.</summary>
    public static string[] StatLines(GameData d)
    {
        int minutes = Mathf.FloorToInt(d.playSeconds / 60f);
        string time = minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        int chests = d.players[0].openedChests.Count + (d.players.Length > 1 ? d.players[1].openedChests.Count : 0);
        int coins  = d.players[0].coins + (MultiplayerManager.IsMultiplayer && d.players.Length > 1 ? d.players[1].coins : 0);

        return new[]
        {
            Loc.T($"Čas na moři: {time}",                     $"Time at sea: {time}"),
            Loc.T($"Chycené ryby: {d.statFishCaught}",        $"Fish caught: {d.statFishCaught}"),
            Loc.T($"Poklady z vraků: {d.statTreasuresDug}",   $"Treasures salvaged: {d.statTreasuresDug}"),
            Loc.T($"Potopení piráti: {d.pirateKills}",        $"Pirates sunk: {d.pirateKills}"),
            Loc.T($"Vyčištěné ostrovy: {d.clearedIslands.Count}", $"Islands cleared: {d.clearedIslands.Count}"),
            Loc.T($"Otevřené bedny: {chests}",                $"Chests opened: {chests}"),
            Loc.T($"Stránky bratrova deníku: {DiaryPages.Count(d)}/{DiaryPages.COUNT}",
                  $"Brother's diary pages: {DiaryPages.Count(d)}/{DiaryPages.COUNT}"),
            Loc.T($"Mince: {coins}",                          $"Coins: {coins}"),
            d.storyEnding == 1 ? Loc.T("Konec: bratr ušetřen", "Ending: the brother spared")
                               : Loc.T("Konec: bratr zabit",   "Ending: the brother killed"),
        };
    }

    private static string CreditsText()
    {
        return Loc.T(
            "THE LAST BEACON\n\n" +
            "Hra, kód a příběh\nmatixiiik\n\n" +
            "Maturitní projekt\n1. IT Gymnázium Satalice\n2026/2027\n\n" +
            "3D modely\nKenney — Pirate Kit, Mini Characters\nQuaternius — Animated Fish Pack\n(CC0)\n\n" +
            "Hudba a zvuky\ngenerované kódem\n(SoundManager.cs)\n\n" +
            "Engine\nUnity 6 · URP\n\n" +
            "Děkuju za hraní!",
            "THE LAST BEACON\n\n" +
            "Game, code and story\nmatixiiik\n\n" +
            "Graduation project\n1. IT Gymnázium Satalice\n2026/2027\n\n" +
            "3D models\nKenney — Pirate Kit, Mini Characters\nQuaternius — Animated Fish Pack\n(CC0)\n\n" +
            "Music and sound\ngenerated in code\n(SoundManager.cs)\n\n" +
            "Engine\nUnity 6 · URP\n\n" +
            "Thank you for playing!");
    }

    private void InitStyles(float s)
    {
        if (creditStyle == null)
            creditStyle = HudSkin.InkAllStates(new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Italic }, HudSkin.Ink);
        creditStyle.fontSize = Mathf.RoundToInt(17f * s);
    }
}
