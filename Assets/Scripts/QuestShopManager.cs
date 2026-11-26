using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  QuestShopManager.cs
//  Řídí DVA pulty v majáku (obojí kreslí tenhle jeden skript):
//   • VÝKUPNA (zelený pult)  — prodej ryb, pokladů, pirátské kořisti a
//                              historického pokladu + vyplacení mega questu.
//   • OBCHOD S QUESTY (oranžový pult) — koupě úkolu; za splnění hráč dostane
//                              zpět víc, než zaplatil, a u větších úkolů i věc
//                              (vylepšení, munici, kořist). Naráz 1 quest.
//  Který se zobrazí, řídí sellMode (nastaví InteriorInteractable podle pultu).
//
//  Typy questů (ActiveQuest.TYPE_…): ryby, poklady, potopit piráty, zničit děla,
//  otevřít bedny, získat kořist. Bojové úkoly se nabízí, jen když má hráč loď
//  s dělem — jinak by je nešlo splnit.
//
//  Stejně jako UpgradeShop má "per-buyer" pomocné metody pro P1 / P2.
//  Vzhled (pergamen, styly) je sdílený v ShopUI.
// ─────────────────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(100)]
public class QuestShopManager : MonoBehaviour
{
    // Statický seznam aktivních obchodů — nahrazuje FindObjectsByType ve
    // UpgradeShopManager.AnyShopOpen (to se ptá každý snímek, viz tamní komentář).
    private static readonly List<QuestShopManager> activeInstances = new List<QuestShopManager>();
    public static IReadOnlyList<QuestShopManager> All => activeInstances;

    void OnEnable()  { activeInstances.Add(this); }
    void OnDisable() { activeInstances.Remove(this); }

    // Výkupní ceny jsou v EconomyConfig (všechny ostrovy stejně) a u kořisti v LootItems.

    private GridManager gridManager;

    // Obchod může být otevřený zvlášť pro hráče 1 i 2 naráz (jsou spolu v majáku).
    private readonly bool[] openFor = new bool[2];
    private int             buyerIndex;

    // Pozice posuvníku sortimentu pro každého hráče (kolečko + tažení myší).
    private readonly Vector2[] scroll = new Vector2[2];

    // true = hráč otevřel VÝKUPNU (zelený pult, prodej), false = obchod s QUESTY
    // (oranžový pult). Nastaví InteriorInteractable podle pultu.
    private readonly bool[] sellMode = new bool[2];

    // Poslední hláška obchodu pro každého hráče ("Získal jsi: Lepší prut").
    private readonly string[] lastMessage = new string[2];

    public bool IsOpen => openFor[0] || openFor[1];

    /// <summary>Je obchod otevřený zrovna pro TOHOHLE hráče?</summary>
    public bool IsOpenForBuyer(int playerIndex) => playerIndex >= 0 && playerIndex < 2 && openFor[playerIndex];

    // Data hry — vždy přes GameSession (funguje i ve scéně majáku bez GridManageru).
    private GameData Data => GameSession.Instance.Data;

    void Update()
    {
        // Zavření obchodu — hráč 1 Escape, hráč 2 NumpadEnter (v sólu obojí zavře P1).
        bool p1Close = Input.GetKeyDown(KeyCode.Escape) || (!MultiplayerManager.IsMultiplayer && Input.GetKeyDown(KeyCode.KeypadEnter));
        bool p2Close = Input.GetKeyDown(KeyCode.KeypadEnter);
        if (openFor[0] && p1Close) openFor[0] = false;
        if (openFor[1] && p2Close) openFor[1] = false;
    }

    // Tři questy, ze kterých si hráč vybírá (vygenerují se při otevření obchodu).
    private OfferedQuest[] offeredQuests;

    // Jedna nabídka questu (jen dočasná, neukládá se).
    private struct OfferedQuest
    {
        public int    type;       // ActiveQuest.TYPE_…
        public string desc;
        public int    target, cost, multiplier;
        public int    rewardItem; // ActiveQuest.REWARD_…
    }

    // Šablona questu — z každé se náhodně "vylosuje" konkrétní cíl a cena v rozsahu.
    private struct QuestTemplate
    {
        public int  type, tMin, tMax, cMin, cMax, mult, rewardItem;
        public bool needsCannon; // jen když má hráč loď s dělem (boj na moři)

