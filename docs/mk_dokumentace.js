// ─────────────────────────────────────────────────────────────────────────────
//  mk_dokumentace.js — generuje Dokumentace_The_Last_Beacon.docx
//  Dokumentace maturitního IT projektu. Struktura kapitol přesně podle školního
//  dokumentu „Maturitní práce – Projekt 2026/2027: IT projekt“, kap. 6
//  (Úvod → Analýza → Návrh → Implementace → Testování → Nasazení →
//  Vyhodnocení → Závěr → Zdroje), viz docs/Pozadavky_ITG_2026_2027.md.
//
//  [[text]] v textu = žlutě zvýrazněný údaj, který musí doplnit autor sám.
//  Spuštění:  NODE_PATH=<složka s node_modules (docx)> node mk_dokumentace.js
//  PDF:       otevřít ve Wordu → Aktualizovat obsah → Uložit jako PDF
//             (nebo docs/export_pdf.ps1, který to udělá automaticky).
// ─────────────────────────────────────────────────────────────────────────────
const fs = require('fs');
const path = require('path');
const d = require('docx');
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle, AlignmentType, LevelFormat, HeadingLevel, TableOfContents, Footer, PageNumber } = d;
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
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, pageBreakBefore: true, keepNext: true, spacing: { before: 0, after: 200 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 32, color: '1F4E79' })] });
// Nadpis 1. úrovně bez zalomení stránky (používá uživatelská příručka, mk_prirucka.js).
const H1n = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, keepNext: true, spacing: { before: 0, after: 200 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 32, color: '1F4E79' })] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, keepNext: true, spacing: { before: 240, after: 100 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 26, color: '1F4E79' })] });
const H3 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_3, keepNext: true, spacing: { before: 160, after: 80 }, children: [new TextRun({ text: t, font: FONT, bold: true, size: 23 })] });
const CAP = (t) => new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 60, after: 160 }, children: [new TextRun({ text: t, font: FONT, italics: true, size: 19, color: '555555' })] });
const CODE = (t) => new Paragraph({ spacing: { after: 0, line: 240 }, shading: { type: ShadingType.CLEAR, fill: 'F2F2F2', color: 'auto' }, children: [new TextRun({ text: t, font: 'Consolas', size: 18 })] });
// Obrázek ze složky obrazky/ (PNG/JPG) natažený na šířku `w` px, s popiskem pod ním.
function IMG(file, w, caption) {
  const p = path.join(__dirname, 'obrazky', file);
  if (!fs.existsSync(p)) return [P('[[' + caption + ' – chybí obrazky/' + file + ']]')];
  const buf = fs.readFileSync(p);
  let pw, ph, type;
  if (file.endsWith('.png')) { pw = buf.readUInt32BE(16); ph = buf.readUInt32BE(20); type = 'png'; }
  else { // JPG: najdi SOF0/SOF2 marker s rozměry
    type = 'jpg';
    for (let i = 2; i < buf.length;) {
      const m = buf[i + 1], len = buf.readUInt16BE(i + 2);
      if (m === 0xC0 || m === 0xC2) { ph = buf.readUInt16BE(i + 5); pw = buf.readUInt16BE(i + 7); break; }
      i += 2 + len;
    }
  }
  return [
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 120, after: 20 }, keepNext: true,
      children: [new d.ImageRun({ type, data: buf, transformation: { width: w, height: Math.round(w * ph / pw) },
        altText: { title: caption, description: caption, name: file } })] }),
    CAP(caption),
  ];
}
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

// ───────── Titulní strana ─────────
const title = [
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 400, after: 60 }, children: runs('1. IT Gymnázium, s.r.o.', { size: 28, bold: true }) }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 60 }, children: runs('K Cihelně 129, 190 15 Praha 9 – Satalice', { size: 22, color: '555555' }) }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1500 }, children: runs('Obor 79-41-K/41 Gymnázium', { size: 22, color: '555555' }) }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: 'MATURITNÍ PRÁCE – IT PROJEKT', font: FONT, size: 26, color: '555555' })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'The Last Beacon', font: FONT, bold: true, size: 60, color: '1F4E79' })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 60 }, children: [new TextRun({ text: '3D hra o plavbě po nekonečném oceánu (Unity, C#)', font: FONT, size: 28 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1800 }, children: [new TextRun({ text: 'Dokumentace projektu', font: FONT, italics: true, size: 24, color: '555555' })] }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Autor: Martin Štěpánek', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Třída: [[TŘÍDA]]', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Vedoucí práce: Kristina Sedeke', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Oponent: [[OPONENT]]', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Školní rok: 2026/2027', { size: 24 }) }),
  new Paragraph({ spacing: { after: 60 }, children: runs('Repozitář: https://github.com/matixiiik/ProjektHra', { size: 22 }) }),
];

// ───────── Anotace, prohlášení ─────────
const anot = [
  H1('Anotace'),
  P('Projekt The Last Beacon je 3D hra vytvořená v enginu Unity 6 v jazyce C#. Hráč pluje po prakticky nekonečném procedurálně generovaném oceánu, rybaří, těží poklady z vraků, bojuje s piráty a nepřátelskými ostrovy, v majácích nakupuje vylepšení lodi, plní úkoly a sbírá pirátskou kořist. Postupně odemyká krátký příběh o ztraceném bratrovi starého námořníka, který vrcholí volbou se dvěma konci. Hra podporuje lokální split-screen pro dva hráče, ukládání do tří slotů a česko-anglickou lokalizaci.'),
  P('Dokumentace popisuje řešený problém a cíle, odborná východiska a porovnání technologií, návrh architektury a datového modelu, implementaci nejdůležitějších částí (generování světa, ukládání, ekonomika, souboje, příběh), testování, nasazení a vyhodnocení výsledku.'),
  P('Klíčová slova: Unity, C#, procedurální generování, herní architektura, ukládání dat, JSON, split-screen.'),
  new Paragraph({ spacing: { before: 300, after: 120 }, children: [new TextRun({ text: 'Annotation', font: FONT, bold: true, size: 26, color: '1F4E79' })] }),
  P('The Last Beacon is a 3D game made in the Unity 6 engine in C#. The player sails across a practically endless, procedurally generated ocean, fishes, salvages treasure from wrecks, fights pirates and hostile islands, buys boat upgrades in lighthouses, takes on jobs and collects pirate loot. Step by step they uncover a short story about the old sailor’s lost brother, which ends with a choice between two endings. The game supports local split-screen for two players, saving into three slots and Czech and English localisation.'),
  P('The documentation describes the problem and goals, the technical background and comparison of technologies, the architecture and data model, the implementation of the key parts (world generation, saving, economy, combat, story), testing, deployment and an evaluation of the result.'),
  P('Keywords: Unity, C#, procedural generation, game architecture, data persistence, JSON, split-screen.'),
  new Paragraph({ spacing: { before: 300, after: 120 }, children: [new TextRun({ text: 'Prohlášení', font: FONT, bold: true, size: 26, color: '1F4E79' })] }),
  P('Prohlašuji, že jsem tento maturitní projekt vypracoval samostatně a uvedl jsem všechny použité zdroje a podklady třetích stran. Při vývoji jsem jako pracovní nástroj využíval AI asistenta; způsob a rozsah jeho použití popisuji v kapitole 4.13. Zadání, návrh hry, rozhodnutí o rozsahu a architektuře, testování a kontrola výsledného kódu jsou mou prací a celý projekt dokážu vysvětlit a obhájit.'),
  P('V Praze dne [[DATUM]]                                                                    Podpis: ______________'),
];

