using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryNpc.cs
//  Příběhové NPC — děda, který sedí na startovním ostrově. Vede hráče příběhem
//  po krocích (gameData.storyStep):
//    0 = start: řekne, ať si koupí aspoň malou loď a vrátí se
//    1 = má loď: chce se prokázat — 1000 mincí + historický poklad
//    2 = dostal souřadnice: navádí k příběhovému mega ostrovu (šipka na minimapě)
//    3 = na mega ostrově se něco děje — podrobnosti řídí gameData.megaTask
//        (0-2 = ostrov ještě neprošel, 3 = vzkaz nalezen). Dokud se tam něco
//        nedokončí, dialog není brána — nikam dál neposouvá.
//    4+ = pokračování (doplní se v pozdějších krocích .claude/story-plan.md)
//
//  Objekt "StoryNpc" je v SampleScene. Panáčka i jeho umístění si vytvoří sám
//  v Start(). V majáku / jiných scénách není.
// ─────────────────────────────────────────────────────────────────────────────

public class StoryNpc : MonoBehaviour
{
    private static string NpcName => Loc.T("Děda", "Grandpa"); // jméno v rámečku dialogu
    private const float  HintRange = 2.4f; // na kolik políček se ukáže nápověda "zmáčkni E"

    public static StoryNpc Instance { get; private set; }

    private const int   PROVE_COST = 1000; // kolik mincí chce starý námořník

    private GridManager gridManager;
    private Vector2Int  tilePos;      // políčko, na kterém děda sedí
    private bool        placed;
    private int         talkingWith = -1; // 0 = P1, 1 = P2, -1 = nikdo
    private int         line;
    private string[]    activeLines;  // repliky pro aktuální rozhovor (podle storyStep)
    private string[]    activeNames;  // kdo kterou repliku říká (null = všechny děda) — epilog s bratrem
    private bool        showGiveButton; // v kroku 1 se splněnými podmínkami
    private float       ignoreKeyUntil;
    private float       reopenAllowedAt;

    private GUIStyle nameStyle, textStyle, hintStyle, giveStyle;
    private bool     stylesReady;

