// ─────────────────────────────────────────────────────────────────────────────
//  mk_obhajoba.js — generuje Obhajoba_The_Last_Beacon.pptx
//  Prezentace k MATURITNÍ OBHAJOBĚ (1. IT Gymnázium): příprava 5 min,
//  obhajoba 25 min. Pořadí slidů kopíruje, co má žák podle školního dokumentu
//  „Obecné pokyny“ (kap. 18) při obhajobě udělat:
//    zadání a cíl → praktická ukázka → postup a technologie → architektura →
//    vlastní přínos → rozhodnutí → problémy → testování → vyhodnocení →
//    limity a rozvoj → posudky → otázky.
//  Časování je v poznámkách řečníka (prezentace + ukázka ~17 min, zbytek
//  posudky a dotazy). Skryté záložní slidy (kód, repozitář) se při listování
//  přeskakují, skáče se na ně z rozcestníku „Otázky“.
//
//  Spuštění:  NODE_PATH=<složka s node_modules (pptxgenjs)> node mk_obhajoba.js
//  Volitelně: SKILL_PPTX=<složka pptx skillu>  OUT=<výstupní .pptx>
// ─────────────────────────────────────────────────────────────────────────────
const pptxgen = require('pptxgenjs');
const path = require('path');
const fs = require('fs');

// apply_theme.js z pptx skillu (zapíše barvy motivu do theme1.xml)
const SKILL_CANDIDATES = [
  process.env.SKILL_PPTX,
  'C:/Users/matix/AppData/Local/Claude-Data/claude_pzs8sxrjxfjjc/local-agent-mode-sessions/skills-plugin/052f3edc-54b3-4acf-942f-b58b343f6d1b/679b4655-be0e-43be-b6dc-ff9660aa7ac7/skills/pptx',
].filter(Boolean);
const SKILL = SKILL_CANDIDATES.find((p) => fs.existsSync(path.join(p, 'scripts', 'apply_theme.js')));
if (!SKILL) throw new Error('Nenašel jsem pptx skill (apply_theme.js) — nastav SKILL_PPTX.');
const { applyTheme } = require(SKILL + '/scripts/apply_theme.js');

const OUT = process.env.OUT || path.join(__dirname, 'Obhajoba_The_Last_Beacon.pptx');
const IMG = path.join(__dirname, 'obrazky');
// Stejný motiv jako čtvrtletní prezentace (námořní modrá + jantar majáku).
const THEME = {
  name: 'The Last Beacon', headFontFace: 'Cambria', bodyFontFace: 'Calibri',
  colors: { dk1: '0B2A43', lt1: 'FFFFFF', dk2: '1C7293', lt2: 'EAF2F6', accent1: 'F2A93B', accent2: '1C7293', accent3: '2E8B6F', accent4: 'C8553D', accent5: '5F7482', accent6: '0B2A43', hlink: '1C7293', folHlink: '5F7482' },
};
const TINT_AMBER = 'FDF1DC', TINT_GREEN = 'E3F2EC', TINT_RED = 'F8E3DE';

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10" × 5.625"
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = 'The Last Beacon – obhajoba maturitního projektu';
pres.author = 'Martin Štěpánek';
const C = pres.SchemeColor;

// ── Pořadí slidů (čísla pro odkazy) ────────────────────────
const S = { TITLE: 1, CIL: 2, ROZSAH: 3, DEMO: 4, TECH: 5, ARCH: 6, SVET: 7, DATA: 8, PRINOS: 9, PROBLEMY: 10, TESTY: 11, VYHOD: 12, POSUDKY: 13, DOTAZY: 14, Z_KOD: 15, Z_REPO: 16 };
const HIDDEN = [S.Z_KOD, S.Z_REPO];

pres.defineSlideMaster({ title: 'TITULNI', background: { color: THEME.colors.dk1 }, objects: [] });
pres.defineSlideMaster({
  title: 'OBSAH', background: { color: 'FFFFFF' },
  objects: [{ placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 0.3, w: 9, h: 0.8, fontFace: 'Cambria', fontSize: 32, bold: true, color: C.text1, margin: 0, valign: 'middle', align: 'left' }, text: '' } }],
});

// ── Pomocné funkce (stejné jako v mk_prezentace.js) ────────
const TRANSP = 'image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAC0lEQVR4nGNgQAcAABIAAXfx+gAAAAAASUVORK5CYII=';
const link = (s, target, x, y, w, h, name, tip) => s.addImage({ data: TRANSP, x, y, w, h, hyperlink: { slide: target, tooltip: tip }, objectName: name, altText: tip });
const noLine = (c) => ({ color: c, width: 0 });
const text = (s, t, o) => s.addText(t, Object.assign({ margin: 0, isTextBox: true }, o));
const circleNum = (s, n, x, y, d, name, fill, txt) => {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill || C.accent2 }, line: noLine(fill || C.accent2), objectName: name });
  text(s, String(n), { x, y, w: d, h: d, align: 'center', valign: 'middle', fontSize: 14, bold: true, color: txt || C.background1, objectName: name + ' text' });
};
const card = (s, x, y, w, h, name, fill) => s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.12, fill: { color: fill || C.background2 }, line: noLine(fill || C.background2), objectName: name });
const bullets = (items) => items.map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < items.length - 1 } }));
// Postupová lišta dole: kde v obhajobě zrovna jsem (body ze školního dokumentu).
const NAV = [['Cíl', S.CIL], ['Ukázka', S.DEMO], ['Řešení', S.TECH], ['Přínos', S.PRINOS], ['Problémy', S.PROBLEMY], ['Testy', S.TESTY], ['Výsledek', S.VYHOD], ['Posudky', S.POSUDKY], ['Otázky', S.DOTAZY]];
function nav(s, active, pageNo) {
  NAV.forEach((n, i) => {
    const x = 0.5 + i * 0.93, on = n[1] === active;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 5.14, w: 0.88, h: 0.28, rectRadius: 0.08, fill: { color: on ? C.accent2 : C.background2 }, line: noLine(on ? C.accent2 : C.background2), objectName: 'Nav ' + n[0] });
    text(s, n[0], { x, y: 5.14, w: 0.88, h: 0.28, fontSize: 10, bold: on, color: on ? C.background1 : C.text1, align: 'center', valign: 'middle', objectName: 'Nav text ' + n[0] });
    link(s, n[1], x, 5.14, 0.88, 0.28, 'Nav odkaz ' + n[0], 'Přejít: ' + n[0]);
  });
  if (pageNo) text(s, String(pageNo), { x: 9.1, y: 5.14, w: 0.4, h: 0.28, fontSize: 11, color: C.accent5, align: 'right', valign: 'middle', objectName: 'Číslo slidu' });
}
const WARN = '\n\n⚠ POUZE PRO MĚ: ';
const note = (s, spoken, mine) => s.addNotes(spoken + (mine ? WARN + mine : ''));
const img = (file) => path.join(IMG, file);
const content = (title, section, pageNo, active) => {
  const s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: section });
  s.addText(title, { placeholder: 'title' });
  if (active !== undefined) nav(s, active, pageNo);
  return s;
};

