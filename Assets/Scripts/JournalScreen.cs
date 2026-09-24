using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  JournalScreen.cs
//  Deník — přehled příběhového postupu, otevírá se tlačítkem v PauseMenu.
//  Ukazuje VÝHRADNĚ zápisky o tom, co hráč UŽ udělal (odvozeno z gameData.
//  storyStep/megaIndex/megaTask/storyDone) — nikdy nic o tom, co ho teprve
//  čeká, ať to nespoiluje další ostrovy. Čistě informativní, na gameData
//  se jen čte, nic se tu nemění ani neukládá.
//
//  Hra je v tu chvíli stejně zastavená (PauseMenu má otevřeno, Time.timeScale
//  = 0), takže žádné vlastní zamykání ovládání hráče není potřeba — jen si
//  Escape bere pro sebe (zavře deník, ne rovnou celou pauzu), viz PauseMenu.
// ─────────────────────────────────────────────────────────────────────────────

public class JournalScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    private static JournalScreen instance;

    private GUIStyle titleStyle, entryStyle, emptyStyle, closeHintStyle;
    private bool     stylesReady;
    private Vector2  scroll;

    public static void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (instance == null) instance = new GameObject("JournalScreen").AddComponent<JournalScreen>();
        IsOpen = true;
    }

    public static void Close() => IsOpen = false;

    void OnGUI()
    {
        if (!IsOpen) return;
        InitStyles();

        GameData d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null) return;

        // Tmavý overlay přes celou obrazovku (PauseMenu se, dokud je deník
        // otevřený, sám nekreslí — viz jeho OnGUI).
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(560f, Screen.width - 80f);
        float h = Mathf.Min(520f, Screen.height - 80f);
        var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.75f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(box.x + 22f, box.y + 16f, box.width - 44f, box.height - 32f));
        GUILayout.Label("DENÍK", titleStyle);
        GUILayout.Space(8);

        var entries = BuildEntries(d);
        if (entries.Count == 0)
        {
            GUILayout.Label("Zatím žádné zápisky. Vrať se sem, až něco dokážeš.", emptyStyle);
        }
        else
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(box.height - 90f));
            foreach (string e in entries)
            {
                GUILayout.Label("•  " + e, entryStyle);
                GUILayout.Space(6);
            }
            GUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("[Esc] zavřít deník", closeHintStyle);
        GUILayout.EndArea();

        // Escape patří deníku (zavře jen jeho) — spotřebuj event, ať ho hned
        // po zavření nechytí i PauseMenu (ten deník při otevření hlídá zvlášť,
        // viz PauseMenu.Update()).
        Event ev = Event.current;
        if (ev != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
        {
            Close();
            ev.Use();
        }
    }

    // Sestaví zápisky jen za to, co hráč UŽ prošel — žádný náznak toho, co ho
    // čeká dál (žádné jméno ostrova, dokud na něj hráč fakticky nedorazí).
    private List<string> BuildEntries(GameData d)
    {
        var list = new List<string>();

        if (d.storyStep >= 1)
            list.Add("Slíbil jsi starému námořníkovi u startovního ostrova, že mu doneseš 1000 mincí a historický poklad, než tě pošle dál.");

        if (d.storyStep >= 2)
            list.Add("Prokázal ses. Dostal jsi souřadnice vzdáleného ostrova a vydal ses za nimi.");

        if (Reached(d, 0, 1)) list.Add("Ostrov pirátů: rozbil jsi jeho obranu — děla na hradebních věžích, ozbrojenou posádku i hlídkovou loď.");
        if (Reached(d, 0, 2)) list.Add("Ostrov pirátů: vyřešil jsi hádanku tří ozubených kol a otevřel trezor.");
        if (Reached(d, 0, 3)) list.Add("Ostrov pirátů: přečetl jsi vzkaz, co v trezoru čekal, a vydal ses po stopě dál.");

        if (Reached(d, 1, 1)) list.Add("Hřbitov lodí: potopil jsi Bludného Holanďana, co hlídkoval v mělčině.");
        if (Reached(d, 1, 2)) list.Add("Hřbitov lodí: vylovil jsi z vody všechny tři kusy roztržené mapy.");
        if (Reached(d, 1, 3)) list.Add("Hřbitov lodí: prohledal jsi podpalubí vraku a přečetl další vzkaz.");

        if (Reached(d, 2, 1)) list.Add("Poslední ostrov: potopil jsi loď, co hlídala příjezd.");

        if (d.storyDone)
        {
            list.Add(d.storyEnding == 1
                ? "Stanul jsi tváří v tvář dědovu ztracenému bratrovi — a rozhodl ses ho ušetřit a vzít domů."
                : "Stanul jsi tváří v tvář dědovu ztracenému bratrovi — a rozhodl ses vzít rodinné dědictví a nechat ho být.");
        }

        return list;
    }

    // Ostrov v pořadí `idx` (0/1/2) je aspoň na kroku `minTask` — buď je to
    // ten AKTUÁLNÍ ostrov s dost vysokým megaTask, nebo je hráč už dávno za
    // ním (vyšší megaIndex = tenhle ostrov dokončil celý, platí i tenhle krok).
    private static bool Reached(GameData d, int idx, int minTask)
        => d.megaIndex > idx || (d.megaIndex == idx && d.megaTask >= minTask);

    private void InitStyles()
    {
        if (stylesReady) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.65f) }
        };
        entryStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, wordWrap = true,
            normal = { textColor = new Color(0.92f, 0.92f, 0.9f) }
        };
        emptyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, wordWrap = true, fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.7f, 0.7f, 0.68f) }
        };
        closeHintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.75f, 0.78f, 0.82f) }
        };
        stylesReady = true;
    }
}
