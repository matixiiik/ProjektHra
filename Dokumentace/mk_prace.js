// Generuje Rocnikova_prace_Posledni_majak.docx
const fs = require('fs');
const d = require('docx');
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle, AlignmentType, LevelFormat, HeadingLevel, TableOfContents, Footer, PageNumber, PageBreak } = d;
const FONT = 'Calibri';
const numbering = { config: [
  { reference: 'b', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 270 } } } }] },
  { reference: 'n', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 320 } } } }] },
] };

// [[text]] v textu = žlutě zvýrazněný placeholder k doplnění
function runs(t, o = {}) {
  return t.split(/(\[\[.*?\]\])/).filter(x => x).map(x =>
    x.startsWith('[[') ? new TextRun({ text: '[' + x.slice(2, -2) + ']', font: FONT, highlight: 'yellow', ...o })
                       : new TextRun({ text: x, font: FONT, ...o }));
}
const P = (t, o = {}) => new Paragraph({ alignment: AlignmentType.JUSTIFIED, spacing: { after: 120, line: 300 }, ...o, children: runs(t) });
const B = (t) => new Paragraph({ numbering: { reference: 'b', level: 0 }, spacing: { after: 50, line: 280 }, children: runs(t) });
const N = (t) => new Paragraph({ numbering: { reference: 'n', level: 0 }, spacing: { after: 50, line: 280 }, children: runs(t) });
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, pageBreakBefore: true, spacing: { before: 0, after: 200 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 32, color: '1F4E79' })] });
const H1n = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 0, after: 200 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 32, color: '1F4E79' })] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, keepNext: true, spacing: { before: 240, after: 100 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 26, color: '1F4E79' })] });
const H3 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_3, keepNext: true, spacing: { before: 160, after: 80 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 23 })] });
const CAP = (t) => new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 60, after: 160 }, children: [new TextRun({ text: t, font: FONT, italics: true, size: 19, color: '555555' })] });
const FIG = (t) => new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 120, after: 20 }, children: [new TextRun({ text: '[' + t + ']', font: FONT, highlight: 'yellow', size: 20 })] });
const bd = { style: BorderStyle.SINGLE, size: 4, color: '999999' };
const borders = { top: bd, bottom: bd, left: bd, right: bd };
function table(widths, rows, size = 19) {
  const total = widths.reduce((a, b) => a + b, 0);
  return new Table({
    width: { size: total, type: WidthType.DXA }, columnWidths: widths,
    rows: rows.map((r, ri) => new TableRow({ tableHeader: ri === 0, cantSplit: true, children: r.map((c, ci) => new TableCell({
      borders, width: { size: widths[ci], type: WidthType.DXA }, margins: { top: 40, bottom: 40, left: 80, right: 80 },
      shading: ri === 0 ? { type: ShadingType.CLEAR, fill: 'D9E2F3', color: 'auto' } : undefined,
      children: [new Paragraph({ spacing: { after: 20 }, children: runs(c, { size, bold: ri === 0 }) })] })) })) });
}
const spacer = () => new Paragraph({ spacing: { after: 120 }, children: [] });
const W = 9600; // šířka textu (A4, okraje 1134 + 1134)

// ───────── Titulní strana ─────────
const title = [
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 600, after: 200 }, children: runs('[[NÁZEV ŠKOLY]]', { size: 26 }) }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1800 }, children: runs('Obor: [[OBOR]]', { size: 22, color: '555555' }) }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: 'ROČNÍKOVÁ PRÁCE – IT PROJEKT', font: FONT, size: 26, color: '555555' })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'Poslední maják', font: FONT, bold: true, size: 72, color: '1F4E79' })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 2200 }, children: [new TextRun({ text: '3D hra o plavbě po nekonečném oceánu (Unity, C#)', font: FONT, size: 28 })] }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Autor: Martin Štěpánek', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Třída: [[TŘÍDA]]', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Vedoucí práce: Kristina Sedeke', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Školní rok: 2026/2027', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Repozitář: https://github.com/matixiiik/ProjektHra [[po přesunu na školní účet upravit odkaz]]', { size: 22 }) }),
];

// ───────── Anotace ─────────
const anot = [
  H1('Anotace'),
  P('Práce popisuje návrh a realizaci 3D hry Poslední maják vytvořené v enginu Unity 6 v jazyce C#. Hráč pluje po prakticky nekonečném procedurálně generovaném oceánu, rybaří, těží poklady z vraků, bojuje s piráty a nepřátelskými ostrovy, v majácích nakupuje vylepšení lodi a plní úkoly. Postupně odemyká krátký příběh o ztraceném bratrovi starého námořníka, který vrcholí volbou se dvěma konci. Hra podporuje lokální split-screen pro dva hráče, uložení do tří slotů a česko-anglickou lokalizaci.'),
  P('Dokumentace shrnuje motivaci a cíle, použité technologie, návrh architektury a datového modelu, popis nejdůležitějších částí implementace (generování světa, ukládání, ekonomika, souboje, příběh), způsob testování, licenční a právní stránku a vlastní hodnocení výsledku.'),
  P('Klíčová slova: Unity, C#, procedurální generování, herní architektura, ukládání dat, split-screen, JSON.'),
  new Paragraph({ spacing: { before: 300, after: 120 }, children: [new TextRun({ text: 'Annotation', font: FONT, bold: true, size: 26, color: '1F4E79' })] }),
  P('[[Anglická anotace – doplnit, 3–5 vět, překlad české anotace.]]'),
  new Paragraph({ spacing: { before: 300, after: 120 }, children: [new TextRun({ text: 'Prohlášení', font: FONT, bold: true, size: 26, color: '1F4E79' })] }),
  P('Prohlašuji, že jsem tuto práci vypracoval samostatně a uvedl jsem všechny použité zdroje. Při vývoji jsem využíval AI asistenta Claude Code (Anthropic), způsob a rozsah jeho použití je popsán v kapitole 6.4. Návrh hry, rozhodnutí o rozsahu a architektuře, zadávání úkolů, testování a kontrola výsledného kódu jsou mou prací. Externí grafické podklady jsou uvedeny v kapitole 6.2.'),
  P('V [[MÍSTO]] dne [[DATUM]]                                                          Podpis: ______________'),
];