// ───────── Obsah ─────────
const toc = [
  H1('Obsah'),
  new TableOfContents('Obsah', { hyperlink: true, headingStyleRange: '1-2' }),
];

// ───────── 1 Úvod ─────────
const k1 = [
  H1('1 Úvod'),
  H2('1.1 Představení díla'),
  P('The Last Beacon („Poslední maják“) je klidná 3D hra o plavbě po moři pro Windows. Svět tvoří nekonečný oceán s ostrovy, které se generují kolem hráče. Na každém ostrově stojí maják, uvnitř kterého je výkupna, obchod s vylepšeními a obchod s úkoly. Hráč začíná s veslicí, postupně si kupuje lepší lodě a vybavení a plní příběh, který ho provede třemi velkými „mega ostrovy“ až k závěrečné volbě.'),
  ...IMG('shot_ostrov.jpg', 470, 'Obrázek 1 – Startovní ostrov s majákem a molem'),
  H2('1.2 Řešený problém a motivace'),
  P('Odborným problémem projektu je vytvořit herní svět, který je prakticky nekonečný, a přesto hra běží plynule a dokáže svůj stav spolehlivě uložit a znovu načíst. Ruční modelování takového světa není možné, proto se musí generovat algoritmicky za běhu, deterministicky (stejné místo se vygeneruje vždy stejně) a ukládat se smí jen to, co hráč změnil nebo navštívil. K tomu přistupuje návrh herní architektury s mnoha vzájemně propojenými systémy (pohyb, ekonomika, souboje, příběh, uživatelské rozhraní, ukládání) tak, aby kód zůstal čitelný a obhajitelný.'),
  P('Hry jsou pro mě nejpřirozenější způsob, jak si vyzkoušet celý vývojový cyklus softwaru: mají jasného uživatele, spoustu propojených částí a výsledek se dá předvést i člověku, který programování nerozumí. Téma plavby po moři umožňuje jednoduchý, ale zajímavý herní princip – objevování, sbírání a postupné zlepšování lodi.'),
  H2('1.3 Cíl projektu'),
  P('Cílem je vytvořit hratelnou 3D hru v Unity, která:'),
  B('má nekonečný, průběžně generovaný a ukládaný svět,'),
  B('nabízí herní smyčku „rybařit / těžit → prodat → vylepšit loď → plout dál“ doplněnou souboji, úkoly a příběhem,'),
  B('je srozumitelná bez tutoriálu, funguje offline a je hratelná i ve dvou na jednom počítači,'),
  B('běží plynule (cíl 60 snímků za sekundu na běžném počítači) a ukládání nezpůsobuje záseky,'),
  B('má čitelný, okomentovaný a obhajitelný zdrojový kód, automatické testy a kompletní dokumentaci.'),
  H2('1.4 Cílová skupina a způsob využití'),
  P('Hra je určena hráčům přibližně 12–25 let, kteří mají rádi klidné hry s postupným zlepšováním a krátké herní sezení, a dvojicím kamarádů či spolužáků, kteří si chtějí zahrát na jednom počítači. Hra nevyžaduje registraci ani připojení k internetu. Zdrojový kód pod licencí MIT může sloužit i jako výukový příklad herní architektury v Unity.'),
  H2('1.5 Rozsah projektu'),
  table([4800, 4800], [
    ['Součástí projektu', 'Mimo rozsah'],
    ['Nekonečný generovaný svět, ostrovy, majáky', 'Síťový multiplayer přes internet (jen naplánován)'],
    ['Rybaření, těžba vraků, obchody, úkoly, pirátská kořist', 'Mobilní platformy a konzole'],
    ['Souboje s piráty a nepřátelskými ostrovy', 'Vlastní 3D modely a animace (použity CC0 balíčky)'],
    ['Příběh se třemi mega ostrovy a dvěma konci', 'Databáze a server (data se ukládají lokálně)'],
    ['Lokální split-screen pro 2 hráče', 'Vlastní herní engine'],
    ['Ukládání do 3 slotů, lokalizace CZ/EN', 'Komerční vydání'],
  ]),
  spacer(),
];