// ═════════ 1 Titulní ═════════
pres.addSection({ title: 'Úvod' });
let s = pres.addSlide({ masterName: 'TITULNI', sectionTitle: 'Úvod' });
s.addShape(pres.shapes.OVAL, { x: 0.5, y: 0.6, w: 0.3, h: 0.3, fill: { color: C.accent1 }, line: noLine(C.accent1), objectName: 'Světlo majáku' });
text(s, 'The Last Beacon', { x: 0.5, y: 1.15, w: 3.9, h: 1.3, fontFace: 'Cambria', fontSize: 40, bold: true, color: C.background1, valign: 'middle', objectName: 'Titulek' });
text(s, '3D hra o plavbě po nekonečném moři', { x: 0.5, y: 2.5, w: 3.8, h: 0.8, fontSize: 20, color: C.background2, valign: 'top', objectName: 'Podtitulek' });
text(s, [
  { text: 'Martin Štěpánek', options: { bold: true, breakLine: true } },
  { text: 'Maturitní práce – IT projekt · 1. IT Gymnázium', options: { breakLine: true } },
  { text: 'Vedoucí Kristina Sedeke · 2026/2027' },
], { x: 0.5, y: 3.95, w: 3.9, h: 1.0, fontSize: 13, color: C.background2, valign: 'top', objectName: 'Autor' });
s.addImage({ path: img('shot_ostrov.jpg'), x: 4.6, y: 1.25, w: 4.9, h: 2.76, objectName: 'Snímek ostrova', altText: 'Startovní ostrov s majákem' });
text(s, 'Unity 6 · C# · offline · 1–2 hráči na jednom PC', { x: 4.6, y: 4.15, w: 4.9, h: 0.4, fontSize: 14, color: C.accent1, objectName: 'Stack' });
note(s, '0:00–0:40  Dobrý den, jmenuji se Martin Štěpánek a obhajuji maturitní IT projekt The Last Beacon – 3D hru v Unity o plavbě po nekonečném moři. Nejdřív řeknu, co bylo zadání a cílem, pak hru prakticky předvedu, vysvětlím, jak je postavená a co je moje práce, a nakonec zhodnotím výsledek a odpovím na posudky a vaše otázky.',
  'PŘED OBHAJOBOU (5 min příprava): spustit build (Builds/TheLastBeacon) nebo Unity se SampleScene, načíst testovací slot, zkontrolovat zvuk a rozlišení, mít otevřený VS Code s repem. Klikání funguje jen v režimu prezentace (F5). Lišta dole = skok na libovolnou část.');

// ═════════ 2 Zadání a cíl ═════════
pres.addSection({ title: 'Zadání a cíl' });
s = content('Zadání a cíl', 'Zadání a cíl', 2, S.CIL);
[
  ['Problém', 'Nekonečný svět se nedá namodelovat ručně – musí se generovat za běhu, stejně při každé návštěvě, a hra přesto musí běžet plynule a ukládat se.'],
  ['Cíl', 'Hratelná 3D hra: smyčka „rybařit → prodat → vylepšit loď“, souboje, úkoly a příběh se dvěma konci; offline, i ve dvou na jednom PC.'],
  ['Pro koho', 'Hráči 12–25 let, kteří chtějí klidnou hru bez registrace, a dvojice kamarádů u jednoho počítače.'],
].forEach((b, i) => {
  const y = 1.25 + i * 1.22;
  circleNum(s, i + 1, 0.5, y + 0.02, 0.38, 'Bod ' + (i + 1));
  text(s, b[0], { x: 1.05, y, w: 3.75, h: 0.34, fontSize: 16, bold: true, color: C.accent2, objectName: 'Nadpis ' + (i + 1) });
  text(s, b[1], { x: 1.05, y: y + 0.36, w: 3.75, h: 0.82, fontSize: 13, color: C.text1, valign: 'top', objectName: 'Text ' + (i + 1) });
});
s.addImage({ path: img('shot_splitscreen.jpg'), x: 5.1, y: 1.25, w: 4.4, h: 2.475, objectName: 'Snímek split-screen', altText: 'Split-screen: dva hráči na jednom počítači' });
card(s, 5.1, 3.9, 4.4, 1.06, 'Karta měřitelné cíle', TINT_AMBER);
text(s, [
  { text: 'Měřitelně: ', options: { bold: true, color: C.text1 } },
  { text: '60 snímků/s, uložení bez záseků, dohratelné od nové hry po oba konce, kód s testy a dokumentací.' },
], { x: 5.3, y: 3.9, w: 4.0, h: 1.06, fontSize: 13, color: C.text1, valign: 'middle', objectName: 'Měřitelné cíle' });
note(s, '0:40–2:00  Odborný problém je tenhle: chci svět, který nikdy neskončí. Ten se nedá namodelovat ručně, musí se generovat algoritmem za běhu, a to tak, aby se stejné místo pokaždé vygenerovalo stejně – a hra přitom musí běžet plynule a umět se uložit. Cílem byla hratelná hra s jasnou smyčkou: rybařit a těžit, prodat, vylepšit loď a plout dál, k tomu souboje, úkoly a příběh se dvěma konci. Celé offline a hratelné i ve dvou na jednom počítači. Cílovka jsou hráči zhruba 12 až 25 let. Cíle jsem si stanovil i měřitelně: 60 snímků za sekundu, ukládání bez záseků, dohratelnost a kód s testy a dokumentací.');

