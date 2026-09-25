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
        GUILayout.Label(Loc.T("DENÍK", "JOURNAL"), titleStyle);
        GUILayout.Space(8);

        var entries = BuildEntries(d);
        if (entries.Count == 0)
        {
            GUILayout.Label(Loc.T("Zatím žádné zápisky. Vrať se sem, až něco dokážeš.",
                                  "No entries yet. Come back once you've accomplished something."), emptyStyle);
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
        GUILayout.Label(Loc.T("[Esc] zavřít deník", "[Esc] close journal"), closeHintStyle);
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
            list.Add(Loc.T(
                "Slíbil jsi starému námořníkovi na startovním ostrově, že mu doneseš 1000 mincí a historický poklad. Teprve pak tě pošle dál.",
                "You promised the old sailor on the starting island that you would bring him 1,000 coins and a historic treasure. Only then will he send you onward."));

        if (d.storyStep >= 2)
            list.Add(Loc.T(
                "Obstál jsi. Starý námořník ti svěřil souřadnice vzdáleného ostrova a ty ses vydal na cestu.",
                "You proved yourself. The old sailor entrusted you with the coordinates of a distant island, and you set sail."));

        if (Reached(d, 0, 1)) list.Add(Loc.T(
            "Ostrov pirátů: zlomil jsi jeho obranu — umlčel jsi děla na hradebních věžích, přemohl ozbrojenou posádku i hlídkovou loď.",
            "Pirate Island: you broke its defenses — silenced the cannons on the wall towers and overpowered the armed crew and the patrol ship."));
        if (Reached(d, 0, 2)) list.Add(Loc.T(
            "Ostrov pirátů: vyřešil jsi hádanku ze tří ozubených kol a otevřel trezor.",
            "Pirate Island: you solved the puzzle of the three gears and opened the vault."));
        if (Reached(d, 0, 3)) list.Add(Loc.T(
            "Ostrov pirátů: v trezoru čekal vzkaz. Přečetl jsi ho a vydal ses po stopě dál.",
            "Pirate Island: a message was waiting in the vault. You read it and followed the trail onward."));

        if (Reached(d, 1, 1)) list.Add(Loc.T(
            "Hřbitov lodí: potopil jsi Bludného Holanďana, který hlídkoval v mělčině.",
            "Ship Graveyard: you sank the Flying Dutchman, who patrolled the shoals."));
        if (Reached(d, 1, 2)) list.Add(Loc.T(
            "Hřbitov lodí: vykopal jsi z mělčiny všechny tři kusy roztržené mapy.",
            "Ship Graveyard: you dug all three pieces of the torn map out of the shoals."));
        if (Reached(d, 1, 3)) list.Add(Loc.T(
            "Hřbitov lodí: prohledal jsi podpalubí vraku a přečetl další vzkaz.",
            "Ship Graveyard: you searched the wreck's hold and read another message."));

        if (Reached(d, 2, 1)) list.Add(Loc.T(
            "Poslední ostrov: potopil jsi loď, která hlídala příjezd.",
            "Final Island: you sank the ship that guarded the approach."));

        if (d.storyDone)
        {
            list.Add(d.storyEnding == 1
                ? Loc.T("Stanul jsi tváří v tvář dědovu ztracenému bratrovi — a rozhodl ses ho ušetřit a vzít domů.",
                        "You came face to face with the old sailor's lost brother — and chose to spare him and take him home.")
                : Loc.T("Stanul jsi tváří v tvář dědovu ztracenému bratrovi. Vzal sis rodinný poklad — a bratr se už domů nevrátí.",
                        "You came face to face with the old sailor's lost brother. You took the family treasure — and the brother will never return home."));
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
