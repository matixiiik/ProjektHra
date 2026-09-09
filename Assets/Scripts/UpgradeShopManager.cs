using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  UpgradeShopManager.cs
//  Obchod s vylepšeními. Otevře se, když hráč (pěšky) stojí vedle budovy
//  UpgradeShop a zmáčkne E (P1) / Numpad1 (P2). Zavírá se Esc / Numpad Enter.
//
//  Nabízí: rychlost lodě, lepší prut, rychlejší těžbu a dva vzhledy lodě
//  (střední, velká). Každý hráč má vlastní mince a vlastní upgrady — proto
//  jsou tu "per-buyer" pomocné metody, které čtou/zapisují buď do polí P1,
//  nebo P2 podle toho, kdo obchod otevřel (buyerIndex).
//
//  Kreslí se přes IMGUI (OnGUI).
// ─────────────────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(100)]
public class UpgradeShopManager : MonoBehaviour
{
    // Základní ceny jsou v EconomyConfig; každý ostrov je násobí svým cenovým
    // levelem (GameSession.ShopPriceLevel) → někde levněji, jinde dráž.

    private GridManager gridManager;   // v SampleScene; ve scéně majáku je null

    // Zaokrouhlená cena pro ostrov, jehož obchod je zrovna otevřený.
    private int PriceOf(int baseCost) => EconomyConfig.Price(baseCost, GameSession.ShopPriceLevel);

    // Obchod může být otevřený zvlášť pro hráče 1 i hráče 2 naráz (jsou spolu
    // v jednom majáku). buyerIndex říká, čí panel se zrovna kreslí / kdo nakupuje.
    private readonly bool[] openFor = new bool[2];
    private int             buyerIndex;

    // Pozice posuvníku sortimentu pro každého hráče (kolečko + tažení myší).
    private readonly Vector2[] scroll = new Vector2[2];

    public bool IsOpen => openFor[0] || openFor[1];

    /// <summary>Je obchod otevřený zrovna pro TOHOHLE hráče? (obchod jednoho hráče nemá mrazit druhého)</summary>
    public bool IsOpenForBuyer(int playerIndex) => playerIndex >= 0 && playerIndex < 2 && openFor[playerIndex];

    // Data hry — vždy přes GameSession (funguje i ve scéně majáku bez GridManageru).
    private GameData Data => GameSession.Instance.Data;

    // Ulož a překresli (v SampleScene přes GridManager, jinak jen přes GameSession).
    private void Persist()
    {
        if (gridManager != null) { gridManager.Save(); gridManager.NotifyWorldChanged(); }
        else                     { GameSession.Instance.Save(); }
    }

    /// <summary>
    /// True, když je otevřený JAKÝKOLI obchod (upgrade i quest, kteréhokoli hráče).
    /// Pauza se podle toho pozná, že Esc má zavřít obchod, ne otevřít pauzu.
    /// </summary>
    public static bool AnyShopOpen
    {
        get
        {
            foreach (var u in FindObjectsByType<UpgradeShopManager>(FindObjectsSortMode.None))
                if (u.IsOpen) return true;
            foreach (var q in FindObjectsByType<QuestShopManager>(FindObjectsSortMode.None))
                if (q.IsOpen) return true;
            return false;
        }
    }

    private GUIStyle titleStyle, rowStyle, ownedStyle, buyStyle, coinsStyle;
    private bool     stylesReady;

    void Start() { gridManager = FindFirstObjectByType<GridManager>(); }

    /// <summary>Otevře obchod pro daného hráče.</summary>
    public void Open(int playerIndex = 0)
    {
        if (playerIndex < 0 || playerIndex > 1) playerIndex = 0;
        openFor[playerIndex] = true;
        buyerIndex = playerIndex;
    }

    void Update()
    {
        // Zavření obchodu — hráč 1 Escape, hráč 2 NumpadEnter (v sólu obojí zavře P1).
        bool p1Close = Input.GetKeyDown(KeyCode.Escape) || (!MultiplayerManager.IsMultiplayer && Input.GetKeyDown(KeyCode.KeypadEnter));
        bool p2Close = Input.GetKeyDown(KeyCode.KeypadEnter);
        if (openFor[0] && p1Close) openFor[0] = false;
        if (openFor[1] && p2Close) openFor[1] = false;
    }

