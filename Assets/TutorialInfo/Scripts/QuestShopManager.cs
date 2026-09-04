using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  QuestShopManager.cs
//  Obchod s questy + výkupna. Otevře se u budovy QuestShop klávesou E / Numpad1.
//
//  Dvě části:
//   1) PRODEJ  — hráč tu prodá nalovené ryby a vytěžené poklady za mince.
//   2) QUESTY  — hráč si koupí úkol ("Ulov 10 ryb"). Za jeho splnění dostane
//                zpět víc, než zaplatil (cost * multiplier). Naráz jen 1 quest.
//
//  Stejně jako UpgradeShop má "per-buyer" pomocné metody pro P1 / P2.
// ─────────────────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(100)]
public class QuestShopManager : MonoBehaviour
{
    public int fishSellPrice     = 10; // cena za 1 rybu
    public int treasureSellPrice = 30; // cena za 1 poklad

    private GridManager gridManager;
    private bool        isOpen;
    private int         buyerIndex; // 0 = P1, 1 = P2

    public bool IsOpen => isOpen;

    // Data hry — vždy přes GameSession (funguje i ve scéně majáku bez GridManageru).
    private GameData Data => GameSession.Instance.Data;

    void Update()
    {
        // Zavření obchodu. Pozn.: sdílený příznak je v UpgradeShopManager.AnyShopOpen.
        if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            isOpen = false;
            UpgradeShopManager.AnyShopOpen = false;
        }
    }

    // Tři questy, ze kterých si hráč vybírá (vygenerují se při otevření obchodu).
    private OfferedQuest[] offeredQuests;

    private GUIStyle titleStyle, sectionStyle, rowStyle;
    private GUIStyle buyStyle, coinsStyle, claimStyle, progressStyle;
    private bool     stylesReady;

    // Jedna nabídka questu (jen dočasná, neukládá se).
    private struct OfferedQuest
    {
        public int    type;       // 0 = ryby, 1 = poklady
        public string desc;
        public int    target, cost, multiplier;
    }

    // Šablony questů: (typ, min cíl, max cíl, min cena, max cena, násobič odměny).
    // Z každé se náhodně "vylosuje" konkrétní cíl a cena v daném rozsahu.
    private static readonly (int type, int tMin, int tMax, int cMin, int cMax, int mult)[] Templates =
    {
        (0,  5, 10,  20,  40, 2),
        (0, 20, 35,  60,  90, 4),
        (0, 50, 80, 100, 150, 7),
        (1,  3,  6,  30,  50, 3),
        (1,  8, 15,  70, 100, 5),
        (1, 18, 25, 120, 180, 8),
    };

    void Start() { gridManager = FindFirstObjectByType<GridManager>(); }

    /// <summary>Otevře obchod pro daného hráče a případně vygeneruje nabídku questů.</summary>
    public void Open(int playerIndex = 0)
    {
        buyerIndex = playerIndex;
        isOpen     = true;
        UpgradeShopManager.AnyShopOpen = true;

        // Nabídku generuj jen když hráč zrovna žádný quest nemá.
        if (!GetQuest().hasQuest) GenerateOffers();
    }

    // ── Per-buyer přístup k datům (P1 vs P2) ─────────────────────────────────
    int         GetCoins()         => buyerIndex == 0 ? Data.coins         : Data.player2Coins;
    void        SetCoins(int v)    { if (buyerIndex == 0) Data.coins = v;         else Data.player2Coins = v; }
    int         GetFish()          => buyerIndex == 0 ? Data.fishCount     : Data.player2FishCount;
    void        SetFish(int v)     { if (buyerIndex == 0) Data.fishCount = v;     else Data.player2FishCount = v; }
    int         GetTreasure()      => buyerIndex == 0 ? Data.treasureCount : Data.player2TreasureCount;
    void        SetTreasure(int v) { if (buyerIndex == 0) Data.treasureCount = v; else Data.player2TreasureCount = v; }
    ActiveQuest GetQuest()         => buyerIndex == 0 ? Data.activeQuest    : Data.player2ActiveQuest;

    // ── Generování nabídky questů ───────────────────────────────────────────
    private void GenerateOffers()
    {
        offeredQuests = new OfferedQuest[3];
        int[] picks = PickDistinct(3, Templates.Length); // 3 různé šablony

        for (int i = 0; i < 3; i++)
        {
            var t      = Templates[picks[i]];
            int target = Random.Range(t.tMin, t.tMax + 1);
            int cost   = Random.Range(t.cMin, t.cMax + 1);
            offeredQuests[i] = new OfferedQuest
            {
                type       = t.type,
                desc       = t.type == 0 ? $"Ulov {target} ryb" : $"Vytez {target} pokladu",
                target     = target,
                cost       = cost,
                multiplier = t.mult
            };
        }
    }

    // Vrátí "count" různých čísel z rozsahu 0..max-1 (bez opakování).
    private int[] PickDistinct(int count, int max)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int pick; bool unique;
            do
            {
                pick   = Random.Range(0, max);
                unique = true;
                for (int j = 0; j < i; j++)
                    if (result[j] == pick) { unique = false; break; } // už jsme vylosovali → losuj znovu
            } while (!unique);
            result[i] = pick;
        }
        return result;
    }

    // Koupě questu — zaplatí cenu a nastaví aktivní quest.
    private void BuyQuest(OfferedQuest q)
    {
        if (GetCoins() < q.cost) return;
        SetCoins(GetCoins() - q.cost);

        ActiveQuest aq = GetQuest();
        aq.hasQuest    = true;
        aq.questType   = q.type;
        aq.description = q.desc;
        aq.target      = q.target;
        aq.progress    = 0;
        aq.cost        = q.cost;
        aq.reward      = q.cost * q.multiplier; // kolik hráč dostane za splnění
        aq.multiplier  = q.multiplier;
        Save();
    }

    /// <summary>Vyzvednutí odměny za splněný quest.</summary>
    public void ClaimQuest()
    {
        ActiveQuest aq = GetQuest();
        if (!aq.hasQuest || !aq.IsComplete) return;

        SetCoins(GetCoins() + aq.reward);
        aq.Reset(); // hráč si teď může koupit další
        Save();
    }

    // ── GUI ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        // Tmavý overlay.
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 580, h = 520;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        // Panel + oranžový proužek.
        GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.6f, 0.1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 25, py + 20, w - 50, h - 40));

        string playerLabel = MultiplayerManager.IsMultiplayer
            ? (buyerIndex == 0 ? "  —  HRÁČ 1" : "  —  HRÁČ 2")
            : "";
        GUILayout.Label($"OBCHOD S QUESTY{playerLabel}", titleStyle);
        GUILayout.Space(12);

        // ── PRODEJ ──────────────────────────────────────────────────────────
        GUILayout.Label("PRODEJ", sectionStyle);

        int fish     = GetFish();
        int treasure = GetTreasure();

        DrawSell($"Ryby  x{fish}  ( {fishSellPrice} minci / kus )",
            fish * fishSellPrice, fish > 0,
            () => { SetCoins(GetCoins() + fish * fishSellPrice); SetFish(0); Save(); });
        GUILayout.Space(4);
        DrawSell($"Poklady  x{treasure}  ( {treasureSellPrice} minci / kus )",
            treasure * treasureSellPrice, treasure > 0,
            () => { SetCoins(GetCoins() + treasure * treasureSellPrice); SetTreasure(0); Save(); });

        GUILayout.Space(14);

        // ── QUESTY ──────────────────────────────────────────────────────────
        GUILayout.Label("QUESTY", sectionStyle);
        ActiveQuest aq = GetQuest();

        if (aq.hasQuest)
        {
            // Hráč už quest má → ukaž postup a případně tlačítko na vyzvednutí.
            GUILayout.Label($"Aktivni:  {aq.description}", rowStyle);
            GUILayout.Label($"Postup:   {aq.progress} / {aq.target}", progressStyle);
            GUILayout.Label($"Odmena:   {aq.reward} minci  ( {aq.multiplier}x )", rowStyle);
            GUILayout.Space(6);

            if (aq.IsComplete)
            {
                if (GUILayout.Button($"  VYPLATIT  {aq.reward} minci  !", claimStyle, GUILayout.Height(38)))
                    ClaimQuest();
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label("( quest jeste neni splnen )", rowStyle);
                GUI.color = Color.white;
            }
        }
        else
        {
            // Hráč quest nemá → nabídni tři na výběr.
            GUILayout.Label("Zadny aktivni quest — vyber si:", rowStyle);
            GUILayout.Space(6);

            if (offeredQuests != null)
            {
                for (int i = 0; i < offeredQuests.Length; i++)
                {
                    var q = offeredQuests[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{q.desc}    odmena: {q.cost * q.multiplier} minci  ( {q.multiplier}x )", rowStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{q.cost} minci", rowStyle, GUILayout.Width(90));
                    GUI.enabled = GetCoins() >= q.cost;
                    if (GUILayout.Button("Koupit", buyStyle, GUILayout.Width(80), GUILayout.Height(26)))
                    {
                        BuyQuest(q);
                        break; // seznam se hned změní → ukonči smyčku
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);
                }
            }
        }

        GUILayout.Space(12);
        GUILayout.Label($"Mince: {GetCoins()}", coinsStyle);
        GUILayout.EndArea();
    }

    // Řádek prodeje: popis + celková částka + tlačítko "Prodat vse".
    private void DrawSell(string label, int total, bool enabled, System.Action onSell)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label($"= {total} minci", rowStyle, GUILayout.Width(110));
        GUI.enabled = enabled; // nejde prodat, když hráč nic nemá
        if (GUILayout.Button("Prodat vse", buyStyle, GUILayout.Width(110), GUILayout.Height(26)))
            onSell();
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    // Ulož hru a dej vědět HUD/minimapě (ve scéně majáku bez GridManageru).
    private void Save()
    {
        if (gridManager != null) { gridManager.Save(); gridManager.NotifyWorldChanged(); }
        else                     { GameSession.Instance.Save(); }
    }

    // ── Styly (jen jednou) ──────────────────────────────────────────────────
    private void InitStyles()
    {
        if (stylesReady) return;

        titleStyle    = new GUIStyle(GUI.skin.label)  { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        sectionStyle  = new GUIStyle(GUI.skin.label)  { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 0.85f, 1f) } };
        rowStyle      = new GUIStyle(GUI.skin.label)  { fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
        progressStyle = new GUIStyle(GUI.skin.label)  { fontSize = 15, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
        buyStyle      = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white, background = MakeTex(new Color(0.2f, 0.45f, 0.2f)) }, hover = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.6f, 0.3f)) } };
        claimStyle    = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.5f, 0.05f)) }, hover = { textColor = Color.white, background = MakeTex(new Color(0.9f, 0.65f, 0.1f)) } };
        coinsStyle    = new GUIStyle(GUI.skin.label)  { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
}