// ───────── Obsah ─────────
const toc = [
  H1('Obsah'),
  new TableOfContents('Obsah', { hyperlink: true, headingStyleRange: '1-2' }),
  P('[[Po otevření ve Wordu klikni pravým tlačítkem na obsah → Aktualizovat pole → Celá tabulka.]]'),
];

// ───────── 1 Úvod ─────────
const k1 = [
  H1('1 Úvod'),
  H2('1.1 Motivace'),
  P('Hry jsou pro mě nejpřirozenější způsob, jak si vyzkoušet celý vývojový cyklus softwaru: má jasného uživatele (hráče), spoustu vzájemně propojených částí (svět, ovládání, uživatelské rozhraní, uložení dat, zvuk) a výsledek se dá ukázat i člověku, který programování nerozumí. Téma plavby po moři jsem zvolil proto, že umožňuje jednoduchý, ale zajímavý herní princip – objevování, sbírání a postupné zlepšování lodi – a zároveň dobře funguje jako technická úloha: svět je nekonečný, a proto se musí generovat za běhu.'),
  H2('1.2 Cíl práce'),
  P('Cílem je vytvořit hratelnou 3D hru v Unity, která:'),
  B('má nekonečný, průběžně generovaný a ukládaný svět,'),
  B('nabízí smyčku „rybařit / těžit → prodat → vylepšit loď → plout dál“ doplněnou souboji a příběhem,'),
  B('je srozumitelná bez tutoriálu, funguje offline a je hratelná i ve dvou na jednom počítači,'),
  B('má čitelný, okomentovaný a obhajitelný zdrojový kód a kompletní dokumentaci.'),
  H2('1.3 Cílová skupina a přínos'),
  P('Hra je určena hráčům přibližně 12–25 let, kteří mají rádi klidné hry s postupným zlepšováním a krátké herní sezení, a dvojicím kamarádů či spolužáků, kteří si chtějí zahrát na jednom počítači. Přínosem pro hráče je uvolnění bez časového tlaku, jasný dlouhodobý cíl a bez nutnosti registrace či připojení k internetu. Přínosem pro autora je ucelená ukázka práce na softwarovém díle od návrhu po nasazení.'),
  P('Očekávané bariéry cílové skupiny: nároky 3D grafiky na slabší počítače, potřeba srozumitelného ovládání bez tutoriálu a konkurence hotových her. Odpovědí je optimalizace výkonu (kapitola 4.8), jednoduché ovládání (kapitola 3.3) a příběhová linka jako odlišení.'),
  H2('1.4 Rozsah a hranice projektu (scope)'),
  table([2600, 7000], [
    ['Součástí projektu (ANO)', 'Mimo rozsah (NE)'],
    ['Nekonečný generovaný svět, ostrovy, maják', 'Síťový multiplayer přes internet (jen naplánován)'],
    ['Rybaření, těžba vraků, obchody, úkoly', 'Mobilní platformy a konzole'],
    ['Souboje s piráty a nepřátelskými ostrovy', 'Vlastní 3D modely a animace (použity CC0 balíčky)'],
    ['Příběh se třemi mega ostrovy a dvěma konci', 'Databáze a server (data se ukládají lokálně)'],
    ['Lokální split-screen pro 2 hráče', 'Vlastní herní engine'],
    ['Uložení do 3 slotů, lokalizace CZ/EN', 'Komerční vydání (zvažováno v kapitole 3.6)'],
  ]),
  spacer(),
  H2('1.5 Struktura práce'),
  P('Kapitola 2 shrnuje použité technologie a teoretické pojmy. Kapitola 3 obsahuje analýzu požadavků, návrh architektury, datový model a business úvahu. Kapitola 4 popisuje realizaci nejdůležitějších částí hry. Kapitola 5 se věnuje testování a nasazení, kapitola 6 licencím a právním otázkám a kapitola 7 hodnotí splnění cílů.'),
];