    void Awake()  { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    private GameData Data => gridManager.gameData;
    private int StoryStep => Data.storyStep;

    // ── Dotazy pro PlayerController ─────────────────────────────────────────
    /// <summary>Sedí děda na tomhle políčku?</summary>
    public bool IsAt(int x, int y) => placed && x == tilePos.x && y == tilePos.y;

    /// <summary>Mluví zrovna tenhle hráč s dědou? (blokuje mu pohyb)</summary>
    public bool IsTalkingWith(int playerIndex) => talkingWith == playerIndex;

    /// <summary>Mluví s dědou někdo?</summary>
    public bool IsTalking => talkingWith != -1;

    /// <summary>Spustí dialog pro daného hráče (volá PlayerController po zmáčknutí E vedle dědy).</summary>
    public void StartTalk(int playerIndex)
    {
        if (talkingWith != -1) return;
        if (Time.unscaledTime < reopenAllowedAt) return; // právě jsme dialog zavřeli — nech E "vyprchat"
        talkingWith    = playerIndex;
        line           = 0;
        BuildDialogForStep();
        ignoreKeyUntil = Time.unscaledTime + 0.25f;
        SoundManager.PlayClick();
    }

    // ── Příběhový dialog ───────────────────────────────────────────────────
    // Sestaví repliky podle aktuálního kroku příběhu (a podmínek).
    private void BuildDialogForStep()
    {
        showGiveButton = false;
        activeNames    = null;

        // Příběh dohraný (Krok 7, ostrov 3) — zvláštní repliky podle koncovky,
        // nezávisle na StoryStep (ten se po ostrovu 3 už nikam neposouvá).
        if (Data.storyDone) { BuildEndingDialog(); return; }

        int shipLevel  = Data.players[talkingWith].shipLevel;
        int coins      = Data.players[talkingWith].coins;

        // Klávesy se liší podle hráče (P1 = WASD + myš, P2 = šipky + numpad) — děda
        // je říká správně tomu, kdo s ním zrovna mluví. Česky i anglicky zvlášť,
        // protože česká věta se skloňuje ("klávesami W A S D" × "šipkami").
        bool   p1        = talkingWith == 0;
        string csMove    = p1 ? "klávesami W A S D"  : "šipkami";
        string enMove    = p1 ? "W A S D"            : "the arrow keys";
        string csShoot   = p1 ? "myší"               : "klávesou Numpad *";
        string enShoot   = p1 ? "the mouse"          : "Numpad *";
        string kEnter    = p1 ? "E"                  : "Numpad 1";
        string kRepair   = p1 ? "R"                  : "Numpad /";
        string kMap      = p1 ? "M"                  : "Numpad 2";
        string csAction  = p1 ? "Mezerníkem"         : "Klávesou Numpad 0";
        string enAction  = p1 ? "Space"              : "Numpad 0";

        switch (StoryStep)
        {
            case 0:
                if (shipLevel <= 0)
                    activeLines = Loc.Pick(
                        new[]
                        {
                            "Á, návštěva. A vyplul jsi na obyčejné veslici, vidím.",
                            "Takhle daleko se nedostaneš, chlapče.",
                            "Kup si v majáku aspoň malou plachetnici a vrať se za mnou.",
                            $"A pamatuj: pluješ {csMove}, z děla střílíš {csShoot}.",
                            $"Klávesou {kEnter} vejdeš do majáku, {kRepair} opraví loď u mola a {kMap} ti otevře velkou mapu.",
                            $"{csAction} rybaříš na hejnech a těžíš poklady z vraků — jenže samo o sobě to mince nedá.",
                            "Úlovky a poklady prodáš ve výkupně, na zeleném pultu v majáku. Teprve pak z nich budou mince.",
                        },
                        new[]
                        {
                            "Ah, a visitor. And you sailed out on a plain old rowboat, I see.",
                            "You won't get far like that, lad.",
                            "Buy at least a small sailboat at the lighthouse and come back to me.",
                            $"And remember: you sail with {enMove}, and you fire the cannon with {enShoot}.",
                            $"Press {kEnter} to enter the lighthouse, {kRepair} repairs your boat at the pier, and {kMap} opens the big map.",
                            $"{enAction} lets you fish the shoals and salvage treasure from wrecks — but that alone won't put coins in your pocket.",
                            "Sell your catch and treasure at the trading post — the green counter in the lighthouse. Only then does it turn into coins.",
                        });
                else
                    activeLines = Loc.Pick(
                        new[]
                        {
                            "Á, teď už máš pořádnou loď. To se mi líbí.",
                            "Než tě pošlu za tím, co hledám, musím vědět, že ti můžu věřit.",
                            "Přines mi 1000 mincí a historický poklad.",
                            "Historický poklad se občas skrývá v pokladech ze starých map — těch z beden.",
                            "Ryby a poklady, co uloví tvoje loď, prodáš ve výkupně (zelený pult v majáku).",
                            "Vrať se, až budeš mít obojí.",
                        },
                        new[]
                        {
                            "Ah, now you have a proper ship. I like that.",
                            "Before I send you after what I'm looking for, I need to know I can trust you.",
                            "Bring me 1,000 coins and a historic treasure.",
                            "A historic treasure sometimes hides among the loot from old maps — the ones found in chests.",
                            "Whatever your ship catches or salvages, you sell at the trading post — the green counter in the lighthouse.",
                            "Come back when you have both.",
                        });
                break;

            case 1:
                if (coins >= PROVE_COST && Data.hasHistoricalTreasure)
                {
                    activeLines = Loc.Pick(
                        new[] { "Tak co, máš pro mě 1000 mincí a ten historický poklad?" },
                        new[] { "Well then — do you have 1,000 coins and that historic treasure for me?" });
                    showGiveButton = true;
                }
                else
                {
                    activeLines = Loc.Pick(
                        new[]
                        {
                            "Ještě to nemáš. Chci 1000 mincí a historický poklad.",
                            "Ten historický ti může vypadnout, když ve výkupně (zelený pult v majáku) vyplatíš poklad z mapy. Poznáš ho.",
                        },
                        new[]
                        {
                            "You don't have it yet. I want 1,000 coins and a historic treasure.",
                            "The historic one can turn up when you cash in a map treasure at the trading post (the green counter in the lighthouse). You'll know it when you see it.",
                        });
                }
                break;

            case 2:
                // Hráč pluje k dalšímu mega ostrovu — děda reaguje na to, co se stalo
                // na tom PŘEDCHOZÍM (megaIndex = kolikátý ostrov je na řadě).
                BuildTravelDialog();
                break;

            case 3:
                // Dokud hráč nedokončil, co má na ostrově udělat (megaTask < 3),
                // dá mu děda jen obecné povzbuzení — detailní repliky přidají
                // další kroky, až bude mít ostrov skutečný obsah.
                if (Data.megaTask < 3)
                    activeLines = Loc.Pick(
                        new[]
                        {
                            "Byl jsi tam, co? Cítím to na tobě.",
                            "Ale ještě jsi to nedokončil. Dokonči, co jsi začal, a vrať se za mnou.",
                        },
                        new[]
                        {
                            "So you've been there, haven't you? I can feel it on you.",
                            "But you're not finished yet. See it through, then come back to me.",
                        });
                else
                    activeLines = Loc.Pick(
                        new[]
                        {
                            "Tak povídej, co jsi tam našel.",
                            "Nech mě přemýšlet. Řeknu ti víc, až tomu porozumím.",
                        },
                        new[]
                        {
                            "Well, tell me — what did you find there?",
                            "Let me think. I'll tell you more once I understand it.",
                        });
                break;

            default:
                activeLines = Loc.Pick(
                    new[] { "Ta stopa nás dovede dál. Buď trpělivý, chlapče." },
                    new[] { "That trail will lead us further. Be patient, lad." });
                break;
        }
    }

    // Děda mezi ostrovy (storyStep 2). Souřadnice prozradí JEN u prvního ostrova —
    // druhý se hledá podle hádanky v dopise (kdyby je děda řekl, hádanka by neměla smysl)
    // a ke třetímu vede šipka ze vzkazu na Hřbitově lodí.
    private void BuildTravelDialog()
    {
        if (Data.megaIndex <= 0)
        {
            activeLines = Loc.Pick(
                new[]
                {
                    $"Ostrov leží na souřadnicích [{Data.storyIslandX}, {Data.storyIslandY}].",
                    "Najdeš je nahoře na obrazovce a na minimapě uvidíš šipku. Drž se jí.",
                    "Je to daleko. Až tam dorazíš, poznáš to.",
                },
                new[]
                {
                    $"The island lies at [{Data.storyIslandX}, {Data.storyIslandY}].",
                    "You'll see the coordinates at the top of the screen, and an arrow on the minimap. Follow it.",
                    "It's far. When you get there, you'll know.",
                });
        }
        else if (Data.megaIndex == 1)
        {
            // Po Pirátském ostrově: hráč nese dopis, který začíná slovem "Bratře".
            activeLines = Loc.Pick(
                new[]
                {
                    "Dopis z trezoru? Ukaž mi ho…",
                    "To písmo. Takhle psal jen jeden člověk, kterého jsem znal.",
                    "Ale to není možné. Můj bratr utonul, když jsem byl ještě kluk. Pohřbili jsme prázdnou rakev.",
                    "Kdo jiný by mi ale napsal „bratře“?",
                    "Kam tě posílá, ti neřeknu — nevím to. Je to schované v té hádance. Dopis máš v Deníku.",
                    Data.ambush1Done
                        ? "Spočítej to a najdi to místo na mapě. A prý jsi na moři skolil nějakou bestii? Ukaž mi pak ten zub!"
                        : "Spočítej to a najdi to místo na mapě. A dávej pozor na moři, prý se tam něco velkého prohání.",
                },
                new[]
                {
                    "A letter from the vault? Show it to me…",
                    "That handwriting. Only one person I ever knew wrote like that.",
                    "But it's impossible. My brother drowned when I was still a boy. We buried an empty coffin.",
                    "Yet who else would ever call me “brother”?",
                    "Where it sends you, I can't tell you — I don't know. It's hidden in that riddle. The letter is in your Journal.",
                    Data.ambush1Done
                        ? "Work it out and find the place on the map. And they say you slew some beast out at sea? Show me that tooth sometime!"
                        : "Work it out and find the place on the map. And mind the sea — they say something big is roaming out there.",
                });
        }
        else
        {
            // Po Hřbitově lodí: vzkaz "Vylezl jsem z vody a loď s vámi byla pryč…".
            activeLines = Loc.Pick(
                new[]
                {
                    "Hřbitov lodí… Tam jsme tehdy ztroskotali. Táta mě vytáhl z vody a veslovali jsme pryč.",
                    "Říkal, že bratra už nenajdeme. Že ho moře vzalo.",
                    "A on celou tu dobu čekal. Čekal na nás. A my se nevrátili.",
                    DiaryPages.Count(Data) > 0
                        ? "A ty stránky, cos našel… To je jeho ruka. Psal je pro mě, celé ty roky."
                        : "Celé ty roky… a já si myslel, že jsem jediný, kdo přežil.",
                    "Pluj za ním. Šipka na minimapě tě dovede. Ať je to s ním jakkoli — nenechávej ho tam podruhé.",
                },
                new[]
                {
                    "The Ship Graveyard… That's where we were wrecked back then. Father pulled me out of the water and we rowed away.",
                    "He said we would never find my brother. That the sea had taken him.",
                    "And all that time he was waiting. Waiting for us. And we never came back.",
                    DiaryPages.Count(Data) > 0
                        ? "And those pages you found… That's his hand. He wrote them for me, all those years."
                        : "All those years… and I thought I was the only one who survived.",
                    "Go after him. The arrow on the minimap will lead you. Whatever has become of him — don't leave him out there a second time.",
                });
        }
    }

    // Repliky u dědy po dohrání příběhu (Krok 7) — jiný text podle koncovky
    // (Data.storyEnding: 1 = bratr ušetřen, 2 = bratr zabit).
    // Poprvé po finále (storyEpilogueSeen = false) dlouhý epilog — u konce 1 rozhovor dědy
    // s bratrem (repliky se střídají, activeNames), pak závěrečná obrazovka. Potom už jen
    // krátké repliky.
    private void BuildEndingDialog()
    {
        string g = NpcName, b = Loc.T("Bratr", "Brother");

        if (Data.storyEpilogueSeen)
        {
            if (Data.storyEnding == 1)
            {
                activeLines = Loc.Pick(new[] { "Konečně je dům zase plný.", "A moře je zase jen moře." },
                                       new[] { "At last the house is full again.", "And the sea is just the sea again." });
                activeNames = new[] { g, b };
            }
            else
                activeLines = Loc.Pick(new[] { "Někdy se dívám na moře a přemýšlím, jestli to tak muselo být." },
                                       new[] { "Sometimes I look at the sea and wonder whether it had to end that way." });
            return;
        }

        if (Data.storyEnding == 1)
        {
            activeLines = Loc.Pick(
                new[]
                {
                    "Ty… to není možné.",
                    "Ahoj, bratříčku. Zestárl jsi.",
                    "Celý život jsem si myslel, že jsme tě tam nechali umřít.",
                    "Nechali. Ale přežil jsem. A teď už nikam nepoplu.",
                    "Odpusť nám to. Odpusť to tátovi… i mně.",
                    "Proto jsem se vrátil. Sedni si, budeme si mít co vyprávět.",
                    "Děkuju, chlapče. Přivezl jsi domů víc než poklad.",
                },
                new[]
                {
                    "You… it can't be.",
                    "Hello, little brother. You've grown old.",
                    "All my life I believed we left you there to die.",
                    "You did. But I survived. And now I'm not sailing anywhere.",
                    "Forgive us. Forgive Father… and me.",
                    "That's why I came back. Sit down — we have a lot to talk about.",
                    "Thank you, lad. You brought home more than a treasure.",
                });
            activeNames = new[] { g, b, g, b, g, b, g };
        }
        else
            activeLines = Loc.Pick(
                new[]
                {
                    "Máš to. Dědictví.",
                    "A on?",
                    "…Rozumím. Dál se neptám.",
                    "Víš, měl jsem syna. Taky si ho vzalo moře — jednou vyplul a už se nevrátil.",
                    "Roky jsem čekal u okna, stejně jako čekal on tam na útesu.",
                    "Možná si moře od naší rodiny všechno jen půjčuje. A jednou si to vezme zpátky.",
                    "Děkuju, žes to dotáhl až do konce. Ať to stálo cokoli.",
                },
                new[]
                {
                    "You have it. The inheritance.",
                    "And him?",
                    "…I see. I won't ask.",
                    "You know, I had a son. The sea took him too — he sailed out one day and never came back.",
                    "I waited by the window for years, just as he waited out there on the cliff.",
                    "Perhaps the sea only ever borrows from our family. And one day it takes it all back.",
                    "Thank you for seeing it through to the end. Whatever it cost.",
                });
    }

    // Volá se, když hráč dočte poslední repliku (nebo dialog ukončí).
    private void OnDialogFinished()
    {
        // Epilog dočtený poprvé (až do poslední repliky) → konec příběhu: zruš šipku domů
        // a ukaž závěrečnou obrazovku. Zavřený dřív Escapem = epilog se ukáže příště znovu.
        if (Data.storyDone && !Data.storyEpilogueSeen && activeLines != null && line >= activeLines.Length - 1)
        {
            Data.storyEpilogueSeen = true;
            foreach (var ps in Data.players)
                if (ps.hasWaypoint && ps.waypointX == Data.storyNpcX && ps.waypointY == Data.storyNpcY) ps.hasWaypoint = false;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            EndScreen.Show();
            return;
        }

        if (StoryStep == 0)
        {
            int shipLevel = Data.players[talkingWith].shipLevel;
            if (shipLevel >= 1) { Data.storyStep = 1; gridManager.Save(); }
        }
        // StoryStep 3 už dialog sám dál neposouvá — to teď dělá postup na
        // mega ostrově (megaTask) a GridManager.GiveNextMegaIsland() v
        // pozdějším kroku. Rozhovor s dědou je tu jen volitelný komentář.
    }

    // Tlačítko "dát starému námořníkovi 1000 mincí + historický poklad" (krok 1).
    private void GiveToSailor()
    {
        int coins = Data.players[talkingWith].coins;
        if (coins < PROVE_COST || !Data.hasHistoricalTreasure) return;

        Data.players[talkingWith].coins -= PROVE_COST;
        Data.hasHistoricalTreasure = false;

        // Vylosuj daleké místo pro příběhový mega ostrov (deterministicky podle
        // pozice hráče, ať to má každá hra jinde).
        Vector2Int first = GridManager.FirstMegaIslandPos(Data.players[0].gridX, Data.players[0].gridY);
        int sx = first.x, sy = first.y;

        gridManager.PlaceMegaIsland(sx, sy);

        Data.storyStep   = 2;
        Data.players[0].hasWaypoint = true;   // šipka na minimapě povede k ostrovu (P1)
        Data.players[0].waypointX   = sx;
        Data.players[0].waypointY   = sy;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        SoundManager.PlayCoin();

        // Pokračuj rovnou navazujícími replikami.
        activeLines = Loc.Pick(
            new[]
            {
                "Výborně. Přesně tohle jsem potřeboval.",
                $"To, co hledám, leží na ostrově na [{sx}, {sy}]. Daleko na moři.",
                "Souřadnice máš nahoře na obrazovce a na minimapě šipku. Vydej se tam.",
            },
            new[]
            {
                "Excellent. This is exactly what I needed.",
                $"What I'm looking for lies on the island at [{sx}, {sy}]. Far out at sea.",
                "You'll see the coordinates at the top of the screen and an arrow on the minimap. Set sail.",
            });
        line = 0;
        showGiveButton = false;
        ignoreKeyUntil = Time.unscaledTime + 0.2f;
    }

    /// <summary>Volá PlayerController, když hráč vstoupí na příběhový mega ostrov.</summary>
    public static void OnReachedStoryIsland()
    {
        var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null || d.storyStep != 2) return;

        d.storyStep   = 3;
        // Cíl splněn — waypoint na ostrov zruš oběma hráčům (ve split screenu si ho mohl dát i P2).
        foreach (var ps in d.players)
            if (ps.hasWaypoint && Mathf.Abs(ps.waypointX - d.storyIslandX) <= 20 && Mathf.Abs(ps.waypointY - d.storyIslandY) <= 20)
                ps.hasWaypoint = false;
        GameSession.Instance.Save();

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.NotifyWorldChanged();

        // Uvítání podle ostrova (dřív jedna věta pro všechny tři — u Hřbitova lodí nesedělo).
        string msg = d.megaIndex == 0
            ? Loc.T("Pirátský ostrov. Hradby, děla na věžích a ozbrojená posádka — budeš se muset probojovat dovnitř.",
                    "Pirate Island. Walls, cannons on the towers and an armed crew — you'll have to fight your way in.")
            : d.megaIndex == 1
            ? Loc.T("Hřbitov lodí. Z mělčiny trčí trosky — někde mezi nimi jsou kousky roztržené mapy.",
                    "The Ship Graveyard. Wrecks jut out of the shallows — somewhere among them lie pieces of a torn map.")
            : Loc.T("Poslední ostrov. Příjezd hlídá velká loď — a na palubě někdo, kdo tě čeká.",
                    "The Final Island. A large ship guards the approach — and someone aboard is waiting for you.");
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(msg, 5f);
    }