    // ── Per-buyer přístup k datům (P1 vs P2) ─────────────────────────────────
    int  Coins()         => buyerIndex == 0 ? Data.coins : Data.player2Coins;
    void SetCoins(int v)  { if (buyerIndex == 0) Data.coins = v; else Data.player2Coins = v; }

    // upgradeType: 0 = speed, 1 = rod (prut), 2 = mining (těžba)
    bool GetUpgrade(int t)
        => t == 0 ? (buyerIndex == 0 ? Data.hasSpeedUpgrade  : Data.player2HasSpeedUpgrade)
         : t == 1 ? (buyerIndex == 0 ? Data.hasRodUpgrade    : Data.player2HasRodUpgrade)
         :          (buyerIndex == 0 ? Data.hasMiningUpgrade : Data.player2HasMiningUpgrade);

    void SetUpgrade(int t, bool v)
    {
        if (buyerIndex == 0)
        {
            if      (t == 0) Data.hasSpeedUpgrade  = v;
            else if (t == 1) Data.hasRodUpgrade    = v;
            else             Data.hasMiningUpgrade = v;
        }
        else
        {
            if      (t == 0) Data.player2HasSpeedUpgrade  = v;
            else if (t == 1) Data.player2HasRodUpgrade    = v;
            else             Data.player2HasMiningUpgrade = v;
        }
    }

    int  ShipLevel()         => buyerIndex == 0 ? Data.shipLevel : Data.player2ShipLevel;
    void SetShipLevel(int v)  { if (buyerIndex == 0) Data.shipLevel = v; else Data.player2ShipLevel = v; }

    int  Ammo()              => buyerIndex == 0 ? Data.ammo : Data.player2Ammo;
    void AddAmmo(int n)       { if (buyerIndex == 0) Data.ammo += n; else Data.player2Ammo += n; }

    bool HasMap()             => buyerIndex == 0 ? Data.hasMap : Data.player2HasMap;
    void GiveMap()            { if (buyerIndex == 0) Data.hasMap = true; else Data.player2HasMap = true; }

    int  BoatHp()            => buyerIndex == 0 ? Data.boatHealth : Data.player2BoatHealth;
    bool BoatWrecked()       => buyerIndex == 0 ? Data.boatWrecked : Data.player2BoatWrecked;
    void FixBoat()
    {
        if (buyerIndex == 0)
        {
            bool wasWrecked = Data.boatWrecked;
            Data.boatHealth = BoatStats.MaxHealth;
            Data.boatWrecked = false;
            if (wasWrecked) Data.boatNeedsRehome = true; // přemístit loď k molu, až se hráč vrátí
        }
        else
        {
            bool wasWrecked = Data.player2BoatWrecked;
            Data.player2BoatHealth = BoatStats.MaxHealth;
            Data.player2BoatWrecked = false;
            if (wasWrecked) Data.player2BoatNeedsRehome = true;
        }
    }

    // ── Nákup vylepšení ─────────────────────────────────────────────────────
    // Podmínky: ještě to nemá + má dost mincí.
    private bool TryBuyUpgrade(int upgradeType, int cost)
    {
        if (GetUpgrade(upgradeType) || Coins() < cost) return false;

        SetCoins(Coins() - cost);
        SetUpgrade(upgradeType, true);
        Persist();
        return true;
    }

    // ── GUI ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!IsOpen) return;
        InitStyles();