// ═════════ 3 Rozsah – co hra umí ═════════
s = content('Co hra umí', 'Zadání a cíl', 3, S.CIL);
const FEAT = [
  ['Svět', ['Nekonečný generovaný oceán', 'Ostrovy, majáky, vraky, bedny', 'Mapa s cílem a deník']],
  ['Hra', ['Rybaření, těžba, výkup', '6 typů úkolů s odměnami', 'Pirátská kořist (25 % šance)']],
  ['Boj a příběh', ['Piráti, děla, pevnost', '3 příběhové ostrovy', 'Hádanky a 2 konce']],
  ['Technika', ['Split-screen pro 2 hráče', '3 sloty, migrace savů', 'Čeština i angličtina']],
];
FEAT.forEach((f, i) => {
  const x = 0.5 + i * 2.3;
  card(s, x, 1.22, 2.15, 2.55, 'Karta ' + f[0]);
  text(s, f[0], { x: x + 0.18, y: 1.34, w: 1.8, h: 0.4, fontSize: 17, bold: true, color: C.accent2, valign: 'middle', objectName: 'Nadpis ' + f[0] });
  text(s, bullets(f[1]), { x: x + 0.18, y: 1.82, w: 1.85, h: 1.85, fontSize: 13, color: C.text1, valign: 'top', paraSpaceAfter: 6, objectName: 'Seznam ' + f[0] });
});
card(s, 0.5, 3.95, 9.0, 1.0, 'Karta mimo rozsah', TINT_RED);
text(s, [
  { text: 'Vědomě mimo rozsah: ', options: { bold: true, color: C.accent4 } },
  { text: 'síťový multiplayer (jen naplánovaný), mobilní verze, vlastní 3D modely – každé z toho by samo zabralo víc času, než projekt měl.' },
], { x: 0.75, y: 3.95, w: 8.5, h: 1.0, fontSize: 14, color: C.text1, valign: 'middle', objectName: 'Mimo rozsah' });
note(s, '2:00–3:00  Krátce, co ve hře je. Svět: nekonečný oceán s ostrovy, majáky, vraky a bednami, k tomu velká mapa a deník. Hra: rybaření, těžba a výkup, šest typů úkolů, za které jsou mince i věci, a kořist z pirátů. Boj a příběh: piráti, děla nepřátelských ostrovů, pirátská pevnost a tři příběhové ostrovy s hádankami a dvěma konci. A technicky split-screen pro dva hráče, tři sloty pro uložení a čeština s angličtinou. Vědomě jsem vynechal síťový multiplayer, mobilní verzi a vlastní modely – to jsem si vymezil hned na začátku.');

// ═════════ 4 Praktická ukázka ═════════
pres.addSection({ title: 'Ukázka' });
s = content('Praktická ukázka', 'Ukázka', 4, S.DEMO);
const DEMO = [
  ['Nová hra, děda', 'start, ovládání, první úkol'],
  ['Rybaření a maják', 'výkupna, obchod, úkoly'],
  ['Mapa a deník', 'souřadnice, cíl, nápověda'],
  ['Souboj s piráty', 'míření kamerou, kořist'],
  ['story 4: pevnost', 'děla na věžích, trezor'],
  ['Uložit a načíst', 'stav se obnoví'],
];
DEMO.forEach((d, i) => {
  const col = i % 2, row = Math.floor(i / 2);
  const x = 0.5 + col * 2.75, y = 1.25 + row * 1.2;
  card(s, x, y, 2.6, 1.05, 'Krok ukázky ' + (i + 1));
  circleNum(s, i + 1, x + 0.15, y + 0.15, 0.38, 'Číslo kroku ' + (i + 1), i === 4 ? C.accent1 : C.accent2, i === 4 ? C.text1 : C.background1);
  text(s, d[0], { x: x + 0.65, y: y + 0.12, w: 1.85, h: 0.42, fontSize: 14, bold: true, color: C.text1, valign: 'middle', objectName: 'Krok název ' + (i + 1) });
  text(s, d[1], { x: x + 0.65, y: y + 0.55, w: 1.85, h: 0.42, fontSize: 12, color: C.accent5, valign: 'top', objectName: 'Krok popis ' + (i + 1) });
});
s.addImage({ path: img('shot_souboj.jpg'), x: 6.15, y: 1.25, w: 3.35, h: 1.885, objectName: 'Snímek souboj', altText: 'Souboj s pirátskou lodí' });
card(s, 6.15, 3.3, 3.35, 1.6, 'Karta konzole', TINT_AMBER);
text(s, [
  { text: 'Jak to stihnu: ', options: { bold: true, breakLine: true } },
  { text: 'vývojářská konzole (klávesa `) má příkaz story 1–10 – přenese mě na libovolnou část příběhu se správnou výbavou.' },
], { x: 6.35, y: 3.3, w: 2.95, h: 1.6, fontSize: 13, color: C.text1, valign: 'middle', objectName: 'Konzole' });
note(s, '3:00–9:00  Teď hru předvedu naživo. (přepnout na hru) Začnu novou hrou u dědy, ukážu ovládání a první úkol. Nachytám pár ryb, v majáku je prodám a ukážu obchod a úkoly. Otevřu mapu – vidíte souřadnice a můžu si kliknout cíl, a deník s kapitolou a nápovědou. Pak souboj s piráty: míří se kamerou, levým tlačítkem střílím, a z piráta může padnout kořist. Abych nemusel hrát půl hodiny, použiju konzoli: příkaz story 4 mě přenese před pirátskou pevnost – děla na věžích, trezor s ozubenými koly. Nakonec hru uložím, vrátím se do menu a načtu ji, aby bylo vidět, že se stav obnoví.',
  'ZÁLOŽNÍ PLÁN, kdyby se něco pokazilo: galerie snímků v docs/obrazky, nebo video. Příkazy: ` (konzole), get money 2000, story 4, get item loot 3. Před obhajobou si to 2× projet nanečisto. Nehrát déle než 6 minut.');