// ───────── 2 Analýza problému a odborná východiska ─────────
const k2 = [
  H1('2 Analýza problému a odborná východiska'),
  H2('2.1 Současný stav a existující řešení'),
  P('Klidné hry o plavbě a sbírání existují; nejbližší jsou Raft [12], Dredge [13] a Sea of Thieves [14]. Žádná z nich ale nespojuje všechny tři vlastnosti, na které cílí tento projekt: nekonečný generovaný svět, hru ve dvou na jednom počítači a krátký uzavřený příběh bez nutnosti připojení k internetu.'),
  table([1900, 2600, 2600, 2500], [
    ['Hra', 'Svět', 'Hra více hráčů', 'Odlišení The Last Beacon'],
    ['Raft', 'Generovaný oceán, přežití na voru', 'Online kooperace', 'Bez hladu a žízně, klidnější, krátký příběh'],
    ['Dredge', 'Pevná ručně stavěná mapa', 'Jen jeden hráč', 'Nekonečný svět, souboje, hra ve dvou'],
    ['Sea of Thieves', 'Velký pevný svět', 'Jen online', 'Celá hra offline, split-screen, malý rozsah'],
  ]),
  spacer(),
  H2('2.2 Odborné a technologické principy'),
  H3('Herní engine a životní cyklus objektů'),
  P('Herní engine poskytuje vykreslování, fyziku, vstup, zvuk a správu scény. V Unity se aplikace skládá ze scén (Scene) s objekty (GameObject), kterým chování dodávají komponenty – skripty v C# odvozené od třídy MonoBehaviour. Engine sám volá jejich metody životního cyklu: Awake a Start při vzniku, Update a LateUpdate každý snímek, OnEnable a OnDisable při zapnutí a vypnutí, OnGUI pro okamžité kreslení uživatelského rozhraní [1].'),
  H3('Procedurální generování'),
  P('Procedurální generování je vytváření obsahu algoritmem místo ručního modelování. Výhodou je téměř neomezená velikost světa při malém objemu dat; nevýhodou nutnost obsah deterministicky reprodukovat nebo ukládat. Projekt používá deterministické generování z celočíselných souřadnic dlaždic (hash souřadnic jako semínko náhody) a Perlinův šum [15] pro tvar pobřeží a mořského dna. Díky tomu se ostrov po opětovné návštěvě vygeneruje stejně a ukládat stačí jen změny.'),
  H3('Serializace a ukládání dat'),
  P('Herní stav je strom objektů, který se musí převést na text nebo bajty (serializace). JSON je textový formát založený na dvojicích klíč–hodnota; je čitelný člověkem a snadno se ladí. Třída JsonUtility v Unity umí převádět serializovatelné třídy na JSON a zpět [4], neumí ale slovníky, hodnoty null v kolekcích ani polymorfismus. Při každém zápisu hrozí poškození souboru, pokud se program ukončí uprostřed; proto se používá zápis do dočasného souboru a jeho atomická záměna.'),
  H3('Vykreslování a výkon'),
  P('Plynulost hry určuje čas jednoho snímku: pro 60 snímků za sekundu je to zhruba 16,7 ms na veškerou logiku i vykreslení. Drahé jsou zejména velké počty objektů na scéně, alokace paměti (spouští garbage collector) a synchronní práce s diskem v hlavním vlákně. Universal Render Pipeline (URP) je zjednodušená renderovací pipeline Unity vhodná pro širokou škálu hardwaru [2].'),
  H2('2.3 Možné technologie a jejich porovnání'),
  table([1700, 2700, 2600, 2600], [
    ['Kritérium', 'Unity 6 (C#)', 'Godot 4 (GDScript/C#)', 'Unreal Engine 5 (C++)'],
    ['Náročnost na začátečníka', 'střední, obrovské množství návodů', 'nízká, menší komunita', 'vysoká (C++, Blueprinty)'],
    ['Jazyk', 'C# – silně typovaný, blízký Javě', 'GDScript (vlastní) nebo C#', 'C++ nebo vizuální skriptování'],
    ['3D a výkon na slabším PC', 'dobrý (URP)', 'dobrý, slabší nástroje pro 3D', 'výborná grafika, náročný na hardware'],
    ['Volné 3D assety', 'velmi mnoho (Kenney, Quaternius)', 'stejné formáty (FBX/GLB)', 'Marketplace, větší soubory'],
    ['Licence', 'zdarma pro osobní použití', 'MIT, zcela zdarma', 'zdarma do limitu příjmů'],
  ]),
  spacer(),
  P('Pro uživatelské rozhraní nabízí Unity dva systémy: IMGUI (metoda OnGUI, kód kreslí rozhraní každý snímek) a uGUI (objekty na Canvasu). Pro ukládání připadal v úvahu JSON, binární formát nebo vestavěná databáze SQLite.'),
  H2('2.4 Důvody výběru'),
  B('Unity a C#: C# znám z výuky, Unity má nejvíc dokumentace a návodů a URP poběží i na integrované grafice. Godot by byl lehčí, ale pro 3D má méně nástrojů; Unreal je pro jednoho autora a slabší počítače zbytečně těžký.'),
  B('IMGUI pro menu, obchody a dialogy (rychle se píše a mění kódem), uGUI pro HUD s minimapou a hotbarem (vykresluje se trvale a výkonněji).'),
  B('JSON přes JsonUtility: žádná další knihovna, čitelný soubor, který jde při ladění otevřít v editoru. Databáze by byla pro jeden lokální soubor zbytečná.'),
  B('Git a GitHub pro verzování, Unity Test Framework (NUnit) pro automatické testy.'),
];