        public QuestTemplate(int type, int tMin, int tMax, int cMin, int cMax, int mult, int rewardItem, bool needsCannon)
        {
            this.type = type; this.tMin = tMin; this.tMax = tMax; this.cMin = cMin; this.cMax = cMax;
            this.mult = mult; this.rewardItem = rewardItem; this.needsCannon = needsCannon;
        }
    }

    // Přizpůsobené nízkému výkupu (1 ryba = 2 mince): odměna questu (cost*mult)
    // je hlavní zdroj větších peněz. Velké a bojové questy dají navíc věc —
    // tím se vyplatí dělat i "vedlejší" úkoly, nejen sbírat ryby.
    private static readonly QuestTemplate[] Templates =
    {
        //                type                        cíl      cena   ×   věcná odměna                    dělo?
        new QuestTemplate(ActiveQuest.TYPE_FISH,      6, 12,  15,  30, 3, ActiveQuest.REWARD_NONE,      false),
        new QuestTemplate(ActiveQuest.TYPE_FISH,     25, 40,  50,  80, 5, ActiveQuest.REWARD_NONE,      false),
        new QuestTemplate(ActiveQuest.TYPE_FISH,     60, 90, 110, 160, 8, ActiveQuest.REWARD_UPGRADE,   false),
        new QuestTemplate(ActiveQuest.TYPE_TREASURE,  4,  8,  25,  45, 4, ActiveQuest.REWARD_NONE,      false),
        new QuestTemplate(ActiveQuest.TYPE_TREASURE, 10, 18,  70, 110, 6, ActiveQuest.REWARD_NONE,      false),
        new QuestTemplate(ActiveQuest.TYPE_TREASURE, 20, 30, 130, 200, 10, ActiveQuest.REWARD_UPGRADE,  false),
        new QuestTemplate(ActiveQuest.TYPE_CHESTS,    2,  3,  30,  50, 4, ActiveQuest.REWARD_LOOT,      false),
        new QuestTemplate(ActiveQuest.TYPE_PIRATES,   2,  4,  35,  55, 4, ActiveQuest.REWARD_AMMO,      true),
        new QuestTemplate(ActiveQuest.TYPE_PIRATES,   8, 12,  90, 140, 6, ActiveQuest.REWARD_UPGRADE,   true),
        new QuestTemplate(ActiveQuest.TYPE_CANNONS,   2,  3,  60,  90, 5, ActiveQuest.REWARD_HAND_AMMO, true),
        new QuestTemplate(ActiveQuest.TYPE_LOOT,      2,  3,  50,  80, 5, ActiveQuest.REWARD_UPGRADE,   true),
    };

    void Start() { gridManager = FindFirstObjectByType<GridManager>(); }

    /// <summary>
    /// Otevře obchod pro daného hráče. sellMode = true → VÝKUPNA (zelený pult:
    /// prodej kořisti + vyplacení mega questu), false → QUESTY (oranžový pult).
    /// </summary>
    public void Open(int playerIndex = 0, bool sellMode = false)
    {
        if (playerIndex < 0 || playerIndex > 1) playerIndex = 0;
        buyerIndex = playerIndex;
        openFor[playerIndex]  = true;
        this.sellMode[playerIndex] = sellMode;
        lastMessage[playerIndex]   = null;

        // Nabídku questů generuj jen v quest módu a jen když hráč quest nemá.
        if (!sellMode && !GetQuest().hasQuest) GenerateOffers();
    }

    // ── Per-buyer přístup k datům (P1 vs P2) ─────────────────────────────────
    PlayerState PS() => Data.players[buyerIndex];

    int         GetCoins()         => PS().coins;
    void        SetCoins(int v)    => PS().coins = v;
    int         GetFish()          => PS().fishCount;
    void        SetFish(int v)     => PS().fishCount = v;
    int         GetTreasure()      => PS().treasureCount;
    void        SetTreasure(int v) => PS().treasureCount = v;
    ActiveQuest GetQuest()         => PS().activeQuest;
    MegaQuest   GetMega()          => PS().megaQuest;
    bool        HasSellBonus()     => PS().sellBonus;
    void        GiveSellBonus()    => PS().sellBonus = true;