// ═════════ 5 Technologie a proč ═════════
pres.addSection({ title: 'Řešení' });
s = content('Technologie a proč právě ty', 'Řešení', 5, S.TECH);
s.addTable([
  [{ text: '', options: { fill: { color: THEME.colors.lt2 } } }, { text: 'Unity 6 (C#)', options: { bold: true, fill: { color: TINT_GREEN } } }, { text: 'Godot 4', options: { bold: true, fill: { color: THEME.colors.lt2 } } }, { text: 'Unreal 5', options: { bold: true, fill: { color: THEME.colors.lt2 } } }],
  ['Jazyk', 'C# – znám z výuky', 'GDScript / C#', 'C++ / Blueprinty'],
  ['3D na slabším PC', 'dobrý (URP)', 'dobrý, méně nástrojů', 'těžký'],
  ['Návody a assety', 'nejvíc', 'méně', 'hodně, velké'],
], { x: 0.5, y: 1.22, w: 5.6, colW: [1.4, 1.5, 1.35, 1.35], fontSize: 12, color: THEME.colors.dk1, border: { type: 'solid', pt: 0.75, color: 'C9D6DE' }, rowH: 0.42, valign: 'middle', objectName: 'Porovnání enginů' });
[
  ['IMGUI + uGUI', 'menu a obchody kódem (rychlé změny), HUD jako objekty (výkon)'],
  ['JSON místo databáze', 'jeden lokální soubor, čitelný při ladění, bez další knihovny'],
  ['Git + GitHub', 'větve a pull requesty, 220+ commitů, přes 20 PR'],
].forEach((a, i) => {
  const y = 3.18 + i * 0.6;
  circleNum(s, i + 1, 0.5, y + 0.04, 0.34, 'Volba ' + (i + 1));
  text(s, [{ text: a[0] + ':  ', options: { bold: true } }, { text: a[1] }], { x: 0.98, y, w: 5.1, h: 0.45, fontSize: 13, color: C.text1, valign: 'middle', objectName: 'Volba text ' + (i + 1) });
});
s.addImage({ path: img('obchod_pergamen.jpg'), x: 6.35, y: 1.22, w: 3.15, h: 1.507, objectName: 'Snímek obchod', altText: 'Obchod v majáku na pergamenu' });
card(s, 6.35, 3.15, 3.15, 1.8, 'Karta důvod', TINT_AMBER);
text(s, 'Unity jsem zvolil, protože C# znám, URP poběží i na integrované grafice a pro split-screen stačí dvě kamery.', { x: 6.55, y: 3.15, w: 2.75, h: 1.8, fontSize: 13, color: C.text1, valign: 'middle', objectName: 'Důvod výběru' });
note(s, '9:00–10:15  Proč tyhle technologie. Porovnával jsem tři enginy. Godot je lehký a zdarma, ale pro 3D má méně nástrojů. Unreal má nejlepší grafiku, ale pro jednoho autora a slabší počítače je zbytečně těžký a píše se v C++. Unity jsem zvolil, protože C# znám z výuky, URP poběží i na integrované grafice a split-screen jsou prostě dvě kamery. Pro rozhraní kombinuju IMGUI – menu a obchody kreslené kódem, které se rychle mění – a uGUI pro HUD, který je vidět pořád. Ukládám do JSON, protože je to jeden lokální soubor, který si při ladění přečtu, a databáze by byla zbytečná. Celé to verzuju v Gitu s větvemi a pull requesty.');

// ═════════ 6 Architektura ═════════
s = content('Architektura: jeden zdroj pravdy', 'Řešení', 6, S.TECH);
s.addImage({ path: img('architektura.png'), x: 0.5, y: 1.15, w: 5.2, h: 3.85, sizing: { type: 'contain', w: 5.2, h: 3.85 }, objectName: 'Schéma architektury', altText: 'Vrstvy: rozhraní, herní logika, stav GameData, ukládání' });
[
  ['Vrstvy', 'rozhraní → herní logika → stav → ukládání; logika o UI neví (událost OnWorldChanged)'],
  ['Jeden zdroj pravdy', 'GameData drží svět, oba hráče i příběh; GameSession ho nese mezi scénami'],
  ['Dvě scény', 'moře (SampleScene) a vnitřek majáku s obchody'],
].forEach((a, i) => {
  const y = 1.25 + i * 1.22;
  circleNum(s, i + 1, 5.95, y + 0.02, 0.38, 'Arch číslo ' + (i + 1));
  text(s, a[0], { x: 6.48, y, w: 3.02, h: 0.32, fontSize: 15, bold: true, color: C.text1, objectName: 'Arch nadpis ' + (i + 1) });
  text(s, a[1], { x: 6.48, y: y + 0.34, w: 3.02, h: 0.8, fontSize: 13, color: C.accent5, valign: 'top', objectName: 'Arch popis ' + (i + 1) });
});
note(s, '10:15–11:30  Architektura je ve vrstvách. Nahoře rozhraní, pod ním herní logika – generování světa v GridManageru, hráč v PlayerControlleru, souboje v CombatDirectoru a příběh. Pod tím jediný zdroj pravdy: objekt GameData, ve kterém je svět, oba hráči i postup příběhu. Drží ho GameSession, který přežije přechod mezi scénou moře a scénou majáku, takže obě pracují se stejnými daty. A dole SaveManager, který GameData ukládá do JSON. Logika o rozhraní neví – když se něco změní, vyhlásí událost OnWorldChanged a HUD s minimapou se samy překreslí.',
  'Kdyby chtěli kód: VS Code → Assets/Scripts/GameSession.cs (20 řádků), GameData.cs (PlayerState), GridManager.NotifyWorldChanged. Singletonů je pár a jen pro „jeden objekt na scénu“.');