// ───────── 3 Návrh řešení ─────────
const k3 = [
  H1('3 Návrh řešení'),
  H2('3.1 Požadavky'),
  H3('Funkční požadavky'),
  table([900, 8700], [
    ['ID', 'Požadavek'],
    ['F1', 'Hráč ovládá loď volným pohybem podle kamery a může vystoupit na břeh pěšky.'],
    ['F2', 'Svět je nekonečný a generuje se kolem hráče; ostrovy mají molo, maják a dekoraci.'],
    ['F3', 'Hráč rybaří a těží poklady z vraků, kořist prodává ve výkupně majáku.'],
    ['F4', 'V majáku lze koupit vylepšení lodi, střelivo, zbraň a mapu a brát úkoly s odměnami.'],
    ['F5', 'Piráti a nepřátelské ostrovy útočí na hráče, hráč může střílet; poražením získá odměnu a s 25% šancí kořist.'],
    ['F6', 'Hra obsahuje příběhovou linku se třemi mega ostrovy a dvěma konci; postup je vidět v deníku.'],
    ['F7', 'Hru lze uložit do tří slotů a znovu načíst; ukládání nesmí způsobovat viditelné záseky.'],
    ['F8', 'Podpora lokálního split-screenu pro dva hráče s odděleným stavem.'],
    ['F9', 'Menu a všechny texty jsou dostupné česky i anglicky.'],
  ]),
  spacer(),
  H3('Nefunkční požadavky'),
  B('Plynulost: cílem je stabilních 60 snímků/s na běžném počítači.'),
  B('Čitelnost kódu: české komentáře, jednoduché řešení před „chytrým trikem“.'),
  B('Offline provoz bez externích služeb a bez sběru osobních údajů.'),
  B('Zpětná kompatibilita ukládání: přidání nového pole nesmí rozbít starší uloženou hru.'),
  H2('3.2 Uživatelské scénáře (use cases)'),
  P('Hlavními aktéry jsou Hráč 1 a Hráč 2 (lokálně), případně hra jako systém (např. vznik pirátů).'),
  ...IMG('usecase.png', 470, 'Obrázek 2 – Diagram případů užití (UC1–UC7)'),
  table([700, 2500, 6400], [
    ['ID', 'Název', 'Stručný průběh'],
    ['UC1', 'Plout a objevovat', 'Hráč jede lodí; svět se dogeneruje; na minimapě se odkrývá okolí.'],
    ['UC2', 'Rybařit / těžit', 'Hráč zastaví u ryb nebo vraku, drží mezerník, po dokončení přibude kořist.'],
    ['UC3', 'Prodat a nakoupit', 'U ostrova vystoupí, vejde do majáku, prodá kořist, koupí vylepšení.'],
    ['UC4', 'Bojovat', 'Pirátská loď zaútočí, hráč míří kamerou a střílí; při zásahu ubývá zdraví.'],
    ['UC5', 'Plnit úkol', 'U pultu si vezme úkol (ryby, poklady, piráti, děla, bedny, kořist), splní ho a vyzvedne odměnu.'],
    ['UC6', 'Postupovat v příběhu', 'Mluví s dědou, dopluje na mega ostrov, vyřeší hádanku, přečte dopis, zvolí konec.'],
    ['UC7', 'Uložit a pokračovat', 'V menu zvolí slot; hra se průběžně ukládá a při dalším spuštění načte.'],
  ]),
  spacer(),
  H2('3.3 Architektura systému'),
  P('Hra je jedna klientská aplikace bez serveru. Architektura je vrstvená: nahoře prezentace (HUD, menu, obchody), uprostřed herní logika (svět, hráč, souboje, příběh) a dole stav a perzistence. Hlavní zásadou je, že veškerý ukládaný stav žije na jednom místě – v objektu GameData, ke kterému se přistupuje přes GameSession.'),
  ...IMG('architektura.png', 470, 'Obrázek 3 – Schéma architektury (vrstvy a scény)'),
  table([2000, 4500, 3100], [
    ['Vrstva', 'Hlavní třídy', 'Odpovědnost'],
    ['Prezentace', 'HUDCounter, MinimapUIRenderer, MainMenuManager, PauseMenu, ShopUI, MapScreen, JournalScreen, LetterScreen, DeathScreen', 'Vykreslení stavu a vstup uživatele do menu'],
    ['Herní logika', 'GridManager, PlayerController, CombatDirector, PirateShip, HostileIslandCannon, StoryNpc, RivalNpc, MegaIslandMarker', 'Pravidla hry, generování světa, souboje, příběh'],
    ['Maják', 'LighthouseManager, LighthouseInterior, UpgradeShopManager, QuestShopManager', 'Obchody, úkoly a výkup ve vnitřní scéně'],
    ['Stav a data', 'GameSession, GameData, PlayerState, TileData, EconomyConfig, LootItems', 'Jediný zdroj pravdy, konstanty ekonomiky, kořist'],
    ['Perzistence', 'SaveManager', 'Zápis a čtení JSON, 3 sloty, migrace starých savů'],
    ['Podpora', 'Loc, SoundManager, GameConsole, MultiplayerManager, HudSkin', 'Lokalizace, zvuk, vývojářská konzole, split-screen, grafika UI'],
  ]),
  spacer(),
  P('Hra má dvě scény: SampleScene (oceán, ostrovy, souboje) a LighthouseInterior (vnitřek majáku s obchody). Objekt GameSession přežívá přechod mezi scénami (DontDestroyOnLoad), takže obě scény pracují s totožnými daty. Komunikace mezi částmi probíhá přes události: změna světa vyvolá událost OnWorldChanged, na kterou reagují HUD a minimapa, aniž by o nich zdroj změny věděl.'),
  H2('3.4 Datový model'),
  P('Hra nepoužívá databázi, roli datového modelu (ERD) proto plní třídní model ukládaného stavu. Kořenem je třída GameData, která se serializuje do souboru save_N.json.'),
  ...IMG('datovy_model.png', 470, 'Obrázek 4 – Třídní diagram ukládaného stavu (GameData, PlayerState, TileStatus)'),
  table([2300, 3500, 3800], [
    ['Skupina dat', 'Příklad polí', 'Poznámka'],
    ['Svět', 'tileData (slovník „x,y“ → TileStatus), hostileIslands, clearedIslands', 'Ukládají se jen vygenerované nebo navštívené dlaždice'],
    ['Hráč 1 a 2', 'pozice, mince, ryby, poklady, zdraví, náboje, úkol, vylepšení, loot', 'Pole players[2] třídy PlayerState'],
    ['Úkol', 'ActiveQuest: questType, target, progress, reward, rewardItem', 'Typy a odměny jsou čísla – nikdy se nepřečíslovávají'],
    ['Příběh', 'storyStep, megaIndex, megaTask, storyDone, storyEnding', 'Postup po jednotlivých mega ostrovech'],
  ]),
  spacer(),
  P('Výčtové hodnoty (typ dlaždice TileType, typ úkolu, index kořisti) se ukládají jako celá čísla, proto se nikdy nesmí přečíslovat – starý save by jinak obsahoval jiné dlaždice nebo předměty. Nové položky se přidávají vždy na konec.'),
  H2('3.5 Návrh uživatelského rozhraní a ovládání'),
  P('Ovládání je navrženo tak, aby ho šlo pochopit bez tutoriálu: pohyb WASD ve směru kamery, jedna akční klávesa E pro nástup, výstup a vstup do budov a mezerník pro práci. Druhý hráč má stejné ovládání na šipkách a numerické klávesnici. Všechna „papírová“ okna (dopis, obchody, mapa, deník) sdílí stejný vzhled pergamenu, aby k sobě rozhraní graficky sedělo; HUD má tmavé dřevěné panely s mosazným lemem.'),
  table([3400, 3100, 3100], [
    ['Akce', 'Hráč 1', 'Hráč 2'],
    ['Pohyb', 'W A S D', 'šipky'],
    ['Nástup / výstup, vstup do budovy, rozhovor', 'E', 'Numpad 1'],
    ['Rybařit / těžit / kopat', 'Mezerník', 'Numpad 0'],
    ['Střelba', 'levé tlačítko myši', 'Numpad *'],
    ['Oprava lodě', 'R', 'Numpad /'],
    ['Velká mapa', 'M', 'Numpad 2'],
    ['Pauza (deník, jazyk, uložení)', 'Esc', 'Numpad Enter'],
  ]),
  spacer(),
  ...IMG('hud.png', 420, 'Obrázek 5 – HUD: minimapa, zdraví, mince, úkol a hotbar'),
  ...IMG('obchod_pergamen.jpg', 440, 'Obrázek 6 – Obchod v majáku (papírová okna mají jednotný vzhled pergamenu)'),
];