    // Výkupní ceny — po splnění mega questu trvale bonus za kus.
    int FishPrice()     => EconomyConfig.FishPrice     + (HasSellBonus() ? EconomyConfig.SellBonusPerItem : 0);
    int TreasurePrice() => EconomyConfig.TreasurePrice + (HasSellBonus() ? EconomyConfig.SellBonusPerItem : 0);

    // Historický poklad potřebuje děda v kroku příběhu 0–1 ("přines mi ho").
    // Potom už ho nechce, takže ho jde prodat.
    bool StoryStillNeedsHistoricTreasure() => !Data.storyDone && Data.storyStep <= 1;

    /// <summary>Vyplacení mega questu (poklad z mapy). Mince + trvalý bonus na výkup.
    /// ~20 % pokladů je "historických" → hráč dostane i historický poklad (příběh).</summary>
    void ClaimMega()
    {
        MegaQuest mq = GetMega();
        if (!mq.active || !mq.dug) return;

        SetCoins(GetCoins() + mq.rewardCoins);
        GiveSellBonus();

        // ~20 % pokladů dá i "historický poklad" — chce ho starý námořník (příběh).
        if (mq.grantsHistoricalTreasure) Data.hasHistoricalTreasure = true;

        mq.Reset(); // hráč si teď může otevřít další bednu
        Save();
        SoundManager.PlayCoin();
    }

    // ── Generování nabídky questů ───────────────────────────────────────────
    private void GenerateOffers()
    {
        // Které šablony má smysl nabídnout: bojové jen hráči s dělem na lodi.
        bool hasCannon = BoatStats.HasCannon(PS().shipLevel);
        var available = new List<int>();
        var combat    = new List<int>();
        for (int i = 0; i < Templates.Length; i++)
        {
            if (Templates[i].needsCannon && !hasCannon) continue;
            available.Add(i);
            if (Templates[i].needsCannon) combat.Add(i);
        }

        // Tři různé šablony. Když hráč může bojovat, aspoň jedna je bojová —
        // ať se v nabídce neopakují jen ryby a poklady.
        var picks = new List<int>();
        if (combat.Count > 0) picks.Add(combat[Random.Range(0, combat.Count)]);
        while (picks.Count < 3 && picks.Count < available.Count)
        {
            int pick = available[Random.Range(0, available.Count)];
            if (!picks.Contains(pick)) picks.Add(pick); // už vylosovaná → losuj znovu
        }

        offeredQuests = new OfferedQuest[picks.Count];
        for (int i = 0; i < picks.Count; i++)
        {
            QuestTemplate t = Templates[picks[i]];
            int target = Random.Range(t.tMin, t.tMax + 1);
            offeredQuests[i] = new OfferedQuest
            {
                type       = t.type,
                desc       = DescribeQuest(t.type, target), // do savu jde text v jazyce z doby koupě, HUD ho ale skládá znovu
                target     = target,
                cost       = Random.Range(t.cMin, t.cMax + 1),
                multiplier = t.mult,
                rewardItem = t.rewardItem
            };
        }
    }

    /// <summary>
    /// Popis questu ("Ulov 10 ryb" / "Catch 10 fish") složený z typu a cíle, v aktuálním
    /// jazyce. Skládá se za běhu, protože text uložený v savu (ActiveQuest.description)
    /// by po přepnutí jazyka zůstal ve starém — tak ho HUD a obchod berou odsud.
    /// </summary>
    public static string DescribeQuest(int type, int target)
    {
        switch (type)
        {
            case ActiveQuest.TYPE_FISH:
                return Loc.T($"Ulov {target} " + Loc.Plural(target, "rybu", "ryby", "ryb", "", ""),
                             $"Catch {target} fish");
            case ActiveQuest.TYPE_TREASURE:
                return Loc.T($"Vytěž {target} " + Loc.Plural(target, "poklad", "poklady", "pokladů", "", ""),
                             $"Salvage {target} " + (target == 1 ? "treasure" : "treasures"));
            case ActiveQuest.TYPE_PIRATES:
                return Loc.T($"Potop {target} " + Loc.Plural(target, "pirátskou loď", "pirátské lodě", "pirátských lodí", "", ""),
                             $"Sink {target} pirate " + (target == 1 ? "ship" : "ships"));
            case ActiveQuest.TYPE_CANNONS:
                return Loc.T($"Znič {target} " + Loc.Plural(target, "dělo", "děla", "děl", "", "") + " na pirátských ostrovech",
                             $"Destroy {target} " + (target == 1 ? "cannon" : "cannons") + " on pirate islands");
            case ActiveQuest.TYPE_CHESTS:
                return Loc.T($"Otevři {target} " + Loc.Plural(target, "bednu", "bedny", "beden", "", "") + " na ostrovech",
                             $"Open {target} " + (target == 1 ? "chest" : "chests") + " on islands");
            case ActiveQuest.TYPE_LOOT:
                return Loc.T($"Získej {target} " + Loc.Plural(target, "kus", "kusy", "kusů", "", "") + " pirátské kořisti",
                             $"Take {target} " + (target == 1 ? "piece" : "pieces") + " of pirate loot");
            default:
                return "?";
        }
    }