// ═════════ 7 Generování světa ═════════
s = content('Jak vzniká nekonečný svět', 'Řešení', 7, S.TECH);
const GEN = [
  ['Mřížka dlaždic', 'Svět = souřadnice x,y. Ukládá se jen navštívené.'],
  ['Hash souřadnic', 'Stejné x,y = stejná náhoda = stejný ostrov.'],
  ['Organický tvar', 'Jádro + náhodné rozrůstání + zaplnění zálivů.'],
  ['Moře je jedna plocha', 'Hladina, dno a nebe jedou za hráčem.'],
];
GEN.forEach((g, i) => {
  const x = 0.5 + i * 2.3, w = 2.1;
  card(s, x, 1.22, w, 1.85, 'Krok gen ' + (i + 1));
  circleNum(s, i + 1, x + 0.15, 1.36, 0.38, 'Gen číslo ' + (i + 1));
  text(s, g[0], { x: x + 0.15, y: 1.82, w: w - 0.3, h: 0.42, fontSize: 14, bold: true, color: C.text1, valign: 'top', objectName: 'Gen název ' + (i + 1) });
  text(s, g[1], { x: x + 0.15, y: 2.26, w: w - 0.3, h: 0.78, fontSize: 12, color: C.accent5, valign: 'top', objectName: 'Gen popis ' + (i + 1) });
  if (i < GEN.length - 1) s.addShape(pres.shapes.RIGHT_ARROW, { x: x + w + 0.04, y: 2.04, w: 0.14, h: 0.22, fill: { color: C.accent5 }, line: noLine(C.accent5), objectName: 'Šipka gen ' + (i + 1) });
});
s.addImage({ path: img('shot_pevnost.jpg'), x: 0.5, y: 3.25, w: 3.0, h: 1.69, objectName: 'Snímek pevnost', altText: 'Pirátská pevnost na generovaném ostrově' });
card(s, 3.7, 3.25, 5.8, 1.69, 'Karta kód gen', TINT_AMBER);
text(s, [
  { text: '// stejné místo = stejné číslo = stejný ostrov', options: { color: C.accent5, breakLine: true } },
  { text: 'int h = (x * 73856093 ^ y * 19349663) & 0x7fffffff;', options: { breakLine: true } },
  { text: 'bool ostrov = h % 100 < 30;   // 30 % políček mřížky' },
], { x: 3.9, y: 3.25, w: 5.4, h: 1.69, fontFace: 'Courier New', fontSize: 12, color: C.text1, valign: 'middle', objectName: 'Kód hash' });
note(s, '11:30–12:45  Jak vzniká nekonečný svět. Svět je mřížka dlaždic s celočíselnými souřadnicemi a do savu se ukládá jen to, co hráč navštívil. Aby se ostrov při další návštěvě vygeneroval stejně, beru jako semínko náhody hash souřadnic – stejné x a y dají vždycky stejné číslo. Ostrov pak nemá tvar čtverce: vznikne pevné jádro, kolem něj se náhodně přidávají dlaždice a nakonec se zaplní zálivy. A nejdůležitější rozhodnutí pro výkon: moře nemá dlaždice, je to jedna plocha hladiny, dno a obloha, které jedou s hráčem, takže 3D objekty mají jen ostrovy, ryby a vraky.',
  'Kód: GridManager.StampOrganicLand, GenerateRegion (zpracovává jen nově odkrytý okraj čtverce 28×28), IslandTerrain.Build (mesh se staví přes korutinu o snímek později).');

// ═════════ 8 Data a ukládání ═════════
s = content('Data a ukládání bez záseků', 'Řešení', 8, S.TECH);
s.addImage({ path: img('datovy_model.png'), x: 0.5, y: 1.15, w: 4.6, h: 3.85, sizing: { type: 'contain', w: 4.6, h: 3.85 }, objectName: 'Datový model', altText: 'Třídní diagram GameData, PlayerState, TileStatus' });
card(s, 5.35, 1.22, 4.15, 1.3, 'Karta výkon', TINT_GREEN);
text(s, '800 ms → 0,13 ms', { x: 5.55, y: 1.28, w: 3.8, h: 0.6, fontFace: 'Cambria', fontSize: 28, bold: true, color: C.text1, valign: 'middle', objectName: 'Číslo výkon' });
text(s, 'zápis savu na hlavním vlákně – dřív zasekl hru při každé rybě', { x: 5.55, y: 1.88, w: 3.8, h: 0.55, fontSize: 12, color: C.text1, valign: 'top', objectName: 'Popis výkon' });
text(s, bullets([
  'Uloží se nejvýš 1× za 8 s, na pozadí, přes dočasný soubor',
  'Verze savu + migrace: starý save se vždy načte',
  'Nová pole = 0 / prázdný seznam, kód s tím počítá',
]), { x: 5.35, y: 2.75, w: 4.15, h: 2.1, fontSize: 13, color: C.text1, valign: 'top', paraSpaceAfter: 8, objectName: 'Body ukládání' });
note(s, '12:45–13:45  Data. Protože hra nemá databázi, roli datového modelu má třídní model GameData – svět jako slovník dlaždic, pole dvou hráčů a postup příběhu. Ukládání byl největší výkonový problém: save má několik megabajtů a zapisoval se při každé rybě, což hru zaseklo zhruba na osm set milisekund. Teď se změna jen označí a zápis proběhne nejvýš jednou za osm sekund na pozadí, přes dočasný soubor, takže ani výpadek uprostřed zápisu nezničí předchozí uložení. Save má číslo verze a při načtení se starý formát převede na nový – to hlídají automatické testy.');