// ───────── 2 Teoretická část ─────────
const k2 = [
  H1('2 Teoretická část'),
  H2('2.1 Herní engine Unity'),
  P('Unity je multiplatformní herní engine společnosti Unity Technologies. Aplikace se v něm skládá ze scén (Scene), které obsahují objekty (GameObject). Chování objektům dodávají komponenty, nejčastěji skripty v jazyce C# odvozené od třídy MonoBehaviour. Engine sám volá jejich životní cyklové metody – Awake a Start při vzniku, Update a LateUpdate každý snímek, OnEnable a OnDisable při zapnutí a vypnutí. Projekt používá Unity 6 (verze 6000.3.10f1) [1].'),
  H2('2.2 Universal Render Pipeline'),
  P('Vykreslování zajišťuje Universal Render Pipeline (URP), zjednodušená renderovací pipeline vhodná pro širokou škálu hardwaru od mobilů po PC. Oproti starší vestavěné pipeline nabízí lepší výkon při vyšší kvalitě a nastavení kvality lze řídit přes assety. Projekt používá URP ve verzi 17.3.0 [2].'),
  H2('2.3 Jazyk C#'),
  P('C# je objektově orientovaný jazyk vyvíjený společností Microsoft [3]. V práci se využívají třídy, rozhraní, výčtové typy (enum), kolekce (List, Dictionary), delegáty a události (event) a korutiny (iterátory s příkazem yield return, které Unity spouští postupně přes více snímků).'),
  H2('2.4 Procedurální generování'),
  P('Procedurální generování je vytváření obsahu algoritmem místo ručního modelování. Výhodou je téměř neomezená velikost světa při malé velikosti dat; nevýhodou nutnost obsah deterministicky reprodukovat nebo ukládat. V práci se používá deterministické generování založené na souřadnicích dlaždic a náhodě s pevným semínkem, aby se ostrov po opětovné návštěvě vygeneroval stejně.'),
  H2('2.5 Ukládání dat a formát JSON'),
  P('JSON je textový formát pro výměnu dat založený na dvojicích klíč–hodnota. Unity nabízí třídu JsonUtility, která převádí serializovatelné třídy na JSON a zpět [4]. Má omezení: neumí slovníky (Dictionary), pole s hodnotou null ani polymorfismus, proto je v práci použita vlastní serializovatelná varianta slovníku. Soubory se ukládají do složky Application.persistentDataPath, která je pro každou aplikaci na daném systému samostatná.'),
  H2('2.6 Návrhové principy použité v projektu'),
  B('Jediný zdroj pravdy: veškerý ukládaný stav je v jednom objektu GameData, ke kterému se přistupuje přes GameSession.'),
  B('Singleton: třída s jedinou instancí a statickým přístupem – používá se střídmě jen tam, kde je na scénu právě jeden objekt.'),
  B('Události (Observer): změna světa vyvolá událost OnWorldChanged, na kterou reagují HUD a minimapa, aniž by o sobě zdroj věděl.'),
  B('Líná inicializace a odložené zápisy: obsah se generuje a ukládá až když je potřeba, aby se zabránilo zásekům.'),
  H2('2.7 Uživatelské rozhraní: IMGUI a uGUI'),
  P('Unity nabízí více systémů UI. IMGUI (metoda OnGUI) je jednoduchý, kódem řízený systém vhodný pro menu a dialogy; uGUI (Canvas) je objektový systém vhodný pro HUD. Hra kombinuje oba: IMGUI pro menu, obchody, dialogy a hádanku, uGUI pro HUD s minimapou a hotbarem.'),
];