        // Kresli panel pro každého hráče, co má obchod otevřený (klidně oba naráz,
        // každý na své půlce). buyerIndex řídí, čí data pomocné metody čtou.
        for (int i = 0; i < 2; i++)
        {
            if (!openFor[i]) continue;
            buyerIndex = i;
            DrawShopPanel(i);
        }
    }

    private void DrawShopPanel(int who)
    {
        // V coopu kresli obchod jen na půlku obrazovky toho hráče (ať druhému nezakryje hru).
        float sx = 0f, sw = Screen.width;
        if (MultiplayerManager.IsMultiplayer)
        {
            sw = Screen.width * 0.5f;
            sx = who == 1 ? Screen.width * 0.5f : 0f;
        }

        // Tmavý overlay.
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(sx, 0, sw, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Panel se vejde na obrazovku i ve split screenu — jinak by spodek nešel
        // vidět. Zbytek sortimentu se doscrolluje kolečkem / tažením myši.
        float w = 560;
        float h = Mathf.Min(624f, Screen.height - 24f);
        float px = sx + (sw - w) / 2f;
        float py = (Screen.height - h) / 2f;

        // Panel + modrý proužek.
        GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Zavírací křížek vpravo nahoře.
        if (ShopUI.CloseButton(px, py, w)) { openFor[who] = false; return; }

        // Tažení myší = scroll (kolečko řeší ScrollView samo).
        ShopUI.HandleDragScroll(ref scroll[who]);

        GUILayout.BeginArea(new Rect(px + 25, py + 20, w - 50, h - 40));

        // V multiplayeru napiš do nadpisu, kdo nakupuje.
        string playerLabel = MultiplayerManager.IsMultiplayer
            ? (buyerIndex == 0 ? "  —  HRÁČ 1" : "  —  HRÁČ 2")
            : "";
        GUILayout.Label($"OBCHOD S VYLEPSENIMI{playerLabel}", titleStyle);

        // Cenová hladina tohoto ostrova (jen pro nákup).
        int pct = Mathf.RoundToInt((EconomyConfig.PriceMultiplier(GameSession.ShopPriceLevel) - 1f) * 100f);
        string priceTag = pct == 0 ? "ceny jako jinde" : pct > 0 ? $"ceny +{pct}%" : $"ceny {pct}%";
        GUILayout.Label($"( {priceTag} na tomhle ostrove )", rowStyle);
        GUILayout.Space(10);

        // Sortiment ve scrollovatelném okně (kolečko / tažení myší).
        scroll[who] = GUILayout.BeginScrollView(scroll[who], GUILayout.Height(h - 40f - 110f));

        DrawRow("Rychlost lodi  —  pohyb 2x rychleji",  PriceOf(EconomyConfig.SpeedUpgrade),  GetUpgrade(0), () => TryBuyUpgrade(0, PriceOf(EconomyConfig.SpeedUpgrade)));
        GUILayout.Space(8);
        DrawRow("Lepsi prud  —  chyta 2 ryby najednou",  PriceOf(EconomyConfig.RodUpgrade),   GetUpgrade(1), () => TryBuyUpgrade(1, PriceOf(EconomyConfig.RodUpgrade)));
        GUILayout.Space(8);
        DrawRow("Rychlost tezby  —  tezba 2x rychleji", PriceOf(EconomyConfig.MiningUpgrade), GetUpgrade(2), () => TryBuyUpgrade(2, PriceOf(EconomyConfig.MiningUpgrade)));
        GUILayout.Space(8);
        DrawShipRow("Lod mala  —  " + BoatStats.Perk(1),    PriceOf(EconomyConfig.ShipSmall),  1);
        GUILayout.Space(8);
        DrawShipRow("Lod stredni  —  " + BoatStats.Perk(2), PriceOf(EconomyConfig.ShipMedium), 2);
        GUILayout.Space(8);
        DrawShipRow("Lod velka  —  " + BoatStats.Perk(3),   PriceOf(EconomyConfig.ShipLarge),  3);
        GUILayout.Space(8);
        DrawAmmoRow();
        GUILayout.Space(8);
        DrawMapRow();
        GUILayout.Space(8);
        DrawRepairRow();

        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label($"Mince: {Coins()}", coinsStyle);
        GUILayout.EndArea();
    }

    // Oprava lodě — ukáže se, jen když je loď poškozená nebo rozbitá.
    private void DrawRepairRow()
    {
        bool wrecked = BoatWrecked();
        int  hp      = BoatHp();
        if (!wrecked && hp >= BoatStats.MaxHealth) return; // loď je v pohodě

        int cost = wrecked
            ? EconomyConfig.WreckRepairCost
            : Mathf.Max(1, (BoatStats.MaxHealth - hp) * EconomyConfig.RepairCostPerHp);

        string label = wrecked
            ? "Opravit ROZBITOU lod  —  vytahnout z vody"
            : $"Opravit lod  ( {hp}/100 )";

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label($"{cost} minci", rowStyle, GUILayout.Width(90));
        GUI.enabled = Coins() >= cost;
        if (SoundManager.Click(GUILayout.Button("Opravit", buyStyle, GUILayout.Width(90), GUILayout.Height(28))))
        {
            if (Coins() >= cost)
            {
                SetCoins(Coins() - cost);
                FixBoat();
                Persist();
            }
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    // Munice do děla — dá se kupovat opakovaně (žádné "Zakoupeno").
    private void DrawAmmoRow()
    {
        int cost = PriceOf(EconomyConfig.AmmoPack);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Munice do dela  —  balicek {EconomyConfig.AmmoPackSize} naboju  (mas {Ammo()})",
            rowStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label($"{cost} minci", rowStyle, GUILayout.Width(90));

        GUI.enabled = Coins() >= cost;
        if (SoundManager.Click(GUILayout.Button("Koupit", buyStyle, GUILayout.Width(90), GUILayout.Height(28))))
        {
            if (Coins() >= cost)
            {
                SetCoins(Coins() - cost);
                AddAmmo(EconomyConfig.AmmoPackSize);
                Persist();
            }
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    // Mapa — jednorázový nákup. Pak v lodi klávesa M otevře velkou mapu, kde si
    // klikneš cíl a na minimapě tě k němu vede šipka.
    private void DrawMapRow()
    {
        DrawRow("Mapa  —  v lodi klavesa M: velka mapa + cil (waypoint)",
            PriceOf(EconomyConfig.MapItem), HasMap(), () =>
            {
                int cost = PriceOf(EconomyConfig.MapItem);
                if (HasMap() || Coins() < cost) return;
                SetCoins(Coins() - cost);
                GiveMap();
                Persist();
            });
    }

    // Řádek nákupu vzhledu lodě. Koupit jde jen když má hráč přesně předchozí úroveň.
    private void DrawShipRow(string label, int cost, int requiredLevel)
    {
        int  cur    = ShipLevel();
        bool owned  = cur >= requiredLevel;
        bool canBuy = cur == requiredLevel - 1;

        DrawRow(label, cost, owned, () =>
        {
            if (!canBuy || Coins() < cost) return;

            SetCoins(Coins() - cost);
            SetShipLevel(requiredLevel);
            Persist();

            // Přepni 3D model lodě u toho správného hráče.
            var switchers = FindObjectsByType<ShipModelSwitcher>(FindObjectsSortMode.None);
            foreach (var s in switchers)
            {
                var pc = s.GetComponent<PlayerController>() ?? s.GetComponentInParent<PlayerController>();
                if (pc != null && pc.playerIndex == buyerIndex) { s.Apply(); break; }
            }
        });
    }

    // Univerzální řádek: popis + buď "Zakoupeno", nebo cena a tlačítko Koupit.
    private void DrawRow(string label, int cost, bool owned, System.Action onBuy)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.ExpandWidth(true));

        if (owned)
        {
            GUILayout.Label("Zakoupeno", ownedStyle, GUILayout.Width(120));
        }
        else
        {
            GUILayout.Label($"{cost} minci", rowStyle, GUILayout.Width(90));
            GUI.enabled = Coins() >= cost; // tlačítko jde zmáčknout jen s dost mincemi
            if (SoundManager.Click(GUILayout.Button("Koupit", buyStyle, GUILayout.Width(90), GUILayout.Height(28))))
                onBuy();
            GUI.enabled = true;
        }

        GUILayout.EndHorizontal();
    }

    // ── Styly (jen jednou) ──────────────────────────────────────────────────
    private void InitStyles()
    {
        if (stylesReady) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        ownedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 1f, 0.4f) }
        };
        buyStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.2f, 0.5f,  0.2f)) },
            hover  = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.65f, 0.3f)) }
        };
        coinsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }
        };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
}