    // ───────────────────────────────────────────────────────────────────────
    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        StartCoroutine(PlaceWhenWorldReady());
    }

    // Startovní ostrov je vygenerovaný hned v GridManager.Awake(), ale pro jistotu
    // (pořadí Awake/Start) zkusíme umístění i pár snímků po sobě. Navíc se okraj
    // ostrova občas dorovná až dodatečně — tak po chvíli zkontrolujeme, že dědovi
    // políčko nezůstalo na vodě, a případně ho přesadíme.
    private IEnumerator PlaceWhenWorldReady()
    {
        // Nech generaci startovního ostrova po startu doběhnout.
        for (int i = 0; i < 12; i++) yield return null;

        for (int tries = 0; tries < 120 && !placed; tries++)
        {
            TryPlace();
            if (placed) break;
            yield return null;
        }

        // Kontrola po usazení světa: první ~3 s po startu se okraj ostrova může
        // ještě dorovnat — kdyby dědovi políčko skončilo na vodě, přesadíme ho.
        for (int check = 0; check < 6; check++)
        {
            yield return new WaitForSeconds(0.5f);
            if (!placed || TileStillGood(tilePos)) continue;

            ClearFigure();
            placed = false;
            yield return null; // nech starou postavičku zmizet
            for (int tries = 0; tries < 60 && !placed; tries++)
            {
                TryPlace();
                if (placed) break;
                yield return null;
            }
        }
    }

    // Je políčko pořád dost "na ostrově" (obklopené pevninou, nebo aspoň 3 sousedi)?
    private bool TileStillGood(Vector2Int t)
        => SurroundedByLand(t) || LandNeighbourCount(t) >= 3;

    // Zahodí díly postavičky (před přesazením).
    private void ClearFigure()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
    }

    private void TryPlace()
    {
        if (gridManager == null) { gridManager = FindFirstObjectByType<GridManager>(); return; }

        var data = gridManager.gameData;

        // Už dřív usazený děda → použij ULOŽENÉ políčko (pokud je pořád v pořádku),
        // ať se po návratu z majáku nestěhuje.
        if (data.storyNpcPlaced)
        {
            var saved = new Vector2Int(data.storyNpcX, data.storyNpcY);
            if (TileStillGood(saved))
            {
                tilePos = saved;
                placed  = true;
                gridManager.ReserveNpcTile(tilePos.x, tilePos.y);
                BuildFigure();
                return;
            }
            // uložené políčko se rozpadlo (kraj ostrova se přeskládal) → vyber znovu
        }

        List<Vector2Int> harbor = gridManager.GetStartIslandHarborTiles();
        if (harbor.Count == 0) return;

        // Děda musí SEDĚT NA OSTROVĚ — jen políčko, které má kolem sebe (4-směrně)
        // samou pevninu (ne kraj / molo / vodu), aby nekoukal z ničeho. Vynech
        // taky políčka těsně u majáku. Referenční bod pro "nejblíž" je počátek
        // světa [0,0] (tam je startovní ostrov) — NE aktuální pozice hráče,
        // jinak by se děda "stěhoval" podle toho, odkud hráč zrovna přišel.
        Vector2Int origin = Vector2Int.zero;
        Vector2Int best = default; float bestDist = float.MaxValue; bool has = false;

        foreach (var t in harbor)
        {
            if (!SurroundedByLand(t)) continue;
            if (NextToLighthouse(t))  continue;
            float d = (t - origin).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; has = true; }
        }

        // Nouzovka: kdyby žádné plně obklopené nebylo (mrňavý ostrov), vezmi
        // aspoň políčko se 3 pevninovými sousedy dál od kraje.
        if (!has)
        {
            foreach (var t in harbor)
            {
                if (LandNeighbourCount(t) < 3 || NextToLighthouse(t)) continue;
                float d = (t - origin).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = t; has = true; }
            }
        }
        if (!has) return; // zkusíme příští snímek

        tilePos = best;
        placed  = true;

        // Ulož políčko napevno.
        data.storyNpcPlaced = true;
        data.storyNpcX      = best.x;
        data.storyNpcY      = best.y;
        gridManager.Save();

        // Na dědově políčku nesmí být žádná dekorace (kámen / palma) — ať trčí
        // ze země panáček, ne trs trávy. GridManager to zařídí i po opětovném
        // vygenerování dlaždice.
        gridManager.ReserveNpcTile(tilePos.x, tilePos.y);

        BuildFigure();
    }

    private bool NextToLighthouse(Vector2Int t) => HasNeighbour(t, TileType.Lighthouse);

    private bool HasNeighbour(Vector2Int t, TileType type)
    {
        return gridManager.GetTileType(t.x + 1, t.y) == type
            || gridManager.GetTileType(t.x - 1, t.y) == type
            || gridManager.GetTileType(t.x, t.y + 1) == type
            || gridManager.GetTileType(t.x, t.y - 1) == type;
    }

    private static bool IsLand(TileType t)
        => t == TileType.Harbor || t == TileType.Lighthouse || t == TileType.Chest;

    private bool SurroundedByLand(Vector2Int t)
        => IsLand(gridManager.GetTileType(t.x + 1, t.y))
        && IsLand(gridManager.GetTileType(t.x - 1, t.y))
        && IsLand(gridManager.GetTileType(t.x, t.y + 1))
        && IsLand(gridManager.GetTileType(t.x, t.y - 1));

    private int LandNeighbourCount(Vector2Int t)
    {
        int n = 0;
        if (IsLand(gridManager.GetTileType(t.x + 1, t.y))) n++;
        if (IsLand(gridManager.GetTileType(t.x - 1, t.y))) n++;
        if (IsLand(gridManager.GetTileType(t.x, t.y + 1))) n++;
        if (IsLand(gridManager.GetTileType(t.x, t.y - 1))) n++;
        return n;
    }

    // ── Panáček (stejné díly jako hráč, jen sedí a je starý) ─────────────────
    private void BuildFigure()
    {
        transform.position = new Vector3(tilePos.x, 0.02f, tilePos.y);

        // Otoč dědu směrem k nejbližšímu molu / k moři, ať kouká na hladinu.
        Vector2Int look = NearestPier() ?? new Vector2Int(tilePos.x, tilePos.y - 1);
        Vector3 dir = new Vector3(look.x - tilePos.x, 0f, look.y - tilePos.y);
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);

        // Nejdřív zkus Kenney model postavy (starý námořník = trochu vybledlá barva,
        // animace "sit" — sedí). Když v Resources není, postav dědu ze základních
        // tvarů jako dřív.
        var model = CharacterModel.TryBuild(transform, "character-male-e",
            CharacterModel.DEFAULT_SCALE, new Color(0.78f, 0.75f, 0.72f), "SitAnim");
        if (model != null)
        {
            // Póza "sit" je "schoulená" (hlava níž, boky v ~0,33) — model trochu
            // zvedni, ať nejde nohama do země a zadkem sedí na sudu (odladěno ručně).
            model.transform.localPosition += new Vector3(0f, 0.24f, 0f);
            AddSeat(transform);
            RefreshCompanion();
            return;
        }

        Material coat  = MakeMat(new Color(0.30f, 0.33f, 0.42f)); // obnošený modrý kabát
        Material skin  = MakeMat(new Color(0.83f, 0.66f, 0.53f));
        Material hair  = MakeMat(new Color(0.88f, 0.88f, 0.85f)); // šedé vlasy / vousy

        AddPart(PrimitiveType.Cube,     "Legs",  new Vector3(0f, 0.12f, 0.30f), new Vector3(0.48f, 0.16f, 0.64f), Vector3.zero,             coat);
        AddPart(PrimitiveType.Capsule,  "Body",  new Vector3(0f, 0.40f, -0.02f), new Vector3(0.44f, 0.40f, 0.44f), new Vector3(-12f, 0f, 0f), coat);
        AddPart(PrimitiveType.Sphere,   "Head",  new Vector3(0f, 0.78f, 0.03f), new Vector3(0.40f, 0.38f, 0.40f), Vector3.zero,             skin);
        AddPart(PrimitiveType.Cylinder, "Hair",  new Vector3(0f, 0.95f, 0.02f), new Vector3(0.46f, 0.07f, 0.46f), Vector3.zero,             hair);
        AddPart(PrimitiveType.Cube,     "Beard", new Vector3(0f, 0.66f, 0.17f), new Vector3(0.24f, 0.26f, 0.12f), Vector3.zero,             hair);
        AddPart(PrimitiveType.Cube,     "Nose",  new Vector3(0f, 0.77f, 0.23f), new Vector3(0.08f, 0.08f, 0.13f), Vector3.zero,             skin);
        RefreshCompanion();
    }

    // ── Bratr doma (konec 1 — ušetřen) ───────────────────────────────────────
    private GameObject companion;

    /// <summary>Postaví (nebo zase uklidí) bratra vedle dědy podle konce příběhu. Volá se
    /// při stavbě dědy a hned po volbě ve finále (RivalNpc). Mluví se s ním přes dědu.</summary>
    public void RefreshCompanion()
    {
        bool want = placed && Data.storyDone && Data.storyEnding == 1;
        if (!want) { if (companion != null) Destroy(companion); companion = null; return; }
        if (companion != null) return;

        // Sousední pevninové políčko (ne maják, ne voda) — první vhodné kolem dědy.
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        Vector2Int spot = tilePos + Vector2Int.right;
        foreach (var d in dirs)
            if (gridManager.GetTileType(tilePos.x + d.x, tilePos.y + d.y) == TileType.Harbor) { spot = tilePos + d; break; }
        gridManager.ClearTileDecor(spot.x, spot.y); // ať mu z hlavy nekouká palma

        companion = new GameObject("BrotherAtHome");
        companion.transform.SetParent(transform, false);
        companion.transform.position = new Vector3(spot.x, 0.02f, spot.y);
        Vector3 toGrandpa = new Vector3(tilePos.x - spot.x, 0f, tilePos.y - spot.y);
        if (toGrandpa.sqrMagnitude > 0.01f) companion.transform.rotation = Quaternion.LookRotation(toGrandpa);

        // Stejný model jako bratr na Posledním ostrově (RivalNpc). Sedí na sudu čelem k dědovi,
        // stejně jako děda ("na starém ostrově teď sedí dva" — EndScreen).
        var model = CharacterModel.TryBuild(companion.transform, "character-male-c",
            CharacterModel.DEFAULT_SCALE, new Color(0.62f, 0.60f, 0.58f), "SitAnim");
        if (model != null)
        {
            model.transform.localPosition += new Vector3(0f, 0.24f, 0f); // stejné zvednutí jako u dědy
            AddSeat(companion.transform);
        }
        else
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(companion.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale    = new Vector3(0.44f, 0.5f, 0.44f);
            body.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(0.24f, 0.26f, 0.30f));
        }
    }

    // Dřevěný sud, na kterém děda sedí (animace "sit" ho posadí zhruba do této výšky).
    private void AddSeat(Transform parent)
    {
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = barrel.GetComponent<Collider>();
        if (col != null) Destroy(col);
        barrel.name = "DedaSeat";
        barrel.transform.SetParent(parent, false);
        barrel.transform.localPosition = new Vector3(0f, 0.15f, -0.12f);  // pod zadkem, kousek dozadu
        barrel.transform.localScale    = new Vector3(0.52f, 0.16f, 0.52f); // nízká stolička, horní hrana ~0,31 = výška sedu

        var r = barrel.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = MakeMat(new Color(0.36f, 0.24f, 0.14f)); // tmavé dřevo
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    private Vector2Int? NearestPier()
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
            if (gridManager.GetTileType(tilePos.x + d.x, tilePos.y + d.y) == TileType.Pier)
                return tilePos + d;

        // molo nemusí být přímo vedle — projdi širší okolí
        for (int r = 2; r <= 8; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    if (gridManager.GetTileType(tilePos.x + x, tilePos.y + y) == TileType.Pier)
                        return new Vector2Int(tilePos.x + x, tilePos.y + y);
        return null;
    }

    private void AddPart(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // NPC nemá nic blokovat

        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.transform.localEulerAngles = euler;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // ── Dialog ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (talkingWith == -1) return;
        if (Time.unscaledTime < ignoreKeyUntil) return;

        bool advance = talkingWith == 0
            ? (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            : (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Keypad0));
        bool close = talkingWith == 0
            ? Input.GetKeyDown(KeyCode.Escape)
            : Input.GetKeyDown(KeyCode.KeypadEnter);

        if (close) { OnDialogFinished(); EndTalk(); return; }
        if (!advance) return;

        // Na poslední replice s tlačítkem "dát" se klávesou dál neposouvá —
        // hráč musí kliknout na tlačítko (nebo zavřít Escapem).
        if (showGiveButton && line >= activeLines.Length - 1) return;

        line++;
        ignoreKeyUntil = Time.unscaledTime + 0.12f;
        if (line >= activeLines.Length) { OnDialogFinished(); EndTalk(); return; }
        SoundManager.PlayClick();
    }

    private void EndTalk()
    {
        talkingWith     = -1;
        line            = 0;
        reopenAllowedAt = Time.unscaledTime + 0.35f; // ať tentýž stisk E hned neotevře dialog znovu
    }

    void OnGUI()
    {
        if (!placed) return;
        InitStyles();

        if (talkingWith != -1) { DrawDialog(talkingWith); return; }

        // Nápověda "zmáčkni E", když je hráč pěšky blízko.
        MaybeHint(0);
        if (MultiplayerManager.IsMultiplayer) MaybeHint(1);
    }

    private void MaybeHint(int playerIndex)
    {
        int px = gridManager.gameData.players[playerIndex].gridX;
        int py = gridManager.gameData.players[playerIndex].gridY;

        float dist = Mathf.Max(Mathf.Abs(px - tilePos.x), Mathf.Abs(py - tilePos.y));
        if (dist > HintRange) return;

        Rect half = HalfRect(playerIndex);
        string key = playerIndex == 0 ? "E" : "Numpad 1";
        var r = new Rect(half.x, half.yMax - 90f, half.width, 26f);
        GUI.Label(r, "[" + key + "]  " + Loc.T("promluv s dědou", "talk to Grandpa"), hintStyle);
    }

    private void DrawDialog(int playerIndex)
    {
        Rect half = HalfRect(playerIndex);

        // Všechno se škáluje podle výšky obrazovky (HudSkin.GuiScale), ať je text čitelný.
        float s = HudSkin.GuiScale;
        nameStyle.fontSize = Mathf.RoundToInt(20f * s);
        textStyle.fontSize = Mathf.RoundToInt(21f * s);
        hintStyle.fontSize = Mathf.RoundToInt(16f * s);
        giveStyle.fontSize = Mathf.RoundToInt(17f * s);

        float w = Mathf.Min(780f * s, half.width - 40f);
        float h = 200f * s;
        var box = new Rect(half.x + (half.width - w) / 2f, half.yMax - h - 38f, w, h);

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.75f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f * s), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float pad = 20f * s;
        int li = activeLines != null ? Mathf.Clamp(line, 0, activeLines.Length - 1) : 0;
        string speaker = activeNames != null && li < activeNames.Length ? activeNames[li] : NpcName;
        GUI.Label(new Rect(box.x + pad, box.y + 10f * s, box.width - 2 * pad, 30f * s), speaker, nameStyle);
        string txt = activeLines != null && activeLines.Length > 0 ? activeLines[li] : "";
        GUI.Label(new Rect(box.x + pad, box.y + 46f * s, box.width - 2 * pad, 100f * s), txt, textStyle);

        string key = playerIndex == 0 ? "E" : "Numpad 1";
        bool lastLine = activeLines == null || line >= activeLines.Length - 1;

        // Krok 1 se splněnými podmínkami: na poslední replice tlačítko "dát".
        if (showGiveButton && lastLine)
        {
            float bw = Mathf.Min(460f * s, box.width - 2 * pad);
            var br = new Rect(box.x + box.width / 2f - bw / 2f, box.yMax - 44f * s, bw, 34f * s);
            if (SoundManager.Click(GUI.Button(br, Loc.T("Dát mu 1000 mincí + historický poklad", "Give him 1,000 coins + the historic treasure"), giveStyle)))
                GiveToSailor();
        }
        else
        {
            string more = lastLine ? "[" + key + "] " + Loc.T("konec", "close")
                                   : "[" + key + "] " + Loc.T("dál",   "next");
            GUI.Label(new Rect(box.x + pad, box.yMax - 34f * s, box.width - 2 * pad, 26f * s), more, hintStyle);
        }
    }

    // Obrazovka celá (sólo) nebo levá / pravá půlka (split screen).
    private Rect HalfRect(int playerIndex)
    {
        if (!MultiplayerManager.IsMultiplayer)
            return new Rect(0f, 0f, Screen.width, Screen.height);
        float w = Screen.width * 0.5f;
        return new Rect(playerIndex == 1 ? w : 0f, 0f, w, Screen.height);
    }

    private void InitStyles()
    {
        if (stylesReady) return;

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.45f) }
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, wordWrap = true,
            normal = { textColor = new Color(0.95f, 0.95f, 0.95f) }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.85f, 0.7f) }
        };
        giveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = Solid(new Color(0.2f, 0.5f, 0.25f)) },
            hover  = { textColor = Color.white, background = Solid(new Color(0.3f, 0.65f, 0.35f)) }
        };
        stylesReady = true;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