// ───────── 3 Analýza a návrh ─────────
const k3 = [
  H1('3 Analýza a návrh'),
  H2('3.1 Požadavky'),
  H3('Funkční požadavky'),
  table([900, 8700], [
    ['ID', 'Požadavek'],
    ['F1', 'Hráč ovládá loď volným pohybem podle kamery a může vystoupit na břeh pěšky.'],
    ['F2', 'Svět je nekonečný a generuje se kolem hráče; ostrovy mají molo, maják a dekoraci.'],
    ['F3', 'Hráč rybaří a těží poklady z vraků, kořist prodává ve výkupně majáku.'],
    ['F4', 'V majáku lze koupit vylepšení lodi, střelivo, zbraň a přijímat úkoly.'],
    ['F5', 'Piráti a nepřátelské ostrovy útočí na hráče, hráč může střílet; poražením získá odměnu.'],
    ['F6', 'Hra obsahuje příběhovou linku se třemi mega ostrovy a dvěma konci.'],
    ['F7', 'Hru lze uložit do tří slotů a znovu načíst; ukládání nesmí způsobovat viditelné záseky.'],
    ['F8', 'Podpora lokálního split-screenu pro dva hráče s odděleným stavem.'],
    ['F9', 'Menu a všechny texty jsou dostupné česky i anglicky.'],
  ]),
  spacer(),
  H3('Nefunkční požadavky'),
  B('Plynulost: cílem je stabilních [[DOPLNIT např. 60]] snímků/s na běžném notebooku.'),
  B('Čitelnost kódu: české komentáře, žádný chytrý trik bez vysvětlení, jednoduché řešení před složitým.'),
  B('Offline provoz bez externích služeb.'),
  B('Zpětná kompatibilita ukládání: přidání nového pole nesmí rozbít starý save.'),
  H2('3.2 Uživatelské scénáře (Use Cases)'),
  P('Hlavními aktéry jsou Hráč 1 a Hráč 2 (lokální), případně Hra jako systém (např. spawn pirátů).'),
  FIG('Obrázek 1 – Use Case diagram (UC1–UC7) – DOPLNIT nakreslený diagram'),
  table([700, 2500, 6400], [
    ['ID', 'Název', 'Stručný průběh'],
    ['UC1', 'Plout a objevovat', 'Hráč jede lodí; svět se dogeneruje; na minimapě se odkrývá oblast.'],
    ['UC2', 'Rybařit / těžit', 'Hráč zastaví u ryby nebo vraku, drží mezerník, po dokončení přibude kořist.'],
    ['UC3', 'Prodat a nakoupit', 'U ostrova vystoupí, vejde do majáku, prodá kořist, koupí vylepšení.'],
    ['UC4', 'Bojovat', 'Pirátská loď zaútočí, hráč mění náměr a střílí; při zásahu ubývá zdraví.'],
    ['UC5', 'Plnit úkol', 'U pultu si vezme úkol, splní jej a u pultu vybere odměnu.'],
    ['UC6', 'Postupovat v příběhu', 'Mluví s dědou, doplouvá na mega ostrov, řeší hádanku, čte vzkaz, volí konec.'],
    ['UC7', 'Uložit a pokračovat', 'V menu zvolí slot; hra se průběžně ukládá a při dalším spuštění načte.'],
  ]),
  spacer(),
  H2('3.3 Návrh ovládání a rozhraní (wireframy)'),
  P('Ovládání je navrženo tak, aby ho šlo pochopit bez tutoriálu: pohyb klávesami WASD, jedna „akční“ klávesa E pro nástup, výstup a vstup do budov a mezerník pro práci (rybaření, těžba). Druhý hráč má ekvivalentní ovládání na šipkách a numerické klávesnici.'),
  table([3400, 3100, 3100], [
    ['Akce', 'Hráč 1', 'Hráč 2'],
    ['Pohyb', 'W A S D', 'šipky'],
    ['Nástup / výstup, vstup do budovy', 'E', 'Numpad 1'],
    ['Rybařit / těžit', 'Mezerník', 'Numpad 0'],
    ['Střelba', 'Levé tlačítko myši', 'Numpad *'],
    ['Oprava lodě', 'R', 'Numpad /'],
    ['Velká mapa', 'M', 'Numpad 2'],
    ['Pauza', 'Esc', 'Numpad Enter'],
  ]),
  spacer(),
  FIG('Obrázek 2 – Wireframe / snímek: hlavní menu se sloty – DOPLNIT (např. Assets/Screenshots)'),
  FIG('Obrázek 3 – Wireframe / snímek: HUD s minimapou, zdravím a hotbarem – DOPLNIT'),
  FIG('Obrázek 4 – Wireframe / snímek: obchod s vylepšeními v majáku – DOPLNIT'),
  H2('3.4 Architektura systému'),
  P('Hra je jedna klientská aplikace bez serveru. Architektura je vrstvená: nahoře prezentace (HUD, menu, obchody), uprostřed herní logika (svět, hráč, souboje, příběh) a dole stav a perzistence. Klíčovou zásadou je, že stav hry žije na jednom místě.'),
  FIG('Obrázek 5 – Schéma architektury – DOPLNIT nakreslený diagram podle tabulky níže'),
  table([2200, 4300, 3100], [
    ['Vrstva', 'Hlavní třídy', 'Odpovědnost'],
    ['Prezentace', 'HUDCounter, MinimapUIRenderer, MainMenuManager, PauseMenu, ShopUI, MapScreen, DeathScreen', 'Vykreslení stavu a vstup uživatele do menu'],
    ['Herní logika', 'GridManager, PlayerController, CombatDirector, PirateShip, HostileIslandCannon, StoryNpc, RivalNpc, MegaIslandMarker', 'Pravidla hry, generování světa, souboje, příběh'],
    ['Lighthouse', 'LighthouseManager, LighthouseInterior, UpgradeShopManager, QuestShopManager', 'Obchody a výkup ve vnitřní scéně'],
    ['Stav a data', 'GameSession, GameData, TileData, EconomyConfig', 'Jediný zdroj pravdy, konstanty ekonomiky'],
    ['Perzistence', 'SaveManager', 'Zápis a čtení JSON, 3 sloty, zápis na pozadí'],
    ['Podpora', 'Loc, SoundManager, GameConsole, MultiplayerManager', 'Lokalizace, zvuk, vývojářská konzole, split-screen'],
  ]),
  spacer(),
  P('Hra má dvě scény: SampleScene (oceán, ostrovy, souboje) a LighthouseInterior (obchody). Objekt GameSession přežívá přechod mezi scénami (DontDestroyOnLoad), takže obě scény sdílejí totožná data.'),
  H2('3.5 Datový model'),
  P('Protože hra nepoužívá databázi, roli datového modelu (ERD) plní třídní model ukládaného stavu. Kořenem je třída GameData, která se serializuje do souboru save_N.json.'),
  FIG('Obrázek 6 – Třídní diagram GameData – DOPLNIT (GameData 1—* TileStatus; GameData 1—1 PlayerData P1/P2)'),
  table([2600, 2200, 4800], [
    ['Skupina dat', 'Příklad polí', 'Poznámka'],
    ['Svět', 'tileData (slovník „x,y“ → TileStatus), hostileIslands, clearedIslands, openedChests', 'Ukládají se jen vygenerované/navštívené dlaždice'],
    ['Hráč 1 a 2', 'pozice, mince, ryby, poklady, zdraví lodě a hráče, náboje, úkol, upgrady', 'Druhý hráč má pole s prefixem player2'],
    ['Souboj', 'ammo, pirateKills, hostileIslands', ''],
    ['Příběh', 'storyStep, storyDone, storyEnding, megaQuest, hasHistoricalTreasure', 'Postup po jednotlivých mega ostrovech'],
  ]),
  spacer(),
  P('Typ dlaždice TileType je výčet (Empty, Water, Water_Fish, Treasure, Harbor, Pier, UpgradeShop, QuestShop, Lighthouse, Chest, MegaIsland). Jeho hodnoty se ukládají jako celá čísla, proto se nikdy nesmí přečíslovat – starý save by pak obsahoval jiné dlaždice. Hodnoty UpgradeShop a QuestShop jsou zděděné ze starší verze hry a zůstávají kvůli kompatibilitě.'),
  H2('3.6 Business model a PR potenciál'),
  B('Distribuce: bezplatně na itch.io jako portfolio; případně nízká jednorázová cena (cca 3–5 €) na Steamu po dopracování.'),
  B('Komunita: krátká videa (YouTube/TikTok), sdílení sběratelských „map“ a náhodně generovaných ostrovů, možnost modifikací přes vývojářskou konzoli.'),
  B('Open-source: zdrojový kód pod licencí MIT, grafika CC0 – hra může sloužit jako výukový příklad.'),
  B('Konkurence: klidné exploration/collector hry; odlišením je nekonečný generovaný svět, split-screen na jednom PC a příběhová linka. [[Doplnit 2–3 konkrétní srovnávané hry a v čem se liší.]]'),
  H2('3.7 Produkční postup (pipeline)'),
  P('Vývoj probíhal iterativně: nápad → prototyp funkce → integrace → ruční ověření v editoru → úklid kódu a komentářů → commit. Verzování zajišťuje Git a GitHub, každá větší změna prochází větví a pull requestem do větve main. Složková struktura repozitáře odpovídá fázím: Assets/Scripts (kód), Assets/Scenes, Assets/Resources (modely načítané za běhu), Assets/Materials, složka .claude (pracovní poznámky a plány) a složka Dokumentace (tato práce).'),
];