// ═════════ 9 Vlastní přínos a AI ═════════
pres.addSection({ title: 'Přínos' });
s = content('Můj přínos a použití AI', 'Přínos', 9, S.PRINOS);
card(s, 0.5, 1.22, 4.4, 3.72, 'Karta moje práce', TINT_GREEN);
text(s, 'Co je moje', { x: 0.7, y: 1.32, w: 4.0, h: 0.4, fontSize: 17, bold: true, color: C.accent3, objectName: 'Nadpis moje' });
text(s, bullets([
  'Zadání, herní návrh a rozsah',
  'Svět, ekonomika, příběh, postavy, hádanky',
  'Rozhodnutí o architektuře a technologiích',
  'Testování ve hře, hledání chyb, ladění',
  'Kontrola a vysvětlení každé změny',
]), { x: 0.7, y: 1.8, w: 4.0, h: 3.0, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 7, objectName: 'Seznam moje' });
card(s, 5.1, 1.22, 4.4, 3.72, 'Karta AI', TINT_AMBER);
text(s, 'AI jako nástroj (Claude Code)', { x: 5.3, y: 1.32, w: 4.0, h: 0.4, fontSize: 17, bold: true, color: C.text1, objectName: 'Nadpis AI' });
text(s, bullets([
  'Psaní a úpravy kódu podle mého zadání',
  'Hledání chyb, měření výkonu',
  'Testy, diagramy, dokumentace',
  'Uvedeno v dokumentaci (kap. 4.13)',
]), { x: 5.3, y: 1.8, w: 4.0, h: 2.0, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 7, objectName: 'Seznam AI' });
text(s, 'Kód je psaný jednoduše a česky okomentovaný, abych ho uměl vysvětlit.', { x: 5.3, y: 3.95, w: 4.0, h: 0.85, fontSize: 13, italic: true, color: C.accent5, valign: 'top', objectName: 'Poznámka AI' });
note(s, '13:45–15:00  K vlastnímu přínosu a k AI otevřeně, jak to škola chce. Moje je zadání, celý herní návrh – svět, ekonomika, příběh, postavy a hádanky –, rozhodnutí o rozsahu, architektuře a technologiích, testování ve hře a kontrola každé změny. Jako pracovní nástroj jsem používal AI asistenta Claude Code: psal a upravoval kód podle mého zadání, pomáhal hledat chyby a měřit výkon a psát testy a dokumentaci. Uvádím to v dokumentaci v kapitole 4.13. Proto je kód záměrně jednoduchý a česky okomentovaný – abych každou část uměl vysvětlit, a klidně kteroukoli otevřu.',
  'DOPLNIT VLASTNÍMI SLOVY 1–2 konkrétní příklady, co jsem navrhl sám (např. hádanka v dopise, ekonomika, příběh se dvěma konci) a co jsem konzultoval. Připravit se vysvětlit: generování ostrova, ukládání, AddProgress u úkolů.');

// ═════════ 10 Problémy a řešení ═════════
pres.addSection({ title: 'Problémy' });
s = content('Problémy a jak jsem je vyřešil', 'Problémy', 10, S.PROBLEMY);
const PROB = [
  ['Záseky při ukládání', '800 ms při každé rybě', 'odložený zápis na pozadí'],
  ['Sekání v boji', '49 snímků/s, 231 KB/snímek', 'cache + statické seznamy → 64 sn./s'],
  ['Ostrov bez hradeb', '1 ze 40 tvarů pevnosti', 'vyhlazení tvaru, test na 200 pozicích'],
  ['Druhý hráč se neukládal', 'zdvojený kód player2…', 'sjednocení do PlayerState[2]'],
];
PROB.forEach((p, i) => {
  const y = 1.22 + i * 0.94;
  card(s, 0.5, y, 9.0, 0.82, 'Karta problém ' + (i + 1));
  circleNum(s, i + 1, 0.68, y + 0.21, 0.4, 'Problém číslo ' + (i + 1), C.accent4);
  text(s, p[0], { x: 1.25, y, w: 2.75, h: 0.82, fontSize: 15, bold: true, color: C.text1, valign: 'middle', objectName: 'Problém ' + (i + 1) });
  text(s, p[1], { x: 4.05, y, w: 2.5, h: 0.82, fontSize: 13, color: C.accent4, valign: 'middle', objectName: 'Příčina ' + (i + 1) });
  s.addShape(pres.shapes.RIGHT_ARROW, { x: 6.55, y: y + 0.3, w: 0.25, h: 0.22, fill: { color: C.accent5 }, line: noLine(C.accent5), objectName: 'Šipka problém ' + (i + 1) });
  text(s, p[2], { x: 6.95, y, w: 2.45, h: 0.82, fontSize: 13, bold: true, color: C.accent3, valign: 'middle', objectName: 'Řešení ' + (i + 1) });
});
note(s, '15:00–16:15  Čtyři největší problémy. Ukládání zasekávalo hru – vyřešil jsem to odloženým zápisem na pozadí. V boji hra sekala na 49 snímků, protože minimapa skládala pro každý pixel řetězec a skript každý snímek hledal všechny piráty ve scéně – po cache a statických seznamech je to 64 snímků. Pevnost na příběhovém ostrově občas neměla hradby, protože jeden ze čtyřiceti tvarů pobřeží měl dva laloky – tvar teď vyhlazuju a ověřil jsem to na dvou stech pozicích. A druhý hráč se kvůli zdvojenému kódu vůbec neukládal – sjednotil jsem oba hráče do jednoho pole a starý save se převede.',
  'Další v dokumentaci (kap. 4.11): Shader.Find v buildu null → Always Included Shaders; migrace přeskočila save bez saveVersion → odhalil test.');

