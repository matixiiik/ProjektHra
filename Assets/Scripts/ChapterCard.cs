using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ChapterCard.cs
//  Úvodní karta kapitoly (fáze B): když příběh přejde do nové kapitoly, na pár
//  vteřin se uprostřed obrazovky objeví pergamenový pruh "KAPITOLA 4 —
//  Pirátský ostrov" s jednou větou nálady, obrazovka lehce ztmavne a zazní akord.
//
//  Kapitolu nepočítá sama — bere ji z deníku (JournalScreen.ChapterNumber), takže
//  karta a deník vždycky sedí. Hlídá jen ZMĚNU čísla kapitoly: po načtení hry ani
//  po návratu z majáku se karta neukáže (první snímek si číslo jen zapamatuje).
//  Když je zrovna otevřený dopis, dialog nebo jiné okno, karta počká, až se zavře.
//
//  Vytváří ji GridManager.Awake (Ensure). Čistě vizuální, nic neukládá.
// ─────────────────────────────────────────────────────────────────────────────

public class ChapterCard : MonoBehaviour
{
    private const float FADE_IN  = 0.9f;
    private const float HOLD     = 3.6f;
    private const float FADE_OUT = 1.3f;

    private int   lastChapter = -1;  // naposledy viděná kapitola (-1 = ještě nic)
    private int   pending     = -1;  // kapitola, která čeká na ukázání (okno je otevřené)
    private int   showing     = -1;  // právě ukazovaná kapitola
    private float shownAt;           // Time.unscaledTime začátku ukázání

    private GUIStyle numberStyle, nameStyle, lineStyle;

    /// <summary>Vytvoří kartu, pokud ve scéně ještě není (volá GridManager.Awake).</summary>
    public static void Ensure()
    {
        if (FindFirstObjectByType<ChapterCard>() != null) return;
        new GameObject("ChapterCard").AddComponent<ChapterCard>();
    }

    void Update()
    {
        GameData d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null) return;

        int ch = JournalScreen.ChapterNumber(d);

        // V hlavním menu (výběr slotu) se jen synchronizuje — načtení jiného savu není "nová kapitola".
        if (lastChapter == -1 || MainMenuManager.IsVisible) { lastChapter = ch; return; }

        if (ch != lastChapter)
        {
            lastChapter = ch;
            pending     = ch;
        }

        // Ukaž, až nebude nic zakrývat (dopis, rozhovor, závěrečná obrazovka…).
        if (pending != -1 && !SomethingOpen())
        {
            showing = pending;
            pending = -1;
            shownAt = Time.unscaledTime;
            SoundManager.PlayChapterChord();
        }
    }

    private static bool SomethingOpen()
    {
        if (LetterScreen.IsOpen || EndScreen.IsOpen || GameConsole.IsOpen || JournalScreen.IsOpen) return true;
        if (StoryNpc.Instance != null && StoryNpc.Instance.IsTalking) return true;
        if (RivalNpc.Instance != null && (RivalNpc.Instance.IsTalkingWith(0) || RivalNpc.Instance.IsTalkingWith(1))) return true;
        return false;
    }

    void OnGUI()
    {
        if (showing == -1) return;

        float t = Time.unscaledTime - shownAt;
        if (t > FADE_IN + HOLD + FADE_OUT) { showing = -1; return; }

        // Průhlednost: náběh → plně → odeznění.
        float alpha = t < FADE_IN ? t / FADE_IN
                    : t < FADE_IN + HOLD ? 1f
                    : 1f - (t - FADE_IN - HOLD) / FADE_OUT;
        alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alpha));

        GUI.depth = -850; // nad HUD a pauzou, pod deníkem a dopisem
        HudSkin.UseUiFont();
        InitStyles();

        float s = HudSkin.GuiScale;
        numberStyle.fontSize = Mathf.RoundToInt(17f * s);
        nameStyle.fontSize   = Mathf.RoundToInt(40f * s);
        lineStyle.fontSize   = Mathf.RoundToInt(18f * s);

        // Lehké ztmavení celé obrazovky — "filmový" přechod.
        GUI.color = new Color(0f, 0f, 0f, 0.35f * alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Pergamenový pruh kousek nad středem (pod panely úkolů nahoře).
        float w = Mathf.Min(760f * s, Screen.width - 40f);
        float h = 150f * s;
        var band = new Rect((Screen.width - w) / 2f, Screen.height * 0.40f - h / 2f, w, h);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(band, HudSkin.Parchment(), ScaleMode.StretchToFill);

        string number = showing >= 9 ? Loc.T("EPILOG", "EPILOGUE")
                                     : Loc.T($"KAPITOLA {showing}", $"CHAPTER {showing}");
        string name   = showing >= 9 ? Loc.T("Cesta domů", "The Way Home") : JournalScreen.ChapterName(showing);

        GUI.Label(new Rect(band.x, band.y + 14f * s, band.width, 24f * s), number, numberStyle);
        GUI.Label(new Rect(band.x, band.y + 38f * s, band.width, 52f * s), name, nameStyle);
        GUI.Label(new Rect(band.x + 30f * s, band.y + 94f * s, band.width - 60f * s, 44f * s), Tagline(showing), lineStyle);
        GUI.color = Color.white;
    }

    // Jedna věta nálady pod názvem kapitoly.
    private static string Tagline(int chapter)
    {
        switch (chapter)
        {
            case 1:  return Loc.T("Každá cesta začíná u někoho, kdo už doplul.", "Every voyage begins with someone who has already returned.");
            case 2:  return Loc.T("Důvěra se na moři nekupuje. Musí se vydělat.", "At sea, trust isn't bought. It has to be earned.");
            case 3:  return Loc.T("Za obzorem čeká ostrov, o kterém děda celý život mlčel.", "Beyond the horizon lies an island Grandpa never spoke of.");
            case 4:  return Loc.T("Hradby, děla a posádka, která nečeká hosty.", "Walls, cannons and a crew that expects no guests.");
            case 5:  return Loc.T("„Bratře…“ Kdo jiný by takhle začal dopis?", "“Brother…” Who else would begin a letter like that?");
            case 6:  return Loc.T("Tady tehdy všechno skončilo. A možná i začalo.", "This is where it all ended back then. Or perhaps began.");
            case 7:  return Loc.T("Někdo čekal celý život. Ještě chvíli vydrží.", "Someone has waited a whole lifetime. He can wait a little longer.");
            case 8:  return Loc.T("Bouře neodpouští. On možná ano.", "The storm does not forgive. Perhaps he will.");
            default: return Loc.T("Moře si vzalo, co chtělo. Teď je čas vrátit se domů.", "The sea has taken what it wanted. Now it's time to go home.");
        }
    }

    private void InitStyles()
    {
        if (nameStyle != null) return;
        numberStyle = HudSkin.InkAllStates(new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, HudSkin.InkFaint);
        nameStyle   = HudSkin.InkAllStates(new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, HudSkin.InkTitle);
        lineStyle   = HudSkin.InkAllStates(new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = true }, HudSkin.Ink);
    }
}