// ───────── 4 Realizace ─────────
const k4 = [
  H1('4 Realizace'),
  P('Zdrojový kód tvoří přibližně [[60]] skriptů a zhruba 18 000 řádků C# včetně komentářů. Nejdůležitější části jsou popsány níže.'),
  H2('4.1 Svět z dlaždic (GridManager)'),
  P('Svět je nekonečná čtvercová mřížka dlaždic. Do paměti a do savu se dostane jen to, co bylo vygenerováno nebo navštíveno. Aktivní okolí hráče má velikost 28 × 28 dlaždic. Klíčem slovníku je řetězec „x,y“ (JsonUtility neumí slovníky, proto je použita vlastní třída SerializableDictionary).'),
  P('Moře samo dlaždice nemá. Místo tisíců objektů se používá jedna poloprůhledná plocha (OceanSurface), členité mořské dno (SeaFloor) a obloha s mraky (SkyClouds), které jedou za hráčem. 3D objekty tak mají jen ostrovy, ryby a vraky. Toto rozhodnutí je hlavním důvodem, proč hra běží plynule i v nekonečném světě.'),
  H2('4.2 Ostrovy'),
  P('Ostrov je organický útvar, ne čtverec. Algoritmus StampOrganicLand nejprve vytvoří pevné jádro o velikosti cca 7–10 dlaždic, pak k němu náhodně přidává okolní dlaždice a nakonec zaplní zálivy. Ostrovy vznikají na mřížce každých 40 dlaždic s 30% pravděpodobností a s minimálním rozestupem 200. Každý má molo (dvoupolíčkový výběžek do vody), maják 2 × 2 (nesmí ostrov rozdělit ani stát u mola) a s 25% šancí bednu. Kolem startovního ostrova je klidová zóna bez nepřátel.'),
  P('Hladký terén ostrova je generovaný mesh: IslandTerrain.Build vytvoří písek, BuildGrass trávu a BuildShallow průsvitný pás mělké vody. Pláž se svažuje pod hladinu na hloubku DEEP_Y, na kterou navazuje mořské dno, aby mezi nimi nevznikla viditelná hrana. Meshe trávy a mělčiny se staví v korutině o snímek či dva později než písek, což rozkládá zátěž a zabraňuje sekání při objevení ostrova.'),
  FIG('Obrázek 7 – Snímek ostrova s molem a majákem – DOPLNIT'),
  H2('4.3 Hráč a loď (PlayerController)'),
  P('Pohyb je volný podle směru kamery (ne skákání po políčkách). Pozice na mřížce (GridX, GridY) se dopočítává z pozice objektu a slouží pro generování světa, mlhu a interakce. Hráč může loď zaparkovat u mola – loď zůstane plavat a při nasednutí se odstraní. Zdraví lodě a hráče jsou oddělené: při zničení lodě hráč plave, při ztrátě vlastního zdraví nastává smrt se ztrátou kořisti, ale ne mincí.'),
  P('Hodnoty vlastní každému hráči (mince, náboje, zdraví…) se čtou a zapisují přes vlastnosti, které podle indexu hráče směrují do polí GameData prvního nebo druhého hráče. Tím se stejná logika používá pro oba hráče bez duplikace.'),
  H2('4.4 Ukládání (SaveManager, GameSession)'),
  P('Objekt GameSession drží aktuální GameData a přežívá přechod mezi scénami. SaveManager ukládá JSON do souborů save_0.json až save_2.json v Application.persistentDataPath. Save má několik MB, proto se zápis neprovádí při každé změně (což způsobovalo sekání zhruba půl sekundy při každé ryby): GridManager.Save() jen nastaví příznak „špinavý“ a skutečný zápis proběhne nejvýš jednou za 8 sekund, při ukončení hry, ztrátě okna a na požádání. Zápis běží na pozadí přes dočasný soubor a záměnu (File.Replace), takže poškození při vypnutí uprostřed zápisu nezničí předchozí uložení. Pro náhledy slotů v menu se čtou jen tři čísla (PeekSlotSummary), ne celý soubor.'),
  P('Kompatibilita: nové pole ve třídě GameData se ve starém savu načte jako 0, false nebo prázdný seznam, s čímž kód počítá.'),
  H2('4.5 Ekonomika a obchody'),
  P('Všechna čísla ekonomiky jsou na jednom místě ve třídě EconomyConfig, aby se dala snadno ladit: jedna ryba = 1 mince, poklad = 5 mincí, loď malá 180, střední 450 a velká 1000 mincí. Nákupní ceny v obchodě se násobí koeficientem 0,8–1,2 podle polohy ostrova (deterministicky z pozice majáku), výkup je všude stejný. Obchody jsou ve vnitřní scéně majáku (LighthouseInterior) – výkupna, obchod s vylepšeními a s úkoly – a fungují v sólo i kooperativním režimu.'),
  H2('4.6 Souboje'),
  P('Třída CombatDirector řídí vznik pirátských lodí (malé, střední, velké s ukazatelem zdraví bosse) a děl nepřátelských ostrovů. Nepřátelský ostrov (zhruba 40 % ostrovů) má 1–3 děla a 1–3 hlídkové lodě. Vše se vytvoří, když je ostrov do 40 dlaždic, a uklidí, když hráč odpluje. Ostrov je „vyčištěný“ až po zničení posledního děla. Náměr střelby se řídí sklonem kamery, takže lze zasáhnout i dělo na věži.'),
  H2('4.7 Příběh'),
  P('Starý námořník (StoryNpc) sedí u startovního ostrova a vede hráče příběhem: nejprve musí koupit loď a přinést 1000 mincí a historický poklad, poté dostane souřadnice mega ostrova. Tři mega ostrovy se objevují postupně:'),
  N('Ostrov pirátů – opevnění s děly na věžích a strážemi; po poražení trezor s hádankou z ozubených kol (VaultMechanism).'),
  N('Hřbitov lodí – Bludný Holanďan, tři vykopávané části mapy z mělčiny a vzkaz v podpalubí.'),
  N('Konfrontace – bratrova loď a postava RivalNpc, dlouhý monolog a volba ušetřit / zabít, která vede ke dvěma konců.'),
  P('Postup je uložen v polích storyStep, megaQuest a storyEnding. Herní konzole obsahuje presety (příkaz story 1–10), které hráče přenesou na libovolný úsek příběhu se správnou výbavou, což výrazně zjednodušilo testování.'),
  H2('4.8 Výkon'),
  P('Klíčová opatření: moře jako jediná plocha místo dlaždic, líné generování okolí, rozložení stavby terénu do více snímků, odložený zápis savu na pozadí, static batching hradeb (spojení zhruba 100 dílů), statické seznamy (PlayerController.All, PirateShip.All) místo opakovaného FindObjectsByType v Update a odstranění zbytečných vyhledávání ve slovníku při generování dlaždic.'),
  P('[[Doplnit naměřené hodnoty před/po: FPS, čas GenerateRegion, velikost a doba zápisu savu – z Unity Profileru; pokud číslo neznáš, změř.]]'),
  H2('4.9 Lokální multiplayer'),
  P('Druhý hráč je kopie prvního (Instantiate), ze které se odstraní komponenty, které mají existovat jen jednou (kamera, HUD, minimapa, konzole, menu). První kamera zabírá levou polovinu obrazovky, druhá pravou. Ekonomiky jsou oddělené a v pauze lze mezi hráči převádět mince. Obchod, mapa, dialog nebo smrt jednoho hráče nezastaví druhého.'),
  H2('4.10 Lokalizace'),
  P('Třída Loc drží obě jazykové verze textu vedle sebe přímo v kódu: Loc.T("Nová hra", "New Game"). Jazyk se ukládá do PlayerPrefs (ne do savu). Text uložený v savu (např. popis úkolu) se do rozhraní složí znovu z dat, aby po přepnutí jazyka nezůstal starý.'),
  H2('4.11 Vývojářská konzole'),
  P('Konzole (klávesa zpětný apostrof) umožňuje cheaty pro testování: get money, tp x y, explore, locate a presety příběhu. Není přeložena, protože je určena jen vývojáři.'),
  H2('4.12 Kvalita kódu a konvence'),
  B('Žádný namespace, komentáře a herní texty česky, hlavičky souborů a sekcí oddělené čarou.'),
  B('Preferována delší, jasná řešení před „chytrými triky“ – kód musí být obhajitelný.'),
  B('Singletony jen pro případy „jeden objekt na scénu“; pro „všechny objekty typu X“ statický seznam přes OnEnable / OnDisable.'),
  B('Každý nový viditelný text prochází Loc.T; každá nová per-hráč hodnota přes vlastnost, HUD a obchody.'),
];

