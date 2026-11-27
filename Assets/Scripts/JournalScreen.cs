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
//  Nahoře je název KAPITOLY, ve které hráč zrovna je, a tlačítko Nápověda —
//  rada, co dělat teď. Nápověda se ukáže až po kliknutí (kdo chce hrát bez
//  rad, nic neuvidí) a mluví jen o aktuálním kroku, ne o dalších ostrovech.
//  Vzhled: pergamen jako dopis a obchody (HudSkin.Parchment + styly ShopUI).
//
//  Hra je v tu chvíli stejně zastavená (PauseMenu má otevřeno, Time.timeScale
//  = 0), takže žádné vlastní zamykání ovládání hráče není potřeba — jen si
//  Escape bere pro sebe (zavře deník, ne rovnou celou pauzu), viz PauseMenu.
// ─────────────────────────────────────────────────────────────────────────────

public class JournalScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    private static JournalScreen instance;

    private Vector2  scroll;
    private bool     showHint;   // hráč klikl na "Nápověda" (při dalším otevření deníku je zase schovaná)

    public static void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (instance == null) instance = new GameObject("JournalScreen").AddComponent<JournalScreen>();
        instance.showHint = false;
        IsOpen = true;
    }

    public static void Close() => IsOpen = false;

    void OnGUI()
    {
        if (!IsOpen) return;
        GUI.depth = -900; // nad pauzou a HUD, pod dopisem
        HudSkin.UseUiFont();

        GameData d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null) return;

        // Tmavý overlay přes celou obrazovku (PauseMenu se, dokud je deník
        // otevřený, sám nekreslí — viz jeho OnGUI).
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float s = HudSkin.GuiScale;
        ShopUI.Prepare(s);

        float w = Mathf.Min(720f * s, Screen.width - 60f);
        float h = Mathf.Min(680f * s, Screen.height - 50f);
        var sheet = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.DrawTexture(sheet, HudSkin.Parchment(), ScaleMode.StretchToFill);

        float pad = 40f * s;
        GUILayout.BeginArea(new Rect(sheet.x + pad, sheet.y + 22f * s, sheet.width - 2f * pad, sheet.height - 44f * s));
        GUILayout.Label(Loc.T("DENÍK", "JOURNAL"), ShopUI.Title);
        GUILayout.Label(ChapterTitle(d), ShopUI.Subtitle);
        GUILayout.Space(10f * s);

        // Zápisky o tom, co už hráč dokázal.
        var entries = BuildEntries(d);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        if (entries.Count == 0)
        {
            GUILayout.Label(Loc.T("Zatím žádné zápisky. Vrať se sem, až něco dokážeš.",
                                  "No entries yet. Come back once you've accomplished something."), ShopUI.Faint);
        }
        else
        {
            foreach (string e in entries)
            {
                GUILayout.Label("•  " + e, ShopUI.Row);
                GUILayout.Space(6f * s);
            }
        }

        // Stránky bratrova deníku (sběratelské, DiaryPages) — jen ty sebrané, ostatní jako "chybí".
        int pages = DiaryPages.Count(d);
        if (pages > 0)
        {
            GUILayout.Space(8f * s);
            GUILayout.Label(Loc.T($"BRATRŮV DENÍK  ({pages}/{DiaryPages.COUNT})", $"THE BROTHER'S DIARY  ({pages}/{DiaryPages.COUNT})"), ShopUI.Section);
            for (int i = 0; i < DiaryPages.COUNT; i++)
            {
                if (DiaryPages.Has(d, i)) GUILayout.Label("„" + DiaryPages.Text(i) + "“", ShopUI.Row);
                else GUILayout.Label(Loc.T("…stránka chybí…", "…a page is missing…"), ShopUI.Faint);
                GUILayout.Space(4f * s);
            }
        }

        // Nápověda — jen po kliknutí.
        if (showHint)
        {
            GUILayout.Space(8f * s);
            GUILayout.Label(Loc.T("NÁPOVĚDA", "HINT"), ShopUI.Section);
            GUILayout.Label(HintText(d), ShopUI.Row);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8f * s);
        GUILayout.BeginHorizontal();
        string hintBtn = showHint ? Loc.T("Skrýt nápovědu", "Hide hint") : Loc.T("Nápověda — co dělat teď?", "Hint — what now?");
        if (SoundManager.Click(GUILayout.Button(hintBtn, ShopUI.Button, GUILayout.Height(34f * s))))
        {
            showHint = !showHint;
            if (showHint) scroll.y = float.MaxValue; // posuň dolů, ať je nápověda hned vidět
        }

        // Dopis z trezoru jde znovu přečíst (hádanka na souřadnice dalšího ostrova).
        if (d.hasLetter && SoundManager.Click(GUILayout.Button(Loc.T("Přečíst dopis z trezoru", "Read the letter from the vault"),
                                                               ShopUI.Button, GUILayout.Height(34f * s))))
        {
            Close();
            LetterScreen.Show(0, d.letterX, d.letterY);
        }

        // Závěrečná obrazovka (statistiky, titulky, Těžké moře) jde po dohrání otevřít znovu.
        if (d.storyEpilogueSeen && SoundManager.Click(GUILayout.Button(Loc.T("Závěr a titulky", "Ending and credits"),
                                                                       ShopUI.Button, GUILayout.Height(34f * s))))
        {
            Close();
            PauseMenu pm = FindFirstObjectByType<PauseMenu>();
            if (pm != null && PauseMenu.IsPaused) pm.ContinueGame(); // zavři pauzu, ať se čas nevrátí pod obrazovkou
            EndScreen.Show();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4f * s);

        GUILayout.Label(Loc.T("[Esc] zavřít deník", "[Esc] close journal"), ShopUI.Subtitle);
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

    // ── Kapitoly ──────────────────────────────────────────────────────────
    // Číslo kapitoly odvozené z postupu příběhu (storyStep / megaIndex / storyDone).
    // Kapitola "na cestě" (storyStep 2) ostrov nejmenuje — hráč ho ještě neviděl.
    public static int ChapterNumber(GameData d)
    {
        if (d.storyDone) return 9;
        if (d.storyStep <= 0) return 1;
        if (d.storyStep == 1) return 2;
        int island = Mathf.Clamp(d.megaIndex, 0, 2);
        bool onIsland = d.IsOnCurrentMegaIsland; // jediné pravidlo, viz GameData
        return 3 + island * 2 + (onIsland ? 1 : 0); // 3–8: cesta / ostrov, třikrát
    }

    private static string ChapterTitle(GameData d)
    {
        int n = ChapterNumber(d);
        if (n >= 9) return ChapterName(n);
        return Loc.T($"Kapitola {n}: {ChapterName(n)}", $"Chapter {n}: {ChapterName(n)}");
    }

    /// <summary>Jméno kapitoly bez čísla (používá i ChapterCard).</summary>
    public static string ChapterName(int n)
    {
        switch (n)
        {
            case 1:  return Loc.T("Starý námořník",   "The Old Sailor");
            case 2:  return Loc.T("Zkouška",          "The Trial");
            case 3:  return Loc.T("Vyplutí",          "Setting Sail");
            case 4:  return Loc.T("Pirátský ostrov",  "Pirate Island");
            case 5:  return Loc.T("Hádanka z dopisu", "The Letter's Riddle");
            case 6:  return Loc.T("Hřbitov lodí",     "The Ship Graveyard");
            case 7:  return Loc.T("Po stopě bratra",  "On the Brother's Trail");
            case 8:  return Loc.T("Poslední ostrov",  "The Final Island");
            default: return Loc.T("Epilog",           "Epilogue");
        }
    }

    // Rada pro aktuální kapitolu (a u ostrovů pro aktuální úkol na nich — megaTask).
    private static string HintText(GameData d)
    {
        switch (ChapterNumber(d))
        {
            case 1:
                return Loc.T("Starý námořník sedí u startovního ostrova. Dojdi k němu pěšky a zmáčkni E. Mezitím si vydělávej: u hejna ryb drž mezerník a ryby prodej ve výkupně v majáku (E u dveří majáku). Za první peníze si kup malou loď.",
                             "The old sailor sits by the starting island. Walk up to him and press E. Meanwhile, earn money: hold Space at a school of fish and sell the fish at the trading post in the lighthouse (E at its door). Buy a small boat with your first coins.");
            case 2:
                return Loc.T("Děda chce 1000 mincí a historický poklad. Ten najdeš přes bedny na ostrovech: bedna dá mapu, dopluj na místo z mapy, vykopej poklad mezerníkem a vyplať ho ve výkupně — zhruba každý pátý je historický. Peníze nejrychleji vydělají úkoly z oranžového pultu a potopení piráti.",
                             "Grandpa wants 1,000 coins and a historic treasure. Find it through chests on islands: a chest gives you a map, sail to the marked spot, dig with Space and cash it in at the trading post — about one in five is historic. Jobs from the orange counter and sinking pirates earn money fastest.");
            case 3:
            case 7:
                return Loc.T("Pluj podle šipky na minimapě (souřadnice cíle máš i nahoře na obrazovce). Ostrov bude bráněný — předem si kup munici do děla a oprav loď.",
                             "Follow the arrow on the minimap (the target coordinates are also at the top of the screen). The island will be defended — buy cannon ammo and repair your boat beforehand.");
            case 4:
                if (d.megaTask == 0)
                    return Loc.T("Zbourej obranu ostrova: děla na věžích hradeb (zamiř kamerou nahoru a střílej levým tlačítkem myši), stráže na břehu a hlídkovou loď.",
                                 "Break the island's defenses: the cannons on the wall towers (aim the camera up and fire with the left mouse button), the guards on shore and the patrol ship.");
                if (d.megaTask == 1)
                    return Loc.T("Obrana padla. Najdi uvnitř hradeb trezor a otoč ozubená kola tak, aby do sebe zapadla a roztočila se všechna.",
                                 "The defenses have fallen. Find the vault inside the walls and turn the gears so they all mesh and spin together.");
                return Loc.T("Trezor je otevřený — přečti si, co je uvnitř (E u trezoru).",
                             "The vault is open — read what is inside (E at the vault).");
            case 5:
                return Loc.T("Souřadnice dalšího ostrova jsou schované v hádance v dopise (tlačítko níž). Spočítej X a Y, otevři mapu (M) a najeď myší na to místo — u kurzoru se ukážou souřadnice. Klikni tam a šipka na minimapě tě dovede.",
                             "The next island's coordinates are hidden in the riddle in the letter (button below). Work out X and Y, open the map (M) and move the mouse to that spot — the coordinates appear next to the cursor. Click there and the minimap arrow will lead you.");
            case 6:
                if (d.megaTask <= 1)
                    return Loc.T("V mělčině kolem ostrova trčí z vody trosky. Dopluj k nim a mezerníkem vykopej tři kusy roztržené mapy.",
                                 "Wrecks stick out of the shoals around the island. Sail up to them and dig out three pieces of the torn map with Space.");
                return Loc.T("Mapa je kompletní — najdi vrak s podpalubím a prohledej ho (E).",
                             "The map is complete — find the wreck with a hold and search it (E).");
            case 8:
                if (d.megaTask == 0)
                    return Loc.T("Příjezd k ostrovu hlídá velká loď. Potop ji.",
                                 "A large ship guards the approach to the island. Sink it.");
                return Loc.T("Někdo se vylodil na ostrov. Dojdi k němu pěšky a promluv si s ním (E).",
                             "Someone has come ashore on the island. Walk up to him and talk (E).");
            default:
                if (!d.storyEpilogueSeen)
                    return Loc.T("Vrať se za dědou na startovní ostrov — šipka na minimapě ukazuje cestu. Promluv si s ním (E).",
                                 "Return to Grandpa on the starting island — the minimap arrow shows the way. Talk to him (E).");
                return Loc.T("Příběh je u konce. Můžeš dál volně plout, plnit úkoly, lovit piráty a sbírat kořist. V závěrečné obrazovce (tlačítko níž) jde zapnout Těžké moře.",
                             "The story is over. You can keep sailing freely, take on jobs, hunt pirates and collect loot. Rough Seas can be switched on in the ending screen (button below).");
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
            "Pirátský ostrov: zlomil jsi jeho obranu — umlčel jsi děla na hradebních věžích, přemohl ozbrojenou posádku i hlídkovou loď.",
            "Pirate Island: you broke its defenses — silenced the cannons on the wall towers and overpowered the armed crew and the patrol ship."));
        if (Reached(d, 0, 2)) list.Add(Loc.T(
            "Pirátský ostrov: vyřešil jsi hádanku ze tří ozubených kol a otevřel trezor.",
            "Pirate Island: you solved the puzzle of the three gears and opened the vault."));
        if (Reached(d, 0, 3)) list.Add(Loc.T(
            "Pirátský ostrov: v trezoru čekal vzkaz. Přečetl jsi ho a vydal ses po stopě dál.",
            "Pirate Island: a message was waiting in the vault. You read it and followed the trail onward."));

        if (d.ambush1Done) list.Add(Loc.T(
            "Na cestě tě přepadl megalodon. Skolil jsi ho — jeho zub si vezeš jako trofej.",
            "A megalodon ambushed you on the way. You slew it — you carry its tooth as a trophy."));

        if (Reached(d, 1, 1)) list.Add(Loc.T(
            "Hřbitov lodí: dorazil jsi. V mělčině kolem trčí trosky a skrývají se v nich kousky roztržené mapy.",
            "Ship Graveyard: you have arrived. Wrecks stick out of the shoals around it, hiding pieces of a torn map."));
        if (Reached(d, 1, 2)) list.Add(Loc.T(
            "Hřbitov lodí: vykopal jsi z mělčiny všechny tři kusy roztržené mapy.",
            "Ship Graveyard: you dug all three pieces of the torn map out of the shoals."));
        if (Reached(d, 1, 3)) list.Add(Loc.T(
            "Hřbitov lodí: prohledal jsi podpalubí vraku a přečetl další vzkaz.",
            "Ship Graveyard: you searched the wreck's hold and read another message."));

        if (d.ambush2Done) list.Add(Loc.T(
            "Z mlhy za Hřbitovem lodí se vynořil Bludný Holanďan. Poslal jsi ho ke dnu.",
            "The Flying Dutchman loomed out of the mist beyond the Ship Graveyard. You sent it to the bottom."));

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
    // U AKTUÁLNÍHO ostrova se navíc musí hráč na ostrov opravdu dostat (storyStep 3) —
    // megaTask se u Hřbitova lodí nastaví na 1 už při postavení ostrova, ne při příjezdu.
    private static bool Reached(GameData d, int idx, int minTask)
        => d.megaIndex > idx
        || (d.megaIndex == idx && (d.IsOnCurrentMegaIsland || d.storyDone) && d.megaTask >= minTask);
}