// ───────── 4 Implementace ─────────
const k4 = [
  H1('4 Implementace'),
  H2('4.1 Použité technologie a nástroje'),
  table([2800, 6800], [
    ['Nástroj', 'Použití'],
    ['Unity 6000.3.10f1', 'Herní engine, editor scén, build pro Windows'],
    ['Universal Render Pipeline 17.3', 'Vykreslování'],
    ['C# (.NET Standard 2.1)', 'Veškerá herní logika – 60 skriptů, zhruba 19 000 řádků včetně komentářů'],
    ['Unity Test Framework (NUnit)', 'Automatické EditMode a PlayMode testy'],
    ['Git, GitHub', 'Verzování, větve a pull requesty (přes 20 sloučených PR)'],
    ['Visual Studio Code', 'Editor kódu'],
    ['MCP for Unity, Claude Code', 'Propojení AI asistenta s editorem Unity (čtení konzole, spouštění testů) – viz 4.13'],
    ['Python, Node.js', 'Generování diagramů a této dokumentace (složka docs)'],
  ]),
  spacer(),
  H2('4.2 Svět z dlaždic (GridManager)'),
  P('Svět je nekonečná čtvercová mřížka dlaždic. Do paměti a do savu se dostane jen to, co bylo vygenerováno nebo navštíveno; klíčem slovníku je text „x,y“ (JsonUtility neumí slovníky, proto vlastní třída SerializableDictionary). Aktivní okolí hráče má velikost 28 × 28 dlaždic a při pohybu se zpracovává jen nově odkrytý okraj, ne celý čtverec.'),
  P('Moře samo dlaždice nemá. Místo tisíců objektů je to jedna poloprůhledná plocha (OceanSurface), členité mořské dno (SeaFloor) a obloha s mraky (SkyClouds), které jedou za hráčem. 3D objekty mají jen ostrovy, ryby a vraky. Toto rozhodnutí je hlavním důvodem, proč hra běží plynule i v nekonečném světě. Barva dna se mění podle hloubky – u pláže navazuje na barvu písku a směrem do hloubky tmavne.'),
  H2('4.3 Ostrovy'),
  P('Ostrov je organický útvar, ne čtverec. Algoritmus StampOrganicLand vytvoří pevné jádro, náhodně k němu přidává okolní dlaždice a nakonec zaplní zálivy. Ostrovy vznikají na mřížce každých 40 dlaždic s 30% pravděpodobností a s minimálním rozestupem. Každý má molo, maják 2 × 2 (nesmí ostrov rozdělit ani stát u mola) a s 25% šancí bednu. Zhruba 40 % ostrovů je nepřátelských – mají děla a hlídkové lodě.'),
  P('Hladký terén ostrova je generovaný mesh: IslandTerrain.Build vytvoří písek, BuildGrass trávu a BuildShallow průsvitný pás mělké vody. Meshe trávy a mělčiny se staví v korutině o snímek či dva později než písek, což rozkládá zátěž a zabraňuje sekání při objevení ostrova.'),
  CODE('// GridManager – deterministická náhoda z pozice: stejné místo = stejný ostrov'),
  CODE('int h = unchecked(x * 73856093 ^ y * 19349663) & 0x7fffffff;'),
  CODE('bool maOstrov = (h % 100) < ISLAND_CHANCE_PERCENT;   // 30 %'),
  spacer(),
  H2('4.4 Hráč a loď (PlayerController)'),
  P('Pohyb je volný podle směru kamery. Pozice na mřížce se dopočítává z pozice objektu a slouží pro generování světa a interakce. Hráč může loď zaparkovat u mola – loď zůstane plavat a při nasednutí se odstraní. Pěšák smí vejít do mělké vody (brodí se a pak plave) a vylézt na libovolný břeh. Zdraví lodě a hráče jsou oddělená: při zničení lodě hráč plave, při ztrátě vlastního zdraví nastává smrt se ztrátou kořisti, ale ne mincí.'),
  P('Hodnoty vlastní každému hráči (mince, náboje, zdraví…) se čtou a zapisují přes vlastnosti, které podle indexu hráče směrují do prvku pole GameData.players. Původně měl druhý hráč zdvojená pole s prefixem player2; sjednocení do pole PlayerState[2] odstranilo duplicitní kód a při něm se našly tři skryté chyby (např. druhý hráč se za běhu vůbec neukládal).'),
  H2('4.5 Ukládání (SaveManager, GameSession)'),
  P('SaveManager ukládá JSON do souborů save_0.json až save_2.json v Application.persistentDataPath. Save má několik MB, proto se nezapisuje při každé změně (dřív to způsobovalo půlsekundové záseky): GridManager.Save() jen nastaví příznak a skutečný zápis proběhne nejvýš jednou za 8 sekund, při ukončení hry, ztrátě okna a na požádání. Zápis běží na pozadí přes dočasný soubor a záměnu (File.Replace), takže vypnutí uprostřed zápisu nezničí předchozí uložení.'),
  P('Změny struktury savu řeší číslo verze (saveVersion): při načtení SaveManager.ParseAndMigrate převede stará data na aktuální formát. Nová pole se ve starém savu načtou jako 0, false nebo prázdný seznam, s čímž kód počítá – například inventář kořisti se čte jen přes LootItems.Count, které chybějící položku vrací jako 0.'),
  H2('4.6 Ekonomika, obchody a úkoly'),
  P('Všechna čísla ekonomiky jsou na jednom místě ve třídě EconomyConfig: ryba 2 mince, poklad 5 mincí, lodě 180 / 450 / 1000 mincí. Nákupní ceny se na každém ostrově násobí koeficientem 0,8–1,2 (deterministicky z pozice majáku), výkup je všude stejný. Obchody jsou ve vnitřní scéně majáku a fungují v sólo i kooperativním režimu; kreslí je sdílená třída ShopUI, která drží vzhled pergamenu a styly na jednom místě.'),
  P('Úkoly mají šest typů: ulovit ryby, vytěžit poklady, potopit pirátské lodě, zničit děla nepřátelských ostrovů, otevřít bedny a získat pirátskou kořist. Hráč za úkol zaplatí zálohu a po splnění dostane její násobek; větší a bojové úkoly dávají navíc věcnou odměnu (vylepšení, munici, vzácnou kořist). Bojové úkoly se nabízí jen hráči s dělem na lodi. Postup všech typů posouvá jediná metoda ActiveQuest.AddProgress, kterou volají místa, kde se něco „počítá“.'),
  CODE('// ActiveQuest – jedna metoda pro všechny typy úkolů'),
  CODE('public void AddProgress(int type, int amount)'),
  CODE('{'),
  CODE('    if (!hasQuest || questType != type || amount <= 0) return;'),
  CODE('    progress = Mathf.Min(progress + amount, target);'),
  CODE('}'),
  spacer(),
  P('Po potopení piráta nebo poražení stráže padne s pravděpodobností 25 % předmět pirátské kořisti (rum, kompas, dalekohled, šavle, prsten, kalich). Předměty mají různou vzácnost (váhu) a cenu a prodávají se ve výkupně; drží se v „neviditelném inventáři“ PlayerState.loot.'),
  H2('4.7 Souboje'),
  P('Třída CombatDirector řídí vznik pirátských lodí tří velikostí a děl nepřátelských ostrovů. Nepřátelský ostrov má 1–3 děla a 1–3 hlídkové lodě, které vzniknou, když je ostrov blízko, a uklidí se, když hráč odpluje. Ostrov je „vyčištěný“ po zničení posledního děla. Náměr se řídí sklonem kamery, takže lze zasáhnout i dělo na věži hradeb. Hráč má kromě lodního děla i pěší zbraň s vlastní municí.'),
  ...IMG('shot_souboj.jpg', 420, 'Obrázek 7 – Souboj s pirátskou lodí'),
  H2('4.8 Příběh'),
  P('Starý námořník (StoryNpc) sedí u startovního ostrova a vede hráče příběhem: nejprve musí koupit loď a přinést 1000 mincí a historický poklad, pak dostane souřadnice prvního mega ostrova. Tři mega ostrovy se objevují postupně:'),
  N('Pirátský ostrov – opevnění s děly na věžích a strážemi; po poražení trezor s hádankou z ozubených kol (VaultMechanism) a dopis s matematickou hádankou na souřadnice dalšího ostrova.'),
  N('Hřbitov lodí – tři vykopávané kusy roztržené mapy, podpalubí vraku se vzkazem a útok Bludného Holanďana při odjezdu.'),
  N('Poslední ostrov – bratrova loď a postava RivalNpc, monolog a volba ušetřit / zabít, která vede ke dvěma koncům.'),
  P('Postup je uložen v polích storyStep, megaIndex, megaTask a storyEnding. Deník (JournalScreen) ukazuje název aktuální kapitoly, zápisky o tom, co hráč už dokázal, a na kliknutí nápovědu k aktuálnímu kroku. Vývojářská konzole obsahuje presety (příkaz story 1–10), které hráče přenesou na libovolný úsek příběhu se správnou výbavou – to výrazně zjednodušilo testování.'),
  ...IMG('shot_pevnost.jpg', 420, 'Obrázek 8 – Pirátský ostrov s hradbami'),
  ...IMG('mapa_pergamen.jpg', 420, 'Obrázek 9 – Velká mapa: souřadnice hráče, souřadnice pod kurzorem, cíl (hádanka z dopisu se řeší právě na ní)'),
  H2('4.9 Lokální multiplayer'),
  P('Druhý hráč je kopie prvního (Instantiate), ze které se odstraní komponenty, které mají existovat jen jednou (kamera, HUD, minimapa, konzole, menu). První kamera zabírá levou polovinu obrazovky, druhá pravou. Ekonomiky jsou oddělené a v pauze lze mezi hráči převádět mince. Obchod, mapa, dialog nebo smrt jednoho hráče nezastaví druhého.'),
  ...IMG('shot_splitscreen.jpg', 420, 'Obrázek 10 – Split-screen pro dva hráče'),
  H2('4.10 Lokalizace a vývojářská konzole'),
  P('Třída Loc drží obě jazykové verze textu vedle sebe přímo v kódu: Loc.T("Nová hra", "New Game"). Jazyk se ukládá do PlayerPrefs, ne do savu. Text uložený v savu (popis úkolu) se do rozhraní skládá znovu z dat, aby po přepnutí jazyka nezůstal starý. Konzole (klávesa zpětný apostrof) umožňuje cheaty pro testování (peníze, teleport, předměty, presety příběhu) a záměrně není přeložená.'),
  H2('4.11 Technické problémy a jejich řešení'),
  table([2100, 3900, 3600], [
    ['Problém', 'Příčina', 'Řešení'],
    ['Záseky při ukládání', 'Každá ryba či nákup synchronně zapsal 5 MB JSON (cca 800 ms)', 'Odložený zápis (max. 1× za 8 s) na pozadí přes dočasný soubor; kompaktní JSON (2 MB)'],
    ['Sekání v boji', 'Minimapa skládala řetězec klíče pro každý pixel; FindObjectsByType každý snímek', 'Cache klíčů, statický seznam PirateShip.All; alokace 231 → 26 KB/snímek, 49 → 64 snímků/s'],
    ['Ostrov bez hradeb', 'Trasování hranice umí jen jednu smyčku; 1 ze 40 tvarů měl dva laloky', 'SmoothMegaIslandShape (eroze, souvislé těleso, dilatace); ověřeno na 200 pozicích'],
    ['Druhý hráč se neukládal', 'Stav P2 se zapisoval jen při respawnu', 'Sjednocení do PlayerState[2] s migrací savu'],
    ['Migrace přeskočila starý save', 'JsonUtility u chybějícího pole saveVersion nechá výchozí (aktuální) verzi', 'ParseAndMigrate bere chybějící pole jako verzi 0; odhalil automatický test'],
    ['Chyba jen v buildu', 'Shader.Find vrátil v buildu null (shader nebyl zahrnut)', 'Shadery v Always Included Shaders; odhalil smoke test buildu'],
    ['Hrana mezi pláží a dnem', 'Pláž a mořské dno končily v jiné výšce', 'Výška dna u ostrova navazuje na konec pláže (DEEP_Y + Y_OFFSET)'],
  ]),
  spacer(),
  H2('4.12 Změny oproti původnímu návrhu'),
  B('Moře původně tvořily dlaždice s vlastními objekty; kvůli výkonu je to jedna plocha, dno a obloha jedoucí za hráčem.'),
  B('Obchody stály jako samostatné budovy na ostrovech; přesunuly se do vnitřní scény majáku (staré typy dlaždic zůstaly kvůli kompatibilitě savů).'),
  B('Pohyb skákal po políčkách; nahradil ho volný pohyb podle kamery.'),
  B('Zdvojená data hráčů (player2…) nahradilo pole PlayerState[2] s migrací savu.'),
  B('Úkoly byly jen „ulov / vytěž“; přibyly bojové úkoly, věcné odměny a pirátská kořist, aby se vyplatilo dělat i vedlejší úkoly.'),
  B('Síťový multiplayer z původních nápadů byl kvůli rozsahu odložen (plán je v repozitáři).'),
  H2('4.13 Použití umělé inteligence'),
  P('V souladu s pravidly školy uvádím, že jsem při vývoji významně využíval AI asistenta Claude Code (Anthropic) v terminálu a v desktopové aplikaci, propojeného s editorem Unity přes nástroj MCP for Unity. AI jsem používal jako pracovní nástroj na:'),
  B('psaní a úpravy kódu podle mého zadání a návrhu (nové funkce, refaktoring P1/P2, optimalizace),'),
  B('hledání příčin chyb (čtení konzole Unity, měření výkonu),'),
  B('psaní automatických testů a pracovní dokumentace, generování diagramů a této dokumentace,'),
  B('rešerši postupů a vysvětlování API Unity.'),
  P('Zadání, herní návrh (svět, ekonomika, příběh, postavy, hádanky), rozhodování o rozsahu a architektuře, testování v Unity a kontrola výstupů jsou moje. Každou změnu jsem procházel, spouštěl a ověřoval ve hře; nevhodná řešení jsem vracel s připomínkami. Kód je psán záměrně jednoduše a s českými komentáři, abych ho dokázal vysvětlit, a celý projekt jsem schopen obhájit. [[Doplnit vlastními slovy 2–3 konkrétní příklady: co jsem navrhl sám a co jsem s AI konzultoval.]]'),
  H2('4.14 Licence a podklady třetích stran'),
  table([2700, 2500, 1600, 2800], [
    ['Podklad', 'Autor / zdroj', 'Licence', 'Použití ve hře'],
    ['Pirate Kit', 'Kenney (kenney.nl)', 'CC0 1.0', 'Lodě, děla, hradby, dekorace ostrovů'],
    ['Mini Characters', 'Kenney (kenney.nl)', 'CC0 1.0', 'Postavy hráče a NPC'],
    ['Animated Fish Pack (žralok)', 'Quaternius', 'CC0 1.0', 'Mořská obluda'],
    ['Zvuky, textury UI', 'Vlastní (generované kódem)', 'Vlastní dílo', 'SoundManager, HudSkin'],
  ]),
  spacer(),
  P('Vlastní kód je pod licencí MIT [5] (soubor LICENSE). Licence CC0 [6] znamená vzdání se autorských práv; přesto jsou autoři uvedeni zde, v README a v souboru THIRD_PARTY_LICENSES.md. Ve hře nejsou žádné cizí fonty, hudba ani zvukové soubory. Hra nesbírá žádné osobní údaje, nevyžaduje registraci a pracuje offline, nařízení GDPR [8] se proto na její provoz prakticky nevztahuje.'),
  H2('4.15 Kvalita kódu a konvence'),
  B('Komentáře a herní texty česky, každý soubor má hlavičku s popisem, k čemu slouží.'),
  B('Žádný namespace, delší jasná řešení před „chytrými triky“ – kód musí být obhajitelný.'),
  B('Singletony jen pro případy „jeden objekt na scénu“ (GameSession, CombatDirector…); pro „všechny objekty typu X“ statický seznam přes OnEnable / OnDisable místo opakovaného vyhledávání.'),
  B('Každý viditelný text prochází Loc.T; každá hodnota hráče přes PlayerState; všechna čísla ekonomiky v EconomyConfig.'),
];