// ───────── 5 Testování a nasazení ─────────
const k5 = [
  H1('5 Testování a nasazení'),
  H2('5.1 Strategie testování'),
  P('Hra nemá sadu automatických testů; testování je z velké části ruční v editoru, což u herní logiky závislé na scéně a vstupu odpovídá běžné praxi malého projektu. Testování bylo doplněno těmito nástroji:'),
  B('Kontrola kompilace bez otevřeného Unity (skript compile-check.sh) po každé změně logiky.'),
  B('Vývojářská konzole a presety příběhu k rychlému dosažení libovolného stavu.'),
  B('Kontrolní seznam pro herní test (soubor .claude/playtest-checklist.md).'),
  B('Ověřování za běhu (Play Mode) včetně sledování Console a Profileru.'),
  B('Testy hraničních případů uložení: poškozený nebo starý save, ztráta okna při zápisu.'),
  H2('5.2 Testovací protokol'),
  table([700, 3600, 3300, 2000], [
    ['ID', 'Scénář', 'Očekávaný výsledek', 'Výsledek'],
    ['T1', 'Nová hra → doplout k ostrovu, vystoupit, vejít do majáku', 'Načte se scéna majáku, obchody fungují', '[[OK/chyba]]'],
    ['T2', 'Rybařit 10×, prodat, zkontrolovat mince', 'Mince odpovídají počtu ryb', '[[OK/chyba]]'],
    ['T3', 'Koupit vylepšení rychlosti', 'Loď zrychlí, mince ubydou', '[[OK/chyba]]'],
    ['T4', 'Zavřít hru uprostřed hraní, znovu spustit, Pokračovat', 'Stav je obnoven', '[[OK/chyba]]'],
    ['T5', 'Přepnout jazyk v menu a pauze', 'Změní se HUD, obchody i úkoly', '[[OK/chyba]]'],
    ['T6', 'Split-screen: oba hráči nakupují současně', 'Ekonomiky se neovlivňují', '[[OK/chyba]]'],
    ['T7', 'Nepřátelský ostrov: zničit všechna děla', 'Odměna, hlídky se uvolní', '[[OK/chyba]]'],
    ['T8', 'Zničit loď, potom hráče', 'Plavání, smrtící obrazovka, respawn', '[[OK/chyba]]'],
    ['T9', 'story 3 (preset) až konec příběhu', 'Všechny tři mega ostrovy, oba konce', '[[OK/chyba]]'],
    ['T10', 'Načíst starý save po změně datového modelu', 'Načte se bez chyby', '[[OK/chyba]]'],
  ]),
  spacer(),
  H2('5.3 Nalezené a opravené chyby'),
  P('[[Doplnit 4–6 nejzajímavějších chyb z historie gitu a poznámek (soubory .claude/handoff.md, git log): popis chyby, příčina, oprava. Příklady: záseky při ukládání, nesprávná hranice mega ostrova na nepravidelném pobřeží, viditelná hrana mezi pláží a mořským dnem.]]'),
  H2('5.4 Build a distribuce'),
  P('[[Zatím není hotovo.]] Plánovaný postup: v Unity zvolit File → Build Profiles → Windows, seřadit obě scény v Build Settings (SampleScene, LighthouseInterior), nastavit Company Name a Product Name, vytvořit build (.exe) a zabalit do ZIP. Release se vytvoří na GitHubu jako Release Candidate a poté finální verze; k němu se přiloží uživatelská příručka.'),
  H2('5.5 Uživatelská příručka'),
  P('[[Zatím není hotovo.]] Příručka bude obsahovat: požadavky na počítač, spuštění, ovládání pro oba hráče (tabulka v kapitole 3.3), popis herní smyčky, popis obchodů, příběhu a řešení častých problémů (výkon, kde jsou uložené hry).'),
];