    /// <summary>Krátký popis věcné odměny ("+ vylepšení lodi"), prázdný, když je odměna jen v mincích.</summary>
    public static string DescribeRewardItem(int rewardItem)
    {
        switch (rewardItem)
        {
            case ActiveQuest.REWARD_UPGRADE:   return Loc.T(" + vylepšení",        " + an upgrade");
            case ActiveQuest.REWARD_AMMO:      return Loc.T(" + 2× munice do děla", " + 2× cannon ammo");
            case ActiveQuest.REWARD_HAND_AMMO: return Loc.T(" + 2× náboje do pušky", " + 2× rifle ammo");
            case ActiveQuest.REWARD_LOOT:      return Loc.T(" + vzácná kořist",     " + rare loot");
            default:                           return "";
        }
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
        aq.rewardItem  = q.rewardItem;
        Save();
    }

    /// <summary>Vyzvednutí odměny za splněný quest (mince + případná věc).</summary>
    public void ClaimQuest()
    {
        ActiveQuest aq = GetQuest();
        if (!aq.hasQuest || !aq.IsComplete) return;

        SetCoins(GetCoins() + aq.reward);
        string itemText = GiveRewardItem(aq.rewardItem, aq.cost);

        lastMessage[buyerIndex] = Loc.T($"Vyplaceno {aq.reward} {Loc.CoinsWord(aq.reward)}", $"Paid out {aq.reward} {Loc.CoinsWord(aq.reward)}")
                                + (itemText != "" ? "  ·  " + itemText : "");
        aq.Reset(); // hráč si teď může koupit další
        GenerateOffers(); // čerstvá nabídka (když obchod otevřel s rozdělaným úkolem, žádná ještě nebyla)
        Save();
        SoundManager.PlayCoin();
    }

    /// <summary>Předá věcnou odměnu questu a vrátí její popis do hlášky.</summary>
    private string GiveRewardItem(int rewardItem, int questCost)
    {
        PlayerState ps = PS();
        switch (rewardItem)
        {
            case ActiveQuest.REWARD_UPGRADE:
            {
                // Náhodně jedno vylepšení z těch, co hráč ještě nemá.
                var missing = new List<int>();
                if (!ps.hasSpeedUpgrade)  missing.Add(0);
                if (!ps.hasRodUpgrade)    missing.Add(1);
                if (!ps.hasMiningUpgrade) missing.Add(2);

                if (missing.Count == 0)
                {
                    // Všechno už má → místo vylepšení mince navíc.
                    int bonus = questCost * 2;
                    SetCoins(GetCoins() + bonus);
                    return Loc.T($"vylepšení už máš všechna, místo nich +{bonus} {Loc.CoinsWord(bonus)}",
                                 $"you already own every upgrade, so +{bonus} {Loc.CoinsWord(bonus)} instead");
                }

                int pick = missing[Random.Range(0, missing.Count)];
                if (pick == 0) { ps.hasSpeedUpgrade  = true; return Loc.T("vylepšení: Rychlost lodi", "upgrade: Boat speed"); }
                if (pick == 1) { ps.hasRodUpgrade    = true; return Loc.T("vylepšení: Lepší prut",    "upgrade: Better rod"); }
                ps.hasMiningUpgrade = true;                  return Loc.T("vylepšení: Rychlost těžby", "upgrade: Mining speed");
            }
            case ActiveQuest.REWARD_AMMO:
                ps.ammo += 2 * EconomyConfig.AmmoPackSize;
                return Loc.T($"+{2 * EconomyConfig.AmmoPackSize} nábojů do děla", $"+{2 * EconomyConfig.AmmoPackSize} cannon rounds");
            case ActiveQuest.REWARD_HAND_AMMO:
                ps.handAmmo += 2 * EconomyConfig.HandAmmoPackSize;
                return Loc.T($"+{2 * EconomyConfig.HandAmmoPackSize} nábojů do pušky", $"+{2 * EconomyConfig.HandAmmoPackSize} rifle rounds");
            case ActiveQuest.REWARD_LOOT:
            {
                // Vzácná kořist: vždy jeden z dražších předmětů (druhá polovina seznamu).
                int index = Random.Range(LootItems.Items.Length / 2, LootItems.Items.Length);
                LootItems.Add(ps, index, 1);
                return Loc.T("kořist: ", "loot: ") + LootItems.Items[index].Name;
            }
            default:
                return "";
        }
    }

