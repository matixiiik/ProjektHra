using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LetterScreen.cs
//  Dopis z trezoru na Pirátském ostrově — otevře se přes celou obrazovku (ve split
//  screenu přes půlku toho, kdo ho čte) jako pergamen s textem od bratra. Dopis
//  obsahuje HÁDANKU: souřadnice dalšího ostrova nejsou napsané rovnou, hráč si
//  X a Y musí spočítat a najít je na velké mapě (proto ukazuje souřadnice pod
//  kurzorem — viz MapScreen).
//
//  Hádanka se generuje ze SKUTEČNÝCH souřadnic ostrova, deterministicky (stejné
//  souřadnice = pokaždé stejná čísla), takže po načtení hry i po přepnutí jazyka
//  zůstane stejná. Dopis jde zavřít Esc (P1) / Numpad Enter (P2) — Esc nespustí
//  pauzu (PauseMenu si tenhle stav hlídá), a znovu se dá přečíst z Deníku.
//
//  Kreslí se přes IMGUI (OnGUI), pergamen je textura vygenerovaná v kódu
//  (žádný import assetu).
// ─────────────────────────────────────────────────────────────────────────────

public class LetterScreen : MonoBehaviour
{
    private static LetterScreen instance;

    /// <summary>Čte někdo právě dopis?</summary>
    public static bool IsOpen => instance != null && instance.openFor != -1;

    /// <summary>Čte dopis tenhle hráč? (mrazí jeho ovládání i zásahy — viz PlayerController.ModalOpen)</summary>
    public static bool IsOpenFor(int playerIndex) => instance != null && instance.openFor == playerIndex;

    private int     openFor = -1;    // -1 = zavřeno, jinak index čtenáře (0/1)
    private int     islandX, islandY; // souřadnice, ze kterých se hádanka skládá
    private Vector2 scroll;

    private Texture2D paper;
    private GUIStyle  titleStyle, bodyStyle, signStyle, hintStyle;

    /// <summary>Otevře dopis pro hráče. Souřadnice cíle se v textu schovají do hádanky.</summary>
    public static void Show(int playerIndex, int targetX, int targetY)
    {
        if (instance == null)
            instance = new GameObject("LetterScreen").AddComponent<LetterScreen>();

        instance.openFor = playerIndex;
        instance.islandX = targetX;
        instance.islandY = targetY;
        instance.scroll  = Vector2.zero;
    }