// ───────── 6 Právní rámec ─────────
const k6 = [
  H1('6 Právní rámec'),
  H2('6.1 Licence vlastního kódu'),
  P('Vlastní zdrojový kód je [[po rozhodnutí doplnit: navrhována licence MIT]] [5]. Licence MIT je krátká a permisivní: umožňuje kód používat, kopírovat, upravovat i šířit, pokud zůstane zachováno oznámení o autorských právech a licence. Vybral jsem ji, protože je jednoduchá, srozumitelná a nebrání dalšímu využití kódu jako výukového příkladu. Alternativou je GPL (vyžaduje, aby odvozená díla zůstala také otevřená) nebo Apache 2.0 (obsahuje navíc ustanovení o patentech).'),
  H2('6.2 Licence třetích stran'),
  table([2700, 2500, 1600, 2800], [
    ['Podklad', 'Autor / zdroj', 'Licence', 'Použití ve hře'],
    ['Pirate Kit', 'Kenney (kenney.nl)', 'CC0 1.0', 'Lodě, děla, dekorace ostrovů'],
    ['Mini Characters', 'Kenney (kenney.nl)', 'CC0 1.0', 'Postavy hráče a NPC'],
    ['Shark, Animated Fish Pack', 'Quaternius (quaternius.com)', 'CC0 1.0', 'Mořská obluda, ryby'],
    ['Ghost Ship', 'Quaternius', 'CC0 1.0', 'Bludný Holanďan'],
    ['Zvuky', 'Vlastní (generované kódem)', 'Vlastní dílo', 'SoundManager vytváří zvuky sinusovkami a šumem'],
  ]),
  spacer(),
  P('Licence CC0 (Creative Commons Zero) znamená vzdání se autorských práv v maximálním rozsahu povoleném zákonem [6]; použití včetně komerčního nevyžaduje uvedení autora, přesto jsou autoři v práci i v README uvedeni. Licenční soubory jsou přiloženy ve složkách s modely v Assets/Resources. [[Ověřit, že se ve hře nenachází další cizí podklady (textury, fonty) a doplnit je do tabulky.]]'),
  H2('6.3 Autorské právo a GDPR'),
  P('Podle autorského zákona [7] je autorem díla fyzická osoba, která je vytvořila; autorská práva k mému kódu a návrhu hry mi proto náleží, s výhradou pravidel školy pro ročníkové práce [[ověřit u vedoucí]]. Hra neshromažďuje ani nepřenáší žádné osobní údaje, nevyžaduje registraci a pracuje výhradně offline; ukládá pouze herní stav do lokální složky. Ustanovení nařízení o ochraně osobních údajů [8] se tedy na provoz hry prakticky nevztahují.'),
  H2('6.4 Použití AI asistenta při vývoji'),
  P('Při vývoji jsem využíval AI asistenta Claude Code (Anthropic) v terminálu a v desktopové aplikaci. Používal jsem ho jako pomocníka při psaní a refaktoringu kódu, hledání chyb a psaní pracovní dokumentace; rozhodnutí o zadání, návrhu hry, rozsahu, architektuře a kontrola výstupu jsou moje. Vzniklý kód jsem četl, spouštěl, testoval v editoru a upravoval. Použití AI je transparentně uvedeno v README repozitáře a v prohlášení autora.'),
  P('[[Důležité: ověřit u vedoucí a ve školním řádu, zda je takový rozsah použití AI přípustný a jak ho formálně uvést (příloha, prohlášení). Doplnit konkrétní příklady, které části jsem sám navrhl a které jsem konzultoval s asistentem, a připravit se na otázky komise, jak funguje např. generování ostrovů nebo ukládání – musím to umět vysvětlit vlastními slovy.]]'),
];