// ═════════ 11 Testování ═════════
pres.addSection({ title: 'Testy' });
s = content('Testování', 'Testy', 11, S.TESTY);
card(s, 0.5, 1.22, 2.9, 1.6, 'Karta auto testy', TINT_GREEN);
text(s, '33 + 4', { x: 0.7, y: 1.28, w: 2.5, h: 0.8, fontFace: 'Cambria', fontSize: 40, bold: true, color: C.text1, valign: 'middle', objectName: 'Číslo testů' });
text(s, 'automatických testů (EditMode + PlayMode)', { x: 0.7, y: 2.08, w: 2.5, h: 0.65, fontSize: 12, color: C.text1, valign: 'top', objectName: 'Popis testů' });
text(s, bullets([
  'Ekonomika, lodě, úkoly a kořist',
  'Migrace starých savů (našly skutečnou chybu)',
  'Kamera a vlny v PlayMode',
]), { x: 0.5, y: 3.0, w: 2.9, h: 1.9, fontSize: 13, color: C.text1, valign: 'top', paraSpaceAfter: 6, objectName: 'Co testy hlídají' });
s.addTable([
  [{ text: 'Ruční scénář', options: { bold: true, fill: { color: THEME.colors.lt2 } } }, { text: 'Výsledek', options: { bold: true, fill: { color: THEME.colors.lt2 } } }],
  ['Nová hra → maják → obchod', 'OK'],
  ['Uložit, vypnout, načíst (i build)', 'OK'],
  ['Smrt, respawn bez kořisti', 'OK'],
  ['Přepnutí jazyka', 'OK'],
  ['Split-screen, převod mincí', 'OK'],
  ['Kořist, bojový úkol, odměna', 'OK'],
  ['Presety příběhu story 1–10', 'průběžně'],
], { x: 3.65, y: 1.22, w: 5.85, colW: [4.35, 1.5], fontSize: 12, color: THEME.colors.dk1, border: { type: 'solid', pt: 0.75, color: 'C9D6DE' }, rowH: 0.42, valign: 'middle', objectName: 'Ruční scénáře' });
text(s, 'Navíc: kontrola kompilace po každé změně, Profiler, smoke test buildu.', { x: 3.65, y: 4.35, w: 5.85, h: 0.5, fontSize: 12, italic: true, color: C.accent5, objectName: 'Další nástroje' });
note(s, '16:15–17:15  Testování má dvě části. Automatické testy v Unity Test Frameworku hlídají datovou část – ekonomiku, lodě, úkoly a kořist a hlavně převod starých savů; jeden z nich odhalil skutečnou chybu, kdy se nejstarší save nepřevedl. Herní logiku ve scéně testuju ručně podle scénářů z dokumentace – nová hra, obchod, uložení a načtení i v hotovém buildu, smrt a respawn, přepnutí jazyka a split-screen. Na rychlé dosažení libovolného místa v příběhu mám presety v konzoli. A po každé změně kontroluju kompilaci a výkon v Profileru.',
  'Poslední běh 26. 11. 2026: EditMode 33/33, PlayMode 4/4. Tabulka scénářů = dokumentace kap. 5.3.');

// ═════════ 12 Vyhodnocení, limity, rozvoj ═════════
pres.addSection({ title: 'Výsledek' });
s = content('Vyhodnocení, limity a další rozvoj', 'Výsledek', 12, S.VYHOD);
[
  ['Splněno', C.accent3, ['Nekonečný svět, ukládání bez záseků', 'Dohratelné po oba konce', 'Hra ve dvou, CZ/EN, offline', 'Testy a dokumentace']],
  ['Limity', C.accent4, ['GridManager a PlayerController jsou velké', 'Logika ve scéně bez autom. testů', 'Výkon ověřen jen na PC']],
  ['Další rozvoj', C.accent1, ['Síťový multiplayer (plán hotový)', 'Další ostrovy, lodě a zbraně', 'Hudba a lepší zvuk']],
].forEach((c, i) => {
  const x = 0.5 + i * 3.07;
  card(s, x, 1.22, 2.86, 3.72, 'Karta ' + c[0]);
  s.addShape(pres.shapes.OVAL, { x: x + 0.2, y: 1.42, w: 0.26, h: 0.26, fill: { color: c[1] }, line: noLine(c[1]), objectName: 'Tečka ' + c[0] });
  text(s, c[0], { x: x + 0.58, y: 1.34, w: 2.15, h: 0.42, fontSize: 17, bold: true, color: C.text1, valign: 'middle', objectName: 'Nadpis ' + c[0] });
  text(s, bullets(c[2]), { x: x + 0.2, y: 1.9, w: 2.5, h: 2.9, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 8, objectName: 'Seznam ' + c[0] });
});
note(s, '17:15–18:30  Zhodnocení. Cíle jsem splnil: svět je nekonečný a ukládání nesekají, hra je dohratelná po oba konce, hraje se i ve dvou, česky i anglicky a offline, a má testy a dokumentaci. Limity vidím taky: dvě hlavní třídy, GridManager a PlayerController, jsou moc velké a příště bych je rozdělil dřív; herní logika ve scéně nemá automatické testy a výkon jsem měřil jen na stolním počítači. Dál bych pokračoval síťovým multiplayerem, na který mám hotový plán, dalšími ostrovy a lodmi a hudbou. Tím jsem s prezentací hotový – teď bych rád zareagoval na posudky.');

// ═════════ 13 Posudky ═════════
pres.addSection({ title: 'Posudky a otázky' });
s = content('Reakce na posudky', 'Posudky a otázky', 13, S.POSUDKY);
[['Vedoucí – Kristina Sedeke', 'Připomínka 1 z posudku', 'Moje odpověď / co jsem udělal'], ['Oponent', 'Připomínka 1 z posudku', 'Moje odpověď / co jsem udělal']].forEach((p, i) => {
  const x = 0.5 + i * 4.6;
  card(s, x, 1.22, 4.4, 3.72, 'Karta posudek ' + (i + 1));
  text(s, p[0], { x: x + 0.2, y: 1.32, w: 4.0, h: 0.42, fontSize: 16, bold: true, color: C.accent2, objectName: 'Posuzovatel ' + (i + 1) });
  text(s, [
    { text: p[1], options: { bold: true, breakLine: true } },
    { text: p[2] + ' – doplnit po obdržení posudku (14 dní před obhajobou).', options: { color: C.accent5 } },
  ], { x: x + 0.2, y: 1.85, w: 4.0, h: 2.9, fontSize: 13, color: C.text1, valign: 'top', objectName: 'Posudek text ' + (i + 1) });
});
note(s, '18:30–21:00  Na posudky. (Za každou připomínku: poděkovat, říct, jestli souhlasím, a co jsem s tím udělal nebo proč to tak je.)',
  'Posudky dorazí nejpozději 14 dní před obhajobou – sem napsat 2–3 hlavní připomínky každého a krátkou odpověď. Hodnotí se to 25 %!');