// ───────── 5 Testování ─────────
const k5 = [
  H1('5 Testování'),
  H2('5.1 Způsob testování'),
  P('Testování kombinuje automatické testy a ruční ověřování ve hře. Automatická sada (Unity Test Framework) obsahuje EditMode testy datových tříd a logiky bez scény (ekonomika, statistiky lodí, úkoly, kořist, migrace savů) a PlayMode testy kamery a vln. Herní logika závislá na scéně a vstupu se ověřuje ručně v editoru i v buildu. Doplňují je tyto nástroje:'),
  B('kontrola kompilace bez otevřeného Unity (skript compile-check.sh) po každé změně,'),
  B('vývojářská konzole a presety příběhu k rychlému dosažení libovolného stavu,'),
  B('kontrolní seznam pro herní test (.claude/playtest-checklist.md),'),
  B('Profiler a měření alokací při ladění výkonu, smoke test hotového buildu.'),
  H2('5.2 Automatické testy'),
  table([3200, 1000, 5400], [
    ['Sada', 'Testů', 'Co ověřuje'],
    ['BoatStatsTests', '3', 'Dělo, rychlost a bonusy podle úrovně lodi'],
    ['EconomyConfigTests', '5', 'Cenový násobič ostrova (0,8–1,2), zaokrouhlení cen'],
    ['GameDataTests', '4', 'Splnění a reset úkolu, mega quest'],
    ['LootAndQuestTests', '11', 'Kořist (přidání, prodej, šance na drop, starý save bez pole), postup úkolů jen správného typu'],
    ['SaveMigrationTests', '6', 'Převod starých savů na aktuální formát, save bez čísla verze'],
    ['PlayMode (kamera, vlny)', '4', 'Sledování hráče kamerou, výška vln'],
  ]),
  spacer(),
  P('Výsledek posledního běhu (26. 11. 2026, Unity 6000.3.10f1): EditMode 33/33, PlayMode 4/4 – vše prošlo.'),
  H2('5.3 Testovací scénáře a výsledky'),
  table([700, 3600, 3300, 2000], [
    ['ID', 'Scénář', 'Očekávaný výsledek', 'Výsledek'],
    ['T1', 'Nová hra → doplout k ostrovu, vystoupit, vejít do majáku', 'Načte se scéna majáku, obchody fungují', 'OK'],
    ['T2', 'Rybařit, prodat ryby, zkontrolovat mince', 'Mince odpovídají počtu ryb × cena', '[[ověřit ručně]]'],
    ['T3', 'Koupit vylepšení rychlosti', 'Loď zrychlí, mince ubydou', 'OK'],
    ['T4', 'Zavřít hru uprostřed hraní, znovu spustit, Pokračovat', 'Stav je obnoven', 'OK (i v buildu)'],
    ['T5', 'Přepnout jazyk v menu a pauze', 'Změní se HUD, obchody i úkoly', 'OK'],
    ['T6', 'Split-screen: oba hráči nakupují současně', 'Ekonomiky se neovlivňují', 'OK (převod mincí); hra dvou lidí [[ručně]]'],
    ['T7', 'Nepřátelský ostrov: zničit všechna děla', 'Odměna, hlídky se uvolní, úkol „děla“ se posune', '[[ověřit ručně]]'],
    ['T8', 'Zničit loď, potom hráče', 'Plavání, obrazovka smrti, respawn bez kořisti', 'OK'],
    ['T9', 'Presety story 1–10 až konec příběhu', 'Všechny tři mega ostrovy, oba konce', 'Částečně – [[ostrovy 2–3 a konce ručně]]'],
    ['T10', 'Načíst starý save po změně datového modelu', 'Načte se bez chyby', 'OK (automatické testy i živě)'],
    ['T11', 'Potopit piráty, prodat kořist ve výkupně', 'Kořist se objeví ve výkupně, prodá se', 'OK (26. 11.)'],
    ['T12', 'Vzít bojový úkol a splnit ho', 'Vyplatí se mince i věcná odměna', 'OK (26. 11.)'],
    ['T13', 'Otevřít mapu, deník, dopis, obchody (myš nad textem)', 'Pergamen, čitelný text, správná kapitola', 'OK (26. 11.)'],
  ]),
  spacer(),
  H2('5.4 Známé chyby a omezení'),
  B('Druhý hráč ve split-screenu byl testován kódem a jednou osobou na jedné klávesnici; dlouhá hra dvou lidí zatím neproběhla.'),
  B('Výkon byl měřen na jednom stolním PC v editoru; na slabších noteboocích ověřen není.'),
  B('Zvuk je jednoduchý (generovaný kódem), bez hudby.'),
];