// ───────── 7 Závěr ─────────
const k7 = [
  H1('7 Závěr'),
  P('Cílem práce bylo vytvořit hratelnou 3D hru v Unity s nekonečným světem, ekonomikou, souboji, příběhem a dokumentací. Výsledkem je hra Poslední maják, která obsahuje všechny plánované funkce z kapitoly 1.4 a je hratelná od nové hry po oba konce příběhu i ve dvou hráčích na jednom počítači. Byla splněna i technická kritéria: svět je nekonečný a ukládaný, ukládání nezpůsobuje záseky, kód je okomentovaný a členěný.'),
  H2('7.1 Omezení'),
  B('Není hotový síťový multiplayer (naplánován v souboru .claude/network-multiplayer-plan.md).'),
  B('Chybí sada automatických testů; testování je ruční.'),
  B('Zvuk je jednoduchý (generovaný kódem), bez hudby.'),
  B('Hra je testována na omezeném počtu zařízení: [[doplnit]].'),
  H2('7.2 Možná rozšíření'),
  B('Síťový multiplayer přes internet (hostování jedním hráčem).'),
  B('Další mega ostrovy, sezónní události, více typů lodí a zbraní.'),
  B('Hudba a bohatší zvukový design.'),
  B('Automatické testy (Unity Test Framework) pro logiku ekonomiky a ukládání.'),
  H2('7.3 Osobní přínos'),
  P('[[Doplnit vlastními slovy 5–8 vět: co jsem se naučil (architektura, práce s Gitem, generování světa, optimalizace, dokumentace), co bylo nejtěžší, co bych udělal jinak.]]'),
];

// ───────── Zdroje a přílohy ─────────
const src = [
  H1n('Seznam použitých zdrojů'),
  P('[[Zdroje jsou uvedeny podle ISO 690. U internetových zdrojů doplnit datum citace (poslední přístup) po ověření, že odkaz funguje.]]', { alignment: AlignmentType.LEFT }),
  ...[
    '[1] UNITY TECHNOLOGIES. Unity Manual. Online. Dostupné z: https://docs.unity3d.com/Manual/. [cit. [[DATUM]]].',
    '[2] UNITY TECHNOLOGIES. Universal Render Pipeline. Online. Dostupné z: https://docs.unity3d.com/Manual/urp/urp-introduction.html. [cit. [[DATUM]]].',
    '[3] MICROSOFT. C# documentation. Online. Dostupné z: https://learn.microsoft.com/dotnet/csharp/. [cit. [[DATUM]]].',
    '[4] UNITY TECHNOLOGIES. JsonUtility. Scripting API. Online. Dostupné z: https://docs.unity3d.com/ScriptReference/JsonUtility.html. [cit. [[DATUM]]].',
    '[5] OPEN SOURCE INITIATIVE. The MIT License. Online. Dostupné z: https://opensource.org/license/mit. [cit. [[DATUM]]].',
    '[6] CREATIVE COMMONS. CC0 1.0 Universal. Online. Dostupné z: https://creativecommons.org/publicdomain/zero/1.0/. [cit. [[DATUM]]].',
    '[7] Zákon č. 121/2000 Sb., o právu autorském, o právech souvisejících s právem autorským a o změně některých zákonů (autorský zákon), ve znění pozdějších předpisů.',
    '[8] Nařízení Evropského parlamentu a Rady (EU) 2016/679 ze dne 27. dubna 2016 o ochraně fyzických osob v souvislosti se zpracováním osobních údajů (GDPR).',
    '[9] KENNEY. Pirate Kit, Mini Characters. Online. Dostupné z: https://kenney.nl. [cit. [[DATUM]]].',
    '[10] QUATERNIUS. Ultimate Animated Fish Pack, Shark, Ghost Ship. Online. Dostupné z: https://quaternius.com. [cit. [[DATUM]]].',
    '[11] ŠTĚPÁNEK, Martin. ProjektHra. GitHub. Online. Dostupné z: https://github.com/matixiiik/ProjektHra. [cit. [[DATUM]]].',
  ].map(t => new Paragraph({ spacing: { after: 80 }, indent: { left: 540, hanging: 540 }, children: runs(t) })),
  H2('Přílohy'),
  B('Příloha A – Zdrojový kód (odkaz na GitHub a tag vydání).'),
  B('Příloha B – Uživatelská příručka (PDF). [[doplnit]]'),
  B('Příloha C – Testovací protokol s daty testování. [[doplnit]]'),
  B('Příloha D – Snímky obrazovky hry. [[doplnit]]'),
  B('Příloha E – Project Charter a prezentace (odevzdané 11. 11.). [[doplnit]]'),
];

const doc = new Document({
  creator: 'Martin Štěpánek', title: 'Poslední maják – ročníková práce',
  styles: { default: { document: { run: { font: FONT, size: 22 } } } },
  numbering,
  features: { updateFields: true },
  sections: [
    { properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } }, children: title },
    { properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 18 })] })] }) },
      children: [...anot, ...toc, ...k1, ...k2, ...k3, ...k4, ...k5, ...k6, ...k7, ...src] },
  ],
});
Packer.toBuffer(doc).then(b => { fs.writeFileSync('Rocnikova_prace_Posledni_majak.docx', b); console.log('OK'); });