// ═════════ 14 Otázky ═════════
s = pres.addSlide({ masterName: 'TITULNI', sectionTitle: 'Posudky a otázky' });
text(s, 'Děkuji za pozornost', { x: 0.5, y: 0.8, w: 9.0, h: 0.9, fontFace: 'Cambria', fontSize: 36, bold: true, color: C.background1, valign: 'middle', objectName: 'Děkuji' });
text(s, 'Prostor pro vaše otázky', { x: 0.5, y: 1.7, w: 9.0, h: 0.5, fontSize: 20, color: C.background2, objectName: 'Otázky podtitulek' });
[['Ukázka kódu', S.Z_KOD], ['Repozitář a postup', S.Z_REPO], ['Architektura', S.ARCH], ['Problémy', S.PROBLEMY]].forEach((b, i) => {
  const x = 0.5 + i * 2.3;
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 2.85, w: 2.1, h: 0.7, rectRadius: 0.12, fill: { color: C.accent2 }, line: noLine(C.accent2), objectName: 'Rozcestník ' + b[0] });
  text(s, b[0], { x, y: 2.85, w: 2.1, h: 0.7, fontSize: 15, bold: true, color: C.background1, align: 'center', valign: 'middle', objectName: 'Rozcestník text ' + b[0] });
  link(s, b[1], x, 2.85, 2.1, 0.7, 'Rozcestník odkaz ' + b[0], 'Přejít: ' + b[0]);
});
text(s, 'github.com/matixiiik/ProjektHra', { x: 0.5, y: 4.4, w: 9.0, h: 0.4, fontSize: 14, color: C.accent1, objectName: 'Odkaz repo' });
note(s, '21:00–25:00  Děkuji za pozornost, rád odpovím na otázky.',
  'Rozcestník vede na skryté záložní slidy. Na otázku „ukaž kód“: otevřít VS Code přímo (Assets/Scripts), ne jen slide.');

// ═════════ 15 Záloha: kód ═════════
s = content('Záloha: jak se plní úkoly', 'Posudky a otázky');
card(s, 0.5, 1.22, 5.4, 2.45, 'Karta kód', 'F4F6F8');
text(s, [
  { text: '// ActiveQuest – jedna metoda pro všechny typy', options: { color: C.accent5, breakLine: true } },
  { text: 'public void AddProgress(int type, int amount)', options: { breakLine: true } },
  { text: '{', options: { breakLine: true } },
  { text: '    if (!hasQuest || questType != type', options: { breakLine: true } },
  { text: '        || amount <= 0) return;', options: { breakLine: true } },
  { text: '    progress = Mathf.Min(progress + amount,', options: { breakLine: true } },
  { text: '                         target);', options: { breakLine: true } },
  { text: '}' },
], { x: 0.7, y: 1.32, w: 5.0, h: 2.25, fontFace: 'Courier New', fontSize: 13, color: C.text1, valign: 'top', objectName: 'Kód AddProgress' });
text(s, bullets([
  'Volá se z rybaření, těžby, potopení piráta, zničení děla, bedny a kořisti',
  'Typ úkolu je číslo v savu – nikdy se nepřečíslovává',
  'Pokrývají to testy LootAndQuestTests',
]), { x: 6.15, y: 1.22, w: 3.35, h: 3.4, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 10, objectName: 'Vysvětlení kódu' });
link(s, S.DOTAZY, 0.5, 4.75, 2.2, 0.32, 'Zpět na otázky', 'Zpět na otázky');
text(s, '← zpět na otázky', { x: 0.5, y: 4.75, w: 2.2, h: 0.32, fontSize: 12, color: C.accent2, objectName: 'Zpět text' });
note(s, 'Záložní slide: ukázka čitelného kódu. Jedna metoda posouvá všechny typy úkolů; volají ji místa, kde se něco „počítá“.');

// ═════════ 16 Záloha: repozitář ═════════
s = content('Záloha: repozitář a průběžná práce', 'Posudky a otázky');
[
  ['220+', 'commitů od dubna 2026'],
  ['20+', 'pull requestů s popisem'],
  ['60', 'skriptů, ~19 000 řádků'],
].forEach((st, i) => {
  const x = 0.5 + i * 3.07;
  card(s, x, 1.22, 2.86, 1.4, 'Karta stat ' + (i + 1), i === 0 ? TINT_GREEN : THEME.colors.lt2);
  text(s, st[0], { x: x + 0.2, y: 1.28, w: 2.46, h: 0.8, fontFace: 'Cambria', fontSize: 34, bold: true, color: C.text1, valign: 'middle', objectName: 'Stat číslo ' + (i + 1) });
  text(s, st[1], { x: x + 0.2, y: 2.06, w: 2.46, h: 0.45, fontSize: 13, color: C.accent5, objectName: 'Stat popis ' + (i + 1) });
});
text(s, bullets([
  'Assets/Scripts – kód, Assets/Tests – testy, docs – dokumentace, diagramy a generátory',
  'README: jak spustit, ovládání, technologie, licence (MIT, assety CC0)',
  'Každá větší změna ve vlastní větvi a pull requestu do main',
]), { x: 0.5, y: 2.85, w: 9.0, h: 1.8, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 8, objectName: 'Struktura repa' });
link(s, S.DOTAZY, 0.5, 4.75, 2.2, 0.32, 'Zpět na otázky 2', 'Zpět na otázky');
text(s, '← zpět na otázky', { x: 0.5, y: 4.75, w: 2.2, h: 0.32, fontSize: 12, color: C.accent2, objectName: 'Zpět text 2' });
note(s, 'Záložní slide: průběžná práce je vidět v historii Gitu – škola to hodnotí (průběžná práce 10 %).');

// ── Zápis + skryté slidy + motiv ────────────────────────────
(async () => {
  await pres.writeFile({ fileName: OUT });
  // Skryté záložní slidy: atribut show="0" v XML slidu (pptxgenjs to neumí přímo).
  const JSZip = require(require.resolve('jszip', { paths: [path.dirname(require.resolve('pptxgenjs'))] }));
  const zip = await JSZip.loadAsync(fs.readFileSync(OUT));
  for (const n of (process.env.NOHIDE ? [] : HIDDEN)) { // NOHIDE=1 = náhled i skrytých slidů
    const f = `ppt/slides/slide${n}.xml`;
    let x = await zip.file(f).async('string');
    x = x.replace('<p:sld ', '<p:sld show="0" ');
    zip.file(f, x);
  }
  fs.writeFileSync(OUT, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
  await applyTheme(OUT, THEME);
  console.log('OK ' + OUT);
})();