// ───────── 6 Nasazení ─────────
const k6 = [
  H1('6 Nasazení'),
  H2('6.1 Prostředí'),
  P('Cílová platforma je Windows 10 nebo 11 (64 bit) s grafikou podporující DirectX 11. Hra běží i na integrované grafice; doporučeno je 8 GB RAM a zhruba 300 MB místa na disku. Nepotřebuje připojení k internetu ani instalaci dalších knihoven.'),
  H2('6.2 Sestavení (build)'),
  P('Windows build (Mono, 64 bit) vzniká v Unity přes File → Build Profiles z obou scén (SampleScene, LighthouseInterior). Shadery hledané v kódu přes Shader.Find musí být v Project Settings → Graphics → Always Included Shaders, jinak v buildu chybí. Build má přibližně 97 MB a po sestavení prochází smoke testem: spuštění, kontrola logu, načtení a migrace uložené hry.'),
  H2('6.3 Distribuce a instalace'),
  P('Hotový build se zabalí do ZIP a zveřejní jako Release na GitHubu spolu s uživatelskou příručkou. Instalace je jen rozbalení archivu a spuštění souboru The Last Beacon.exe. Uložené hry leží v %USERPROFILE%\\AppData\\LocalLow\\matixiiik\\The Last Beacon\\save_0.json až save_2.json.'),
  H2('6.4 Spuštění ze zdrojového kódu'),
  N('Naklonovat repozitář: git clone https://github.com/matixiiik/ProjektHra.git'),
  N('V Unity Hubu přidat složku projektu a otevřít ji ve verzi 6000.3.10f1.'),
  N('Otevřít scénu Assets/Scenes/SampleScene.unity a stisknout Play.'),
  N('Testy: Window → General → Test Runner → EditMode / PlayMode → Run All.'),
];

// ───────── 7 Vyhodnocení ─────────
const k7 = [
  H1('7 Vyhodnocení'),
  H2('7.1 Míra naplnění cíle'),
  table([4600, 1400, 3600], [
    ['Cíl', 'Splněno', 'Poznámka'],
    ['Nekonečný generovaný a ukládaný svět', 'ano', 'Ukládají se jen navštívené dlaždice'],
    ['Herní smyčka, souboje, úkoly, příběh', 'ano', 'Příběh dohratelný k oběma koncům'],
    ['Bez tutoriálu, offline, hra ve dvou', 'ano', 'Deník s nápovědou, split-screen'],
    ['Plynulost 60 snímků/s, ukládání bez záseků', 'ano', 'Naměřeno na PC; notebook neověřen'],
    ['Čitelný kód, testy, dokumentace', 'ano', 'České komentáře, automatické testy, tento dokument'],
  ]),
  spacer(),
  H2('7.2 Silné a slabé stránky'),
  H3('Silné stránky'),
  B('Nekonečný svět, který přesto běží plynule a ukládá se rychle.'),
  B('Jednotná architektura „jeden zdroj pravdy“ a datově řízená ekonomika v jednom souboru.'),
  B('Kompletní příběh s hádankami a dvěma konci; nástroje pro rychlé testování (presety).'),
  B('Zpětná kompatibilita savů ověřená automatickými testy.'),
  H3('Slabé stránky'),
  B('Velké třídy GridManager a PlayerController (přes 1900 řádků) by si zasloužily další rozdělení.'),
  B('Herní logika ve scéně nemá automatické testy, spoléhá na ruční testování.'),
  B('Grafika je složená z volných balíčků, bez vlastních modelů a animací.'),
  H2('7.3 Možnosti dalšího rozvoje'),
  B('Síťový multiplayer přes internet (hostování jedním hráčem; plán je v repozitáři).'),
  B('Další mega ostrovy, více druhů lodí a zbraní, sezónní události.'),
  B('Hudba a bohatší zvukový design.'),
  B('Rozdělení největších tříd a PlayMode testy herní logiky.'),
];