    void OnGUI()
    {
        if (openFor == -1) return;

        GUI.depth = -1000; // nad pauzou, deníkem i HUD
        HudSkin.UseUiFont();
        InitStyles();

        // Kde se kreslí: sólo = celá obrazovka, split screen = půlka čtenáře.
        Rect half = new Rect(0f, 0f, Screen.width, Screen.height);
        if (MultiplayerManager.IsMultiplayer)
        {
            float hw = Screen.width * 0.5f;
            half = new Rect(openFor == 1 ? hw : 0f, 0f, hw, Screen.height);
        }

        // Ztmavení pozadí + pergamen přes (skoro) celou plochu.
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(half, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float s = HudSkin.GuiScale;
        float w = Mathf.Min(900f * s, half.width  - 60f);
        float h = Mathf.Min(760f * s, half.height - 50f);
        var sheet = new Rect(half.x + (half.width - w) / 2f, half.y + (half.height - h) / 2f, w, h);

        GUI.DrawTexture(sheet, GetPaper(), ScaleMode.StretchToFill);

        // Velikosti písma podle měřítka.
        titleStyle.fontSize = Mathf.RoundToInt(34f * s);
        bodyStyle.fontSize  = Mathf.RoundToInt(21f * s);
        signStyle.fontSize  = Mathf.RoundToInt(24f * s);
        hintStyle.fontSize  = Mathf.RoundToInt(15f * s);

        float pad = 46f * s;
        var inner = new Rect(sheet.x + pad, sheet.y + 28f * s, sheet.width - 2 * pad, sheet.height - 56f * s);

        GUILayout.BeginArea(inner);
        GUILayout.Label(Loc.T("List", "Letter"), titleStyle);
        GUILayout.Space(10f * s);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(inner.height - 90f * s));
        GUILayout.Label(Loc.T(BodyCs(islandX, islandY), BodyEn(islandX, islandY)), bodyStyle);
        GUILayout.Space(14f * s);
        GUILayout.Label(Loc.T("— tvůj bratr", "— your brother"), signStyle);
        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        string closeKey = openFor == 0 ? "Esc" : "Numpad Enter";
        GUILayout.Label(Loc.T($"[{closeKey}] zavřít", $"[{closeKey}] close"), hintStyle);
        GUILayout.EndArea();

        // Zavření: Esc (P1) / Numpad Enter (P2) — event spotřebuj, ať nevyskočí pauza.
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown &&
            ((openFor == 0 && e.keyCode == KeyCode.Escape) ||
             (openFor == 1 && e.keyCode == KeyCode.KeypadEnter)))
        {
            openFor = -1;
            e.Use();
        }
    }

    // ── Text dopisu ──────────────────────────────────────────────────────────
    // Čísla hádanky vycházejí ze souřadnic cíle:
    //   |X| = a·b + c        (kotvy × sáhy řetězu + lano navrch)
    //   |Y| = a2·b2 − c2     (bedny × lahve rumu − vypité lahve)
    // Směr (východ/západ, sever/jih) je v textu napsaný slovy, ať čísla zůstanou kladná.
    // Počátek [0, 0] je "domov" = startovní ostrov.
    private static void RiddleNumbers(int x, int y, out int a, out int b, out int c, out int a2, out int b2, out int c2)
    {
        int h = unchecked(x * 73856093 ^ y * 19349663) & 0x7fffffff;

        int ax = Mathf.Abs(x);
        a = 4 + h % 6;               // 4..9 kotev
        b = ax / a;
        c = ax - a * b;              // 0..a-1

        int ay = Mathf.Abs(y);
        a2 = 5 + (h / 7) % 5;        // 5..9 beden
        b2 = (ay + a2 - 1) / a2;     // zaokrouhleno nahoru
        c2 = a2 * b2 - ay;           // 0..a2-1
    }

    // Česká čísla: 1 = tvar1, 2–4 = tvar2, jinak tvar5.
    private static string Cz(int n, string one, string few, string many)
        => n == 1 ? one : (n >= 2 && n <= 4 ? few : many);

    private static string BodyCs(int x, int y)
    {
        RiddleNumbers(x, y, out int a, out int b, out int c, out int a2, out int b2, out int c2);
        string dirX = x >= 0 ? "východ" : "západ";
        string dirY = y >= 0 ? "sever" : "jih";

        string riddleX = $"Na {dirX}: {a} kotev, u každé {b} {Cz(b, "sáh", "sáhy", "sáhů")} řetězu"
                       + (c == 0 ? ", víc nic." : $", a k tomu ještě {c} {Cz(c, "sáh", "sáhy", "sáhů")} lana navrch.")
                       + $" Tolik políček je to na {dirX} od domova.";

        string riddleY = $"Na {dirY}: {a2} beden a v každé {b2} {Cz(b2, "lahev", "lahve", "lahví")} rumu. "
                       + (c2 == 0 ? "Posádka cestou nevypila ani lahev."
                                  : $"Posádka jich cestou vypila {c2}.")
                       + $" Kolik lahví zbylo, tolik políček je to na {dirY} od domova.";

        return
            "Bratře,\n\n"
          + "jestli držíš v ruce tenhle papír, dostal ses přes hradby, přes děla i přes mou starou posádku. "
          + "To jsem od tebe — nebo od toho, koho jsi za mnou poslal — nečekal. Ano, byl jsem tady první. "
          + "Trezor jsem otevřel dřív než ty a odvezl jsem si všechno, co stálo za odvezení. "
          + "Těch pár mincí, co tu zbylo, si nech na cestu.\n\n"
          + "Pamatuješ, jak nám děda říkal, že pravý poklad se nikdy nevozí v truhle? Měl pravdu. "
          + "To nejdůležitější jsem ukryl jinam, na místo, kde tě nikdo nebude hledat, a zadarmo ti ho nedám. "
          + "Jestli jsi pořád tak chytrý, jak si myslíš, dokážeš si to spočítat. "
          + "Vzdálenosti měř od našeho starého přístavu — od místa, odkud jsme spolu poprvé vyplouvali. "
          + "Jedno políčko na mapě je jeden sáh.\n\n"
          + "I.   " + riddleX + "\n\n"
          + "II.  " + riddleY + "\n\n"
          + "Obě čísla si zapiš a najdi to místo na velké mapě — když s myší přejedeš po moři, ukáže ti, "
          + "kde přesně jsi. Cesta tam nebude snadná. Voda kolem toho místa už není tak klidná, jak si pamatuješ, "
          + "a něco velkého se tam v poslední době pohybuje. Dávej pozor.\n\n"
          + "Nečekej, že tě přivítám. Sejdeme se na tom ostrově až tehdy, kdy budu chtít já.";
    }

    private static string BodyEn(int x, int y)
    {
        RiddleNumbers(x, y, out int a, out int b, out int c, out int a2, out int b2, out int c2);
        string dirX = x >= 0 ? "east" : "west";
        string dirY = y >= 0 ? "north" : "south";

        string riddleX = $"To the {dirX}: {a} anchors, each on {b} {(b == 1 ? "fathom" : "fathoms")} of chain"
                       + (c == 0 ? ", nothing more." : $", plus {c} more {(c == 1 ? "fathom" : "fathoms")} of rope on top.")
                       + $" That is how many tiles it lies to the {dirX} of home.";

        string riddleY = $"To the {dirY}: {a2} crates, {b2} {(b2 == 1 ? "bottle" : "bottles")} of rum in each. "
                       + (c2 == 0 ? "The crew did not drink a single bottle on the way."
                                  : $"The crew drank {c2} of them on the way.")
                       + $" However many bottles are left, that is how many tiles it lies to the {dirY} of home.";

        return
            "Brother,\n\n"
          + "if you are holding this paper, you got past the walls, past the cannons and past my old crew. "
          + "I did not expect that from you — or from whoever you sent after me. Yes, I was here first. "
          + "I opened the vault before you did and took everything worth taking. "
          + "Keep the few coins left behind for the road.\n\n"
          + "Remember how Grandfather used to say that a true treasure is never carried in a chest? He was right. "
          + "I hid the most important thing elsewhere, in a place where nobody will look for you, and I won’t hand it over for free. "
          + "If you are still as clever as you think you are, you will work it out. "
          + "Measure the distances from our old harbor — from the place we first set sail together. "
          + "One tile on the map is one fathom.\n\n"
          + "I.   " + riddleX + "\n\n"
          + "II.  " + riddleY + "\n\n"
          + "Write both numbers down and find the place on the large map — when you move the mouse over the sea, "
          + "it shows you exactly where you are pointing. The way there will not be easy. The water around that place "
          + "is no longer as calm as you remember, and something large has been moving there lately. Be careful.\n\n"
          + "Do not expect me to welcome you. We will meet on that island when I decide it is time.";
    }

    // ── Vzhled ───────────────────────────────────────────────────────────────
    // Pergamen: krémový podklad, jemný šum a ztmavené okraje (vytvoří se jednou).
    private Texture2D GetPaper()
    {
        if (paper != null) return paper;

        const int S = 256;
        paper = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color light = new Color(0.94f, 0.86f, 0.66f);
        Color edge  = new Color(0.60f, 0.44f, 0.24f);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float u = x / (float)(S - 1), v = y / (float)(S - 1);

            // Vzdálenost od okraje (0 = kraj, 1 = střed) → ztmavení okrajů.
            float dEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;
            float vignette = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dEdge / 0.35f));

            // Skvrny a vlákna papíru (Perlin).
            float blotch = Mathf.PerlinNoise(u * 3.1f + 7.3f, v * 3.1f + 1.9f) - 0.5f;
            float grain  = Mathf.PerlinNoise(u * 40f, v * 40f) - 0.5f;

            Color c = Color.Lerp(light, edge, vignette * 0.85f);
            c = new Color(c.r + blotch * 0.10f + grain * 0.04f,
                          c.g + blotch * 0.09f + grain * 0.04f,
                          c.b + blotch * 0.07f + grain * 0.03f, 1f);
            paper.SetPixel(x, y, c);
        }
        paper.Apply();
        return paper;
    }

    private void InitStyles()
    {
        if (titleStyle != null) return;

        Color ink = new Color(0.22f, 0.14f, 0.08f);

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.36f, 0.20f, 0.10f) }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true, alignment = TextAnchor.UpperLeft,
            normal = { textColor = ink }
        };
        signStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleRight,
            normal = { textColor = ink }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.40f, 0.28f, 0.16f) }
        };
    }
}