    // ── GUI ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!IsOpen) return;

        // Kresli panel pro každého hráče, co má obchod otevřený (klidně oba naráz).
        for (int i = 0; i < 2; i++)
        {
            if (!openFor[i]) continue;
            buyerIndex = i;
            DrawShopPanel(i);
        }
    }

    private void DrawShopPanel(int who)
    {
        // Pergamen jako dopis (ShopUI) — ve split screenu jen na půlce toho hráče.
        Rect inner = ShopUI.BeginSheet(who, 640f, 600f, out bool close);
        if (close) { openFor[who] = false; return; }

        bool sell = sellMode[who];

        // Tažení myší = scroll (kolečko řeší ScrollView samo).
        ShopUI.HandleDragScroll(ref scroll[who]);

        GUILayout.BeginArea(inner);

        string playerLabel = MultiplayerManager.IsMultiplayer
            ? Loc.T(buyerIndex == 0 ? "  —  HRÁČ 1" : "  —  HRÁČ 2",
                    buyerIndex == 0 ? "  —  PLAYER 1" : "  —  PLAYER 2")
            : "";
        string shopTitle = sell ? Loc.T("VÝKUPNA", "TRADING POST") : Loc.T("OBCHOD S QUESTY", "QUEST SHOP");
        GUILayout.Label(shopTitle + playerLabel, ShopUI.Title);
        GUILayout.Label(sell ? Loc.T("( výkup je na všech ostrovech stejný )", "( every island buys at the same prices )")
                             : Loc.T("( zaplať zálohu, splň úkol a vyzvedni si víc )", "( pay a deposit, finish the job, collect more )"),
                        ShopUI.Subtitle);
        GUILayout.Space(10);

        scroll[who] = GUILayout.BeginScrollView(scroll[who], GUILayout.Height(inner.height - ShopUI.Px(120f)));

        if (sell) DrawSellContent();
        else      DrawQuestContent();

        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.Label(Loc.T("Mince: ", "Coins: ") + GetCoins(), ShopUI.Coins);
        GUILayout.EndArea();
    }

    // ── Obsah VÝKUPNY (zelený pult) ─────────────────────────────────────────
    private void DrawSellContent()
    {
        // Vyplacení mega questu (poklad z mapy) — mince + trvalý bonus na výkup.
        MegaQuest mq = GetMega();
        if (mq.active && mq.dug)
        {
            GUILayout.Label(Loc.T("POKLAD Z MAPY", "TREASURE FROM THE MAP"), ShopUI.Section);
            GUILayout.Label(mq.grantsHistoricalTreasure
                ? Loc.T("Vykopaný poklad je výjimečný — je HISTORICKÝ.", "The treasure you dug up is exceptional — it is HISTORIC.")
                : Loc.T("Vykopaný poklad je připravený k vyplacení.",     "The treasure you dug up is ready to be cashed in."), ShopUI.Row);

            string coinsPart = $"{mq.rewardCoins} {Loc.CoinsWord(mq.rewardCoins)}";
            string btn = mq.grantsHistoricalTreasure
                ? Loc.T($"  VYPLATIT  {coinsPart}  +  HISTORICKÝ POKLAD  +  bonus!",
                        $"  CASH IN  {coinsPart}  +  HISTORIC TREASURE  +  bonus!")
                : Loc.T($"  VYPLATIT  {coinsPart}  +  trvalý bonus k výkupu!",
                        $"  CASH IN  {coinsPart}  +  permanent sell bonus!");
            if (SoundManager.Click(GUILayout.Button(btn, ShopUI.ClaimButton, GUILayout.Height(ShopUI.Px(38)))))
                ClaimMega();
            GUILayout.Space(14);
        }

        // ── Ryby a poklady ──
        GUILayout.Label(Loc.T("ÚLOVEK", "CATCH"), ShopUI.Section);

        int fish     = GetFish();
        int treasure = GetTreasure();
        int fp = FishPrice(), tp = TreasurePrice();
        string bonusTag = HasSellBonus() ? "  (bonus!)" : "";

        DrawSell(Loc.T($"Ryby  ×{fish}  ( {fp} {Loc.CoinsWord(fp)} / kus ){bonusTag}",
                       $"Fish  ×{fish}  ( {fp} {Loc.CoinsWord(fp)} each ){bonusTag}"),
            fish * fp, fish > 0,
            () => { SetCoins(GetCoins() + fish * fp); SetFish(0); Save(); SoundManager.PlayCoin(); });
        GUILayout.Space(4);
        DrawSell(Loc.T($"Poklady  ×{treasure}  ( {tp} {Loc.CoinsWord(tp)} / kus ){bonusTag}",
                       $"Treasure  ×{treasure}  ( {tp} {Loc.CoinsWord(tp)} each ){bonusTag}"),
            treasure * tp, treasure > 0,
            () => { SetCoins(GetCoins() + treasure * tp); SetTreasure(0); Save(); SoundManager.PlayCoin(); });

        // ── Kořist z pirátů ──
        GUILayout.Space(12);
        GUILayout.Label(Loc.T("KOŘIST Z PIRÁTŮ", "PIRATE LOOT"), ShopUI.Section);
        PlayerState ps = PS();
        if (LootItems.TotalCount(ps) == 0)
        {
            GUILayout.Label(Loc.T("( zatím nic — potopení piráti a stráže občas něco upustí )",
                                  "( nothing yet — sunk pirates and guards sometimes drop something )"), ShopUI.Faint);
        }
        else
        {
            for (int i = 0; i < LootItems.Items.Length; i++)
            {
                int count = LootItems.Count(ps, i);
                if (count == 0) continue;

                int index = i; // kopie pro lambdu
                int price = LootItems.Items[i].price;
                DrawSell(Loc.T($"{LootItems.Items[i].Name}  ×{count}  ( {price} / kus )",
                               $"{LootItems.Items[i].Name}  ×{count}  ( {price} each )"),
                    count * price, true,
                    () => { SetCoins(GetCoins() + count * price); LootItems.Add(PS(), index, -count); Save(); SoundManager.PlayCoin(); });
                GUILayout.Space(4);
            }
        }

        // ── Historický poklad ──
        if (Data.hasHistoricalTreasure)
        {
            GUILayout.Space(12);
            GUILayout.Label(Loc.T("HISTORICKÝ POKLAD", "HISTORIC TREASURE"), ShopUI.Section);
            if (StoryStillNeedsHistoricTreasure())
            {
                GUILayout.Label(Loc.T("Tenhle chce starý námořník — prodat ho půjde, až mu ho doneseš… nebo najdeš další.",
                                      "The old sailor wants this one — you can sell one once he has his… or find another."), ShopUI.Warn);
            }
            else
            {
                int hp = EconomyConfig.HistoricalTreasurePrice;
                DrawSell(Loc.T("Historický poklad  ×1  ( sběratelé ho chtějí )", "Historic treasure  ×1  ( collectors want it )"),
                    hp, true,
                    () => { SetCoins(GetCoins() + hp); Data.hasHistoricalTreasure = false; Save(); SoundManager.PlayCoin(); });
            }
        }

        if (fish == 0 && treasure == 0 && LootItems.TotalCount(ps) == 0 && !Data.hasHistoricalTreasure)
        {
            GUILayout.Space(8);
            GUILayout.Label(Loc.T("( nic k prodeji — nalov ryby, vytěž poklady nebo poraz piráty )",
                                  "( nothing to sell — catch fish, salvage treasure or beat pirates )"), ShopUI.Faint);
        }
    }

    // ── Obsah OBCHODU S QUESTY (oranžový pult) ──────────────────────────────
    private void DrawQuestContent()
    {
        // Hláška po vyplacení ("Vyplaceno 240 mincí · vylepšení: Lepší prut").
        if (!string.IsNullOrEmpty(lastMessage[buyerIndex]))
        {
            GUILayout.Label(lastMessage[buyerIndex], ShopUI.Owned);
            GUILayout.Space(8);
        }

        ActiveQuest aq = GetQuest();

        if (aq.hasQuest)
        {
            // Hráč už quest má → ukaž postup a případně tlačítko na vyzvednutí.
            GUILayout.Label(Loc.T("TVŮJ ÚKOL", "YOUR JOB"), ShopUI.Section);
            GUILayout.Label(DescribeQuest(aq.questType, aq.target), ShopUI.Row);
            GUILayout.Label(Loc.T("Postup:  ", "Progress:  ") + $"{aq.progress} / {aq.target}", ShopUI.Row);
            GUILayout.Label(Loc.T("Odměna:  ", "Reward:  ") + $"{aq.reward} {Loc.CoinsWord(aq.reward)}  ( {aq.multiplier}× )"
                            + DescribeRewardItem(aq.rewardItem), ShopUI.Row);
            GUILayout.Space(8);

            if (aq.IsComplete)
            {
                string claim = Loc.T($"  VYZVEDNOUT ODMĚNU  {aq.reward} {Loc.CoinsWord(aq.reward)}{DescribeRewardItem(aq.rewardItem)}",
                                     $"  CLAIM  {aq.reward} {Loc.CoinsWord(aq.reward)}{DescribeRewardItem(aq.rewardItem)}");
                if (SoundManager.Click(GUILayout.Button(claim, ShopUI.ClaimButton, GUILayout.Height(ShopUI.Px(38)))))
                    ClaimQuest();
            }
            else
            {
                GUILayout.Label(Loc.T("( úkol ještě není splněný — postup vidíš i nahoře na obrazovce )",
                                      "( not done yet — the progress is also shown at the top of the screen )"), ShopUI.Faint);
            }
            return;
        }

        // Hráč quest nemá → nabídni tři na výběr.
        GUILayout.Label(Loc.T("NABÍDKA ÚKOLŮ", "AVAILABLE JOBS"), ShopUI.Section);
        if (!BoatStats.HasCannon(PS().shipLevel))
            GUILayout.Label(Loc.T("( bojové úkoly dostaneš, až budeš mít loď s dělem )",
                                  "( combat jobs unlock once your boat has a cannon )"), ShopUI.Faint);
        GUILayout.Space(4);

        if (offeredQuests == null) return;
        for (int i = 0; i < offeredQuests.Length; i++)
        {
            var q = offeredQuests[i];
            int rewardCoins = q.cost * q.multiplier;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(DescribeQuest(q.type, q.target), ShopUI.Row);
            GUILayout.Label(Loc.T($"odměna: {rewardCoins} {Loc.CoinsWord(rewardCoins)}", $"reward: {rewardCoins} {Loc.CoinsWord(rewardCoins)}")
                            + DescribeRewardItem(q.rewardItem), ShopUI.Faint);
            GUILayout.EndVertical();
            GUILayout.Label($"{q.cost} {Loc.CoinsWord(q.cost)}", ShopUI.Row, GUILayout.Width(ShopUI.Px(90)));

            GUI.enabled = GetCoins() >= q.cost;
            bool buy = SoundManager.Click(GUILayout.Button(Loc.T("Vzít", "Take"), ShopUI.Button,
                                                           GUILayout.Width(ShopUI.Px(80)), GUILayout.Height(ShopUI.Px(28))));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (buy)
            {
                BuyQuest(q);
                break; // seznam se hned změní → ukonči smyčku
            }
        }
    }

    // Řádek prodeje: popis + celková částka + tlačítko "Prodat vše".
    private void DrawSell(string label, int total, bool enabled, System.Action onSell)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, ShopUI.Row, GUILayout.ExpandWidth(true));
        GUILayout.Label($"= {total} {Loc.CoinsWord(total)}", ShopUI.Row, GUILayout.Width(ShopUI.Px(110)));
        GUI.enabled = enabled; // nejde prodat, když hráč nic nemá
        if (SoundManager.Click(GUILayout.Button(Loc.T("Prodat vše", "Sell all"), ShopUI.Button,
                                                GUILayout.Width(ShopUI.Px(110)), GUILayout.Height(ShopUI.Px(28)))))
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
}