// ───────── 8 Závěr ─────────
const k8 = [
  H1('8 Závěr'),
  P('Cílem projektu bylo vytvořit hratelnou 3D hru v Unity s nekonečným světem, ekonomikou, souboji, příběhem a kompletní dokumentací. Výsledkem je hra The Last Beacon, která obsahuje všechny plánované funkce, je dohratelná od nové hry po oba konce příběhu a hratelná i ve dvou hráčích na jednom počítači. Svět je nekonečný a ukládaný, ukládání nezpůsobuje záseky a kód je členěný, okomentovaný a pokrytý automatickými testy v datové části.'),
  H2('Osobní přínos'),
  P('[[Doplnit vlastními slovy 5–8 vět: co jsem se naučil (architektura, Git, generování světa, optimalizace, dokumentace), co bylo nejtěžší, co bych příště udělal jinak.]]'),
];

// ───────── 9 Zdroje a přílohy ─────────
const src = [
  H1('9 Seznam použitých zdrojů'),
  P('Citace podle normy ČSN ISO 690. U online zdrojů je uvedeno datum citace.', { alignment: AlignmentType.LEFT }),
  ...[
    '[1] UNITY TECHNOLOGIES. Unity Manual [online]. 2026 [cit. 2026-11-26]. Dostupné z: https://docs.unity3d.com/Manual/',
    '[2] UNITY TECHNOLOGIES. Universal Render Pipeline overview [online]. 2026 [cit. 2026-11-26]. Dostupné z: https://docs.unity3d.com/Manual/urp/urp-introduction.html',
    '[3] MICROSOFT. C# documentation [online]. 2026 [cit. 2026-11-26]. Dostupné z: https://learn.microsoft.com/dotnet/csharp/',
    '[4] UNITY TECHNOLOGIES. JsonUtility. Unity Scripting API [online]. 2026 [cit. 2026-11-26]. Dostupné z: https://docs.unity3d.com/ScriptReference/JsonUtility.html',
    '[5] OPEN SOURCE INITIATIVE. The MIT License [online]. [cit. 2026-11-26]. Dostupné z: https://opensource.org/license/mit',
    '[6] CREATIVE COMMONS. CC0 1.0 Universal [online]. [cit. 2026-11-26]. Dostupné z: https://creativecommons.org/publicdomain/zero/1.0/',
    '[7] Česko. Zákon č. 121/2000 Sb., o právu autorském, o právech souvisejících s právem autorským a o změně některých zákonů (autorský zákon). In: Sbírka zákonů. 2000.',
    '[8] Evropská unie. Nařízení Evropského parlamentu a Rady (EU) 2016/679 ze dne 27. dubna 2016 o ochraně fyzických osob v souvislosti se zpracováním osobních údajů (GDPR). In: Úřední věstník Evropské unie. 2016, L 119.',
    '[9] KENNEY. Pirate Kit, Mini Characters [online]. [cit. 2026-11-26]. Dostupné z: https://kenney.nl/assets',
    '[10] QUATERNIUS. Ultimate Animated Fish Pack [online]. [cit. 2026-11-26]. Dostupné z: https://quaternius.com',
    '[11] ŠTĚPÁNEK, Martin. ProjektHra (The Last Beacon) [online]. GitHub, 2026 [cit. 2026-11-26]. Dostupné z: https://github.com/matixiiik/ProjektHra',
    '[12] REDBEET INTERACTIVE. Raft [videohra]. Axolot Games, 2022.',
    '[13] BLACK SALT GAMES. Dredge [videohra]. Team17, 2023.',
    '[14] RARE. Sea of Thieves [videohra]. Xbox Game Studios, 2018.',
    '[15] PERLIN, Ken. An image synthesizer. ACM SIGGRAPH Computer Graphics. 1985, roč. 19, č. 3, s. 287–296.',
    '[16] GODOT ENGINE. Godot Engine documentation [online]. [cit. 2026-11-26]. Dostupné z: https://docs.godotengine.org/',
    '[17] EPIC GAMES. Unreal Engine 5 documentation [online]. [cit. 2026-11-26]. Dostupné z: https://dev.epicgames.com/documentation/unreal-engine',
    '[18] UNITY TECHNOLOGIES. Unity Test Framework [online]. [cit. 2026-11-26]. Dostupné z: https://docs.unity3d.com/Packages/com.unity.test-framework@latest',
  ].map(t => new Paragraph({ spacing: { after: 80 }, indent: { left: 540, hanging: 540 }, children: runs(t) })),
  H2('Přílohy'),
  B('Příloha A – Zdrojový kód: repozitář na GitHubu, finální verze jako Release.'),
  B('Příloha B – Uživatelská příručka (Uzivatelska_prirucka_The_Last_Beacon.pdf).'),
  B('Příloha C – Požadavky školy a jejich splnění (Pozadavky_ITG_2026_2027.md).'),
  B('Příloha D – Snímky obrazovky a diagramy (složka docs/obrazky).'),
  B('Příloha E – Project Charter a prezentace k obhajobě.'),
];

const doc = new Document({
  creator: 'Martin Štěpánek', title: 'The Last Beacon – dokumentace maturitního projektu',
  styles: {
    default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 32, bold: true, color: '1F4E79' }, paragraph: { spacing: { before: 0, after: 200 }, keepNext: true, outlineLevel: 0 } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 26, bold: true, color: '1F4E79' }, paragraph: { spacing: { before: 240, after: 100 }, keepNext: true, outlineLevel: 1 } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 23, bold: true }, paragraph: { spacing: { before: 160, after: 80 }, keepNext: true, outlineLevel: 2 } },
    ],
  },
  numbering,
  features: { updateFields: true },
  sections: [
    { properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1418, bottom: 1418, left: 1134, right: 1134 } } }, children: title },
    { properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1418, bottom: 1418, left: 1134, right: 1134 } } },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 18 })] })] }) },
      children: [...anot, ...toc, ...k1, ...k2, ...k3, ...k4, ...k5, ...k6, ...k7, ...k8, ...src] },
  ],
});
const OUT = process.env.OUT || path.join(__dirname, 'Dokumentace_The_Last_Beacon.docx');
Packer.toBuffer(doc).then(b => { fs.writeFileSync(OUT, b); console.log('OK ' + OUT); });
