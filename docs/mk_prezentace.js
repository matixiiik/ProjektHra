// ─────────────────────────────────────────────────────────────────────────────
//  mk_prezentace.js — generuje Prezentace_The_Last_Beacon.pptx
//  Čtvrtletní prezentace 11. 11. 2026, limit 7 minut. Struktura odpovídá
//  hodnotící rubrice: Scope & rizika (35 %), Architektura (35 %), Business
//  (10 %), Repo a prezentace (20 %).
//
//  INTERAKTIVNÍ (klikání funguje jen v režimu prezentace, F5):
//   - navigační lišta dole (skok na libovolnou část),
//   - slide 4: klikací schéma architektury (klik na vrstvu = skrytý detail),
//   - slide 6: galerie, klik na náhled/krok zvětší snímek (přechod Morph),
//   - slide 13: rozcestník pro dotazy.
//  Skryté slidy (galerie 7–9, detaily 14–17) se při listování šipkou přeskakují.
//
//  Spuštění:  NODE_PATH=<složka s node_modules> node mk_prezentace.js
//  Volitelně: SKILL_PPTX=<složka pptx skillu>  OUT=<výstupní .pptx>
// ─────────────────────────────────────────────────────────────────────────────
const pptxgen = require('pptxgenjs');
const path = require('path');
const fs = require('fs');

// apply_theme.js z pptx skillu (zapíše barvy motivu do theme1.xml)
const SKILL_CANDIDATES = [
  process.env.SKILL_PPTX,
  'C:/Users/QQM2Z/AppData/Local/Packages/Claude_pzs8sxrjxfjjc/LocalCache/Roaming/Claude/local-agent-mode-sessions/skills-plugin/052f3edc-54b3-4acf-942f-b58b343f6d1b/679b4655-be0e-43be-b6dc-ff9660aa7ac7/skills/pptx',
  'C:/Users/matix/AppData/Local/Claude-Data/claude_pzs8sxrjxfjjc/local-agent-mode-sessions/skills-plugin/052f3edc-54b3-4acf-942f-b58b343f6d1b/679b4655-be0e-43be-b6dc-ff9660aa7ac7/skills/pptx',
].filter(Boolean);
const SKILL = SKILL_CANDIDATES.find((p) => fs.existsSync(path.join(p, 'scripts', 'apply_theme.js')));
if (!SKILL) throw new Error('Nenašel jsem pptx skill (apply_theme.js) — nastav SKILL_PPTX.');
const { applyTheme } = require(SKILL + '/scripts/apply_theme.js');
const JSZip = require(require.resolve('jszip', { paths: [path.dirname(require.resolve('pptxgenjs'))] }));

const OUT = process.env.OUT || path.join(__dirname, 'Prezentace_The_Last_Beacon.pptx');
const IMG = path.join(__dirname, 'obrazky');
const THEME = {
  name: 'The Last Beacon', headFontFace: 'Cambria', bodyFontFace: 'Calibri',
  colors: { dk1: '0B2A43', lt1: 'FFFFFF', dk2: '1C7293', lt2: 'EAF2F6', accent1: 'F2A93B', accent2: '1C7293', accent3: '2E8B6F', accent4: 'C8553D', accent5: '5F7482', accent6: '0B2A43', hlink: '1C7293', folHlink: '5F7482' },
};
const HEX = THEME.colors; // pro volby, které berou jen hex (tabulky)
const TINT_AMBER = 'FDF1DC', TINT_GREEN = 'E3F2EC';

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10" × 5.625"
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = 'The Last Beacon – čtvrtletní prezentace';
pres.author = 'Martin Štěpánek';
const C = pres.SchemeColor;

// ── Pořadí slidů (čísla pro odkazy) ────────────────────────
const S = { TITLE: 1, TEMA: 2, ROZSAH: 3, ARCH: 4, TOK: 5, DEMO: 6, RIZIKA: 10, BUSINESS: 11, PLAN: 12, DOTAZY: 13, D_UI: 14, D_LOGIKA: 15, D_STAV: 16, D_SAVE: 17 };
const HIDDEN = [7, 8, 9, 14, 15, 16, 17];
const MORPH = [7, 8, 9];

pres.defineSlideMaster({ title: 'TITULNI', background: { color: THEME.colors.dk1 }, objects: [] });
pres.defineSlideMaster({
  title: 'OBSAH', background: { color: 'FFFFFF' },
  objects: [{ placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 0.3, w: 9, h: 0.8, fontFace: 'Cambria', fontSize: 34, bold: true, color: C.text1, margin: 0, valign: 'middle', align: 'left' }, text: '' } }],
});

// ── Pomocné funkce ─────────────────────────────────────────
const TRANSP = 'image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAC0lEQVR4nGNgQAcAABIAAXfx+gAAAAAASUVORK5CYII=';
// Průhledná klikací plocha (obrázek s odkazem na slide) — reaguje celá plocha.
const link = (s, target, x, y, w, h, name, tip) => s.addImage({ data: TRANSP, x, y, w, h, hyperlink: { slide: target, tooltip: tip }, objectName: name, altText: tip });
const noLine = (c) => ({ color: c, width: 0 });
const text = (s, t, o) => s.addText(t, Object.assign({ margin: 0, isTextBox: true }, o));
const circleNum = (s, n, x, y, d, name, fill, txt) => {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill || C.accent2 }, line: noLine(fill || C.accent2), objectName: name });
  text(s, String(n), { x, y, w: d, h: d, align: 'center', valign: 'middle', fontSize: 14, bold: true, color: txt || C.background1, objectName: name + ' text' });
};
const card = (s, x, y, w, h, name, fill) => s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.12, fill: { color: fill || C.background2 }, line: noLine(fill || C.background2), objectName: name });
const pill = (s, label, x, y, w, h, fill, name) => {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: h / 2, fill: { color: fill }, line: noLine(fill), objectName: name });
  text(s, label, { x, y, w, h, fontSize: 11, bold: true, color: C.background1, align: 'center', valign: 'middle', objectName: name + ' text' });
};
const NAV = [['Téma', S.TEMA], ['Rozsah', S.ROZSAH], ['Technika', S.ARCH], ['Tok dat', S.TOK], ['Ukázka', S.DEMO], ['Rizika', S.RIZIKA], ['Business', S.BUSINESS], ['Plán', S.PLAN], ['Otázky', S.DOTAZY]];
function nav(s, active, pageNo) {
  NAV.forEach((n, i) => {
    const x = 0.5 + i * 0.93, on = n[1] === active;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 5.14, w: 0.88, h: 0.28, rectRadius: 0.08, fill: { color: on ? C.accent2 : C.background2 }, line: noLine(on ? C.accent2 : C.background2), objectName: 'Nav ' + n[0] });
    text(s, n[0], { x, y: 5.14, w: 0.88, h: 0.28, fontSize: 10, bold: on, color: on ? C.background1 : C.text1, align: 'center', valign: 'middle', objectName: 'Nav text ' + n[0] });
    link(s, n[1], x, 5.14, 0.88, 0.28, 'Nav odkaz ' + n[0], 'Přejít: ' + n[0]);
  });
  if (pageNo) text(s, String(pageNo), { x: 9.1, y: 5.14, w: 0.4, h: 0.28, fontSize: 11, color: C.accent5, align: 'right', valign: 'middle', objectName: 'Číslo slidu' });
}
const WARN = '\n\n⚠ POUZE PRO MĚ (při promítání se nezobrazí): ';
const note = (s, spoken, mine) => s.addNotes(spoken + (mine ? WARN + mine : ''));
const img = (file) => path.join(IMG, file);

// ═════════ 1 Titulní ═════════
pres.addSection({ title: 'Úvod' });
let s = pres.addSlide({ masterName: 'TITULNI', sectionTitle: 'Úvod' });
s.addShape(pres.shapes.OVAL, { x: 0.5, y: 0.6, w: 0.3, h: 0.3, fill: { color: C.accent1 }, line: noLine(C.accent1), objectName: 'Světlo majáku' });
text(s, 'The Last Beacon', { x: 0.5, y: 1.15, w: 3.9, h: 1.3, fontFace: 'Cambria', fontSize: 40, bold: true, color: C.background1, valign: 'middle', objectName: 'Titulek' });
text(s, '3D hra o plavbě po nekonečném moři', { x: 0.5, y: 2.5, w: 3.8, h: 0.8, fontSize: 20, color: C.background2, valign: 'top', objectName: 'Podtitulek' });
text(s, [
  { text: 'Martin Štěpánek', options: { bold: true, breakLine: true } },
  { text: 'IT projekt, 4. ročník · vedoucí Kristina Sedeke', options: { breakLine: true } },
  { text: 'Čtvrtletní prezentace, 11. 11. 2026' },
], { x: 0.5, y: 3.95, w: 3.9, h: 1.0, fontSize: 13, color: C.background2, valign: 'top', objectName: 'Autor' });
s.addImage({ path: img('shot_ostrov.jpg'), x: 4.6, y: 1.25, w: 4.9, h: 2.76, objectName: 'Snímek ostrova', altText: 'Startovní ostrov s majákem, hráč na molu u veslice' });
text(s, 'Unity 6 · C# · offline · 1–2 hráči na jednom PC', { x: 4.6, y: 4.15, w: 4.9, h: 0.4, fontSize: 14, color: C.accent1, objectName: 'Stack' });
note(s, '0:00–0:20  Dobrý den, jsem Martin Štěpánek a představím vám svůj IT projekt The Last Beacon – 3D hru v Unity o plavbě po nekonečném moři. Za sedm minut ukážu, pro koho hra je, co je hotové, jak funguje uvnitř, jaká má rizika, jaký má přínos a plán do dubna.',
  'OVLÁDÁNÍ: klikání funguje jen v režimu prezentace (F5). Dole je navigační lišta, klikem skočíš kamkoli. Skryté slidy (galerie, detaily architektury) se šipkou přeskakují. Všechny snímky jsou z čisté nové hry (slot 2), česky, bez cheatů.');

// ═════════ 2 Téma ═════════
pres.addSection({ title: 'Téma' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Téma' });
s.addText('Co to je a pro koho', { placeholder: 'title' });
[
  ['Co to je', 'Plavba po moři, které se generuje samo: rybaření, poklady z vraků, obchody v majácích, piráti a příběh se dvěma konci.'],
  ['Pro koho', 'Hráči kolem 12–25 let, kteří chtějí klidnou hru bez stresu, a dvojice kamarádů na jednom počítači.'],
  ['Co jim to dá', 'Odpočinek, jasný cíl (lepší loď, další ostrov) a příběh. Bez registrace a bez internetu.'],
].forEach((b, i) => {
  const y = 1.25 + i * 1.22;
  circleNum(s, i + 1, 0.5, y + 0.02, 0.38, 'Bod ' + (i + 1));
  text(s, b[0], { x: 1.05, y, w: 3.75, h: 0.34, fontSize: 16, bold: true, color: C.accent2, objectName: 'Nadpis ' + (i + 1) });
  text(s, b[1], { x: 1.05, y: y + 0.36, w: 3.75, h: 0.8, fontSize: 14, color: C.text1, valign: 'top', objectName: 'Text ' + (i + 1) });
});
s.addImage({ path: img('shot_splitscreen.jpg'), x: 5.1, y: 1.25, w: 4.4, h: 2.475, objectName: 'Snímek split-screen', altText: 'Split-screen: dva hráči na jednom počítači' });
text(s, 'Split-screen: každý hráč má vlastní loď, peníze i HUD', { x: 5.1, y: 3.78, w: 4.4, h: 0.28, fontSize: 11, italic: true, color: C.accent5, objectName: 'Popisek split-screen' });
card(s, 5.1, 4.18, 4.4, 0.78, 'Karta cíle');
text(s, [
  { text: 'Cíl práce: ', options: { bold: true, color: C.accent2 } },
  { text: 'hratelný build, veřejný repozitář a dokumentace do 14. 4. 2027.' },
], { x: 5.3, y: 4.18, w: 4.0, h: 0.78, fontSize: 14, color: C.text1, valign: 'middle', objectName: 'Cíl práce' });
nav(s, S.TEMA, 2);
note(s, '0:20–1:05  Hráč pluje lodí po moři, které se generuje samo podle toho, kam jede, takže nikdy neskončí. Rybaří, těží poklady z vraků, v majácích je prodává a vylepšuje si loď. Do toho bojuje s piráty a postupně odemyká příběh starého námořníka a jeho ztraceného bratra. Cílová skupina jsou hráči zhruba od dvanácti do pětadvaceti let, kteří chtějí klidnou hru bez stresu, a dvojice kamarádů – hra se dá hrát ve dvou na jednom počítači, jak vidíte na snímku. Hráčům dá odpočinek, jasný cíl a příběh, a to bez registrace a bez internetu.',
  'Kdyby se ptali „proč zrovna 12–25“: hra je bez násilí na lidech (souboje lodí), jednoduché ovládání, delší příběh ocení i starší hráči.');

// ═════════ 3 Rozsah a MVP ═════════
pres.addSection({ title: 'Rozsah' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Rozsah' });
s.addText('Rozsah: MVP, hotovo a co nebude', { placeholder: 'title' });
card(s, 0.5, 1.22, 9.0, 0.78, 'Karta MVP', TINT_GREEN);
circleNum(s, '✓', 0.7, 1.41, 0.4, 'MVP fajfka', C.accent3);
text(s, [
  { text: 'MVP: ', options: { bold: true, color: C.accent3 } },
  { text: 'hru jde dohrát od nové hry po oba konce příběhu, ukládá se a načítá a běží z hotového buildu.' },
], { x: 1.3, y: 1.22, w: 6.7, h: 0.78, fontSize: 15, color: C.text1, valign: 'middle', objectName: 'Definice MVP' });
pill(s, 'SPLNĚNO', 8.15, 1.45, 1.15, 0.32, C.accent3, 'Stav MVP');
const cols = [
  ['Hotovo', C.accent3, ['Nekonečný svět, ostrovy a majáky', 'Rybaření, těžba, obchody, questy', 'Souboje s piráty a ostrovy', 'Příběh: 3 ostrovy, 2 konce', 'Split-screen, CZ/EN, 3 sloty']],
  ['Zbývá do dubna', C.accent1, ['Dokumentace a obhajoba', 'Testovací protokol a opravy', 'Release na GitHubu', 'Uživatelská příručka']],
  ['Vědomě nebude', C.accent5, ['Síťový multiplayer', 'Mobilní verze', 'Vlastní 3D modely', 'Nové velké funkce (backlog)']],
];
cols.forEach((c, i) => {
  const x = 0.5 + i * 3.07;
  card(s, x, 2.18, 2.86, 2.78, 'Karta ' + c[0]);
  s.addShape(pres.shapes.OVAL, { x: x + 0.2, y: 2.38, w: 0.26, h: 0.26, fill: { color: c[1] }, line: noLine(c[1]), objectName: 'Tečka ' + c[0] });
  text(s, c[0], { x: x + 0.58, y: 2.3, w: 2.15, h: 0.42, fontSize: 17, bold: true, color: C.text1, valign: 'middle', objectName: 'Nadpis ' + c[0] });
  text(s, c[2].map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < c[2].length - 1 } })), { x: x + 0.2, y: 2.85, w: 2.5, h: 2.0, fontSize: 14, color: C.text1, valign: 'top', paraSpaceAfter: 5, objectName: 'Seznam ' + c[0] });
});
link(s, S.DEMO, 0.5, 2.18, 2.86, 2.78, 'Odkaz Hotovo', 'Klikni: ukázka hotové hry');
nav(s, S.ROZSAH, 3);
note(s, '1:05–1:55  Rozsah jsem vymezil přes MVP: hru jde dohrát od nové hry po oba konce příběhu, ukládá se, načítá a běží z hotového buildu. To je splněné. Hotové je tedy jádro hry včetně soubojů, příběhu, split-screenu, češtiny a angličtiny. Zbývá to, co k projektu patří okolo: dokumentace, testování, release a příručka. A vědomě jsem vyškrtl síťový multiplayer, mobilní verzi a vlastní 3D modely – každá z těch věcí by sama zabrala víc času, než do dubna mám. Nové nápady jdou do backlogu.',
  'Klik na kartu „Hotovo“ skočí na ukázku hry. Build ověřen 30. 9. (Windows, ~97 MB). Kdyby se ptali na síťový multiplayer: plán existuje (.claude/network-multiplayer-plan.md), ale je to záměrně mimo rozsah.');

// ═════════ 4 Architektura (klikací schéma z tvarů) ═════════
pres.addSection({ title: 'Technika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Technika' });
s.addText('Architektura a technologie', { placeholder: 'title' });
const layer = (x, y, w, h, head, body, fill, border, name, target) => {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.08, fill: { color: fill }, line: { color: border, width: 1.5 }, objectName: name });
  text(s, [
    { text: head, options: { bold: true, fontSize: 13, color: C.text1, breakLine: true } },
    { text: body, options: { fontSize: 11, color: C.accent5 } },
  ], { x: x + 0.12, y, w: w - 0.24, h, align: 'center', valign: 'middle', objectName: name + ' text' });
  if (target) link(s, target, x, y, w, h, name + ' odkaz', 'Detail: ' + head);
};
const vArrow = (x, y1, y2, name) => s.addShape(pres.shapes.LINE, { x, y: y1, w: 0, h: y2 - y1, line: { color: C.accent5, width: 1.5, beginArrowType: 'triangle', endArrowType: 'triangle' }, objectName: name });
layer(0.5, 1.22, 5.0, 0.58, 'Rozhraní (UI)', 'HUD a hotbar · minimapa · menu · obchody · dialogy', C.background2, C.accent2, 'Vrstva UI', S.D_UI);
vArrow(1.71, 1.82, 2.0, 'Šipka UI-logika L'); vArrow(4.29, 1.82, 2.0, 'Šipka UI-logika P');
layer(0.5, 2.02, 2.42, 0.82, 'Herní logika – moře', 'GridManager · PlayerController · CombatDirector · příběh', C.background2, C.accent2, 'Vrstva logika moře', S.D_LOGIKA);
layer(3.08, 2.02, 2.42, 0.82, 'Herní logika – maják', 'LighthouseManager · obchody a výkupna', C.background2, C.accent2, 'Vrstva logika maják', S.D_LOGIKA);
vArrow(1.71, 2.86, 3.04, 'Šipka logika-stav L'); vArrow(4.29, 2.86, 3.04, 'Šipka logika-stav P');
layer(0.5, 3.06, 5.0, 0.58, 'Stav hry – jeden zdroj pravdy', 'GameSession → GameData (oba hráči, svět, příběh)', TINT_AMBER, C.accent1, 'Vrstva stav', S.D_STAV);
vArrow(1.71, 3.66, 3.84, 'Šipka stav-ukládání');
layer(0.5, 3.86, 2.42, 0.58, 'Ukládání', 'SaveManager → save_0–2.json', TINT_GREEN, C.accent3, 'Vrstva ukládání', S.D_SAVE);
layer(3.08, 3.86, 2.42, 0.58, 'Podpora', 'Loc (CZ/EN) · zvuk · konzole', C.background2, C.accent5, 'Vrstva podpora', null);
text(s, 'Klikni na vrstvu schématu pro detail', { x: 0.5, y: 4.55, w: 5.0, h: 0.28, fontSize: 12, italic: true, color: C.accent5, objectName: 'Nápověda schéma' });
text(s, 'Proč tenhle stack', { x: 5.95, y: 1.22, w: 3.55, h: 0.36, fontSize: 16, bold: true, color: C.accent2, objectName: 'Nadpis stack' });
[
  ['Unity 6 + C# + URP', 'Unity a C# znám. Engine řeší 3D, světlo i víc kamer pro split-screen.'],
  ['Offline, bez serveru', 'Backend netřeba: 0 Kč provoz a žádná osobní data hráčů.'],
  ['JSON místo databáze', 'Celý stav je jeden objekt GameData – uloží se jedním voláním, 3 sloty.'],
].forEach((a, i) => {
  const y = 1.66 + i * 1.12;
  circleNum(s, i + 1, 5.95, y + 0.02, 0.38, 'Stack číslo ' + (i + 1));
  text(s, a[0], { x: 6.48, y, w: 3.02, h: 0.32, fontSize: 15, bold: true, color: C.text1, objectName: 'Stack nadpis ' + (i + 1) });
  text(s, a[1], { x: 6.48, y: y + 0.33, w: 3.02, h: 0.68, fontSize: 14, color: C.accent5, valign: 'top', objectName: 'Stack popis ' + (i + 1) });
});
nav(s, S.ARCH, 4);
note(s, '1:55–2:55  Technicky je to jedna offline aplikace v Unity 6 a C#. Unity a C# znám, engine za mě řeší 3D, světlo i víc kamer pro split-screen, a protože hra nepotřebuje server, nemá žádné provozní náklady ani osobní data. Architektura je ve vrstvách: nahoře rozhraní, pod ním herní logika – svět, hráč, souboje a příběh, a zvlášť scéna majáku s obchody. Pod tím je jediný zdroj pravdy, objekt GameData, a úplně dole ukládání do JSON souborů ve třech slotech. Databázi nepotřebuju, protože celý stav je jeden objekt, který se uloží jedním voláním. Kdybyste chtěli detail, kliknu na vrstvu.',
  'Klikací vrstvy: UI, logika (obě krabice), stav, ukládání → skryté slidy 14–17, z každého vede tlačítko zpět. Proč ne Godot/Unreal (kdyby se ptali): Unity znám, má největší komunitu a dokumentaci a pro malou 3D hru je Unreal zbytečně těžký.');

// ═════════ 5 Tok dat (jak spolu části komunikují) ═════════
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Technika' });
s.addText('Jak spolu části mluví: hráč chytí rybu', { placeholder: 'title' });
const STEPS = [
  ['Vstup', 'PlayerController', 'Mezerník spustí rybaření (FishingRoutine).'],
  ['Stav', 'GameData', 'Přičte rybu hráči a posune rozdělaný quest.'],
  ['Událost', 'OnWorldChanged', 'HUD a minimapa se samy překreslí – logika o UI neví.'],
  ['Uložení', 'saveDirty', 'Změna se jen označí, zápis nejvýš 1× za 8 s.'],
  ['Zápis na disk', 'SaveManager', 'JSON na pozadí přes dočasný soubor → save_N.json.'],
];
STEPS.forEach((st, i) => {
  const x = 0.5 + i * 1.845, w = 1.62;
  card(s, x, 1.22, w, 2.28, 'Krok ' + (i + 1));
  circleNum(s, i + 1, x + 0.15, 1.36, 0.38, 'Krok číslo ' + (i + 1), i === 3 ? C.accent1 : C.accent2, i === 3 ? C.text1 : C.background1);
  text(s, st[0], { x: x + 0.15, y: 1.82, w: w - 0.3, h: 0.5, fontSize: 14, bold: true, color: C.text1, valign: 'top', objectName: 'Krok název ' + (i + 1) });
  text(s, st[1], { x: x + 0.08, y: 2.3, w: w - 0.16, h: 0.28, fontFace: 'Courier New', fontSize: 10, bold: true, color: C.accent2, objectName: 'Krok třída ' + (i + 1) });
  text(s, st[2], { x: x + 0.15, y: 2.6, w: w - 0.3, h: 0.85, fontSize: 12, color: C.text1, valign: 'top', objectName: 'Krok popis ' + (i + 1) });
  if (i < STEPS.length - 1) s.addShape(pres.shapes.RIGHT_ARROW, { x: x + w + 0.03, y: 2.24, w: 0.17, h: 0.22, fill: { color: C.accent5 }, line: noLine(C.accent5), objectName: 'Šipka ' + (i + 1) });
});
card(s, 0.5, 3.68, 4.42, 1.28, 'Karta výkon', TINT_AMBER);
text(s, '800 ms → 0,13 ms', { x: 0.7, y: 3.76, w: 4.0, h: 0.58, fontFace: 'Cambria', fontSize: 28, bold: true, color: C.text1, valign: 'middle', objectName: 'Číslo výkon' });
text(s, 'Synchronní zápis 5 MB savu zasekl hru při každé rybě. Teď se hra kvůli ukládání nezastaví.', { x: 0.7, y: 4.32, w: 4.05, h: 0.6, fontSize: 12, color: C.text1, valign: 'top', objectName: 'Popis výkon' });
card(s, 5.08, 3.68, 4.42, 1.28, 'Karta testy', TINT_GREEN);
text(s, '26 automatických testů', { x: 5.28, y: 3.76, w: 4.0, h: 0.58, fontFace: 'Cambria', fontSize: 24, bold: true, color: C.text1, valign: 'middle', objectName: 'Číslo testy' });
text(s, 'Hlídají ekonomiku, lodě, data hráčů a převod starých savů na novou verzi.', { x: 5.28, y: 4.32, w: 4.05, h: 0.6, fontSize: 12, color: C.text1, valign: 'top', objectName: 'Popis testy' });
nav(s, S.TOK, 5);
note(s, '2:55–3:40  Jak spolu části mluví, ukážu na jedné akci – hráč chytí rybu. Mezerník spustí v PlayerControlleru rybaření a po pár sekundách se v GameData přičte ryba. GridManager pak vyhlásí událost OnWorldChanged a HUD i minimapa se samy překreslí – herní logika o rozhraní vůbec neví. Ukládání je odložené: změna se jen označí a nejvýš jednou za osm sekund ji SaveManager zapíše na pozadí přes dočasný soubor. Dřív zápis pěti megabajtů zasekl hru zhruba na osm set milisekund, teď to na hlavním vlákně trvá 0,13 milisekundy. A dvacet šest automatických testů hlídá, že se data neporouchají.',
  'Přesně podle kódu: PlayerController.FishingRoutine → PFishCount (= GameData.players[i].fishCount) → gridManager.NotifyWorldChanged() → odběratelé HUDCounter a MinimapUIRenderer. Ukládání: GridManager.Save() jen nastaví saveDirty; Update zapíše nejvýš 1× za SAVE_INTERVAL = 8 s, navíc při ukončení a ztrátě okna; SaveManager.SaveGameAsync píše přes .tmp + File.Replace. Čísla 800 ms / 0,13 ms jsou z měření v ročníkové práci (tabulka optimalizací). Testy: 22 EditMode + 4 PlayMode.');

// ═════════ 6–9 Ukázka: galerie se zvětšováním (Morph) ═════════
pres.addSection({ title: 'Ukázka' });
const GAL = [
  { key: 'deda', file: 'shot_deda.jpg', step: 'Děda zadá úkol', alt: 'Startovní ostrov, dialog s dědou' },
  { key: 'obchod', file: 'shot_obchod.jpg', step: 'Maják: obchod', alt: 'Obchod s vylepšeními v majáku' },
  { key: 'souboj', file: 'shot_souboj.jpg', step: 'Souboj s piráty', alt: 'Souboj s pirátskou lodí a ukazatel jejího zdraví' },
  { key: 'pevnost', file: 'shot_pevnost.jpg', step: 'Pirátská pevnost', alt: 'Ostrov pirátů s hradbami a věžemi' },
];
const BIG = { x: 0.5, y: 1.22, w: 5.4, h: 3.04 }, TW = 1.1, TH = 0.62;
GAL.forEach((cur, st) => {
  const s2 = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Ukázka' });
  s2.addText('Ukázka hry', { placeholder: 'title' });
  let t = 0;
  GAL.forEach((g, gi) => {
    const isBig = gi === st;
    const pos = isBig ? BIG : { x: 0.5 + t * (TW + 0.08), y: 4.36, w: TW, h: TH };
    if (!isBig) t++;
    s2.addImage({ path: img(g.file), x: pos.x, y: pos.y, w: pos.w, h: pos.h, objectName: '!!img_' + g.key, altText: g.alt });
    if (!isBig) link(s2, S.DEMO + gi, pos.x, pos.y, pos.w, pos.h, 'Odkaz náhled ' + g.key, 'Zvětšit: ' + g.step);
  });
  GAL.forEach((g, gi) => {
    const y = 1.3 + gi * 0.78, on = gi === st;
    circleNum(s2, gi + 1, 6.3, y, 0.46, 'Krok ' + (gi + 1), on ? C.accent1 : C.accent2, on ? C.text1 : C.background1);
    text(s2, g.step, { x: 6.95, y, w: 2.55, h: 0.46, fontSize: 16, bold: on, color: on ? C.text1 : C.accent5, valign: 'middle', objectName: 'Krok text ' + (gi + 1) });
    if (!on) link(s2, S.DEMO + gi, 6.3, y, 3.2, 0.46, 'Odkaz krok ' + (gi + 1), 'Zvětšit: ' + g.step);
  });
  text(s2, 'Klikni na náhled nebo na krok', { x: 6.3, y: 4.5, w: 3.2, h: 0.3, fontSize: 12, italic: true, color: C.accent5, objectName: 'Nápověda galerie' });
  nav(s2, S.DEMO, 6);
  if (st === 0) {
    note(s2, '3:40–4:40  Teď krátce, jak to vypadá ve hře. Začíná se u dědy na startovním ostrově, který vám zadá první úkol. (klik) V majáku prodáváte úlovky a kupujete vylepšení – lepší loď, prut nebo zbraň, a ceny se liší ostrov od ostrova. (klik) Na moři vás napadají piráti od malých po velké lodě, tady s ukazatelem zdraví jako v boss fightu. (klik) A tohle je první příběhový ostrov – pirátská pevnost s hradbami a děly na věžích, kterou musíte dobýt.',
      'Klikej na náhledy/kroky zprava, vybraný snímek se plynule zvětší (Morph – PowerPoint 2019+, jinak prolnutí). Kdybys natočil video (Win+Alt+R), dá se sem vložit místo galerie.');
  } else {
    note(s2, 'Galerie – zvětšený snímek: ' + cur.step + '. Klikni na další náhled nebo pokračuj šipkou.');
  }
});

// ═════════ 10 Rizika ═════════
pres.addSection({ title: 'Rizika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Rizika' });
s.addText('Rizika a jak je řeším', { placeholder: 'title' });
const LVL = { vysoké: C.accent4, střední: C.accent1, nízké: C.accent3 };
[
  ['Čas', 'vysoké', 'Funkce zmrazené, MVP hotové. Zbývá dokumentace podle milníků.'],
  ['Technologie', 'střední', 'Chyby až v buildu (shadery) → pravidelný testovací build.'],
  ['Výkon', 'střední', 'Svět jen kolem hráče, odložený zápis. Měření na slabším PC.'],
  ['Ztráta dat', 'nízké', 'Git a pull requesty, bezpečný zápis savu, testy migrace.'],
  ['Bezpečnost a GDPR', 'nízké', 'Offline, bez účtů a sítě, osobní údaje hra nesbírá.'],
  ['Autorská práva', 'nízké', 'Jen modely CC0 s licencí, vlastní kód MIT, zvuky z kódu.'],
].forEach((r, i) => {
  const col = i % 2, row = Math.floor(i / 2);
  const x = 0.5 + col * 4.58, y = 1.22 + row * 1.2, w = 4.42;
  card(s, x, y, w, 1.06, 'Riziko ' + r[0]);
  text(s, r[0], { x: x + 0.2, y: y + 0.1, w: 2.9, h: 0.36, fontSize: 15, bold: true, color: C.text1, valign: 'middle', objectName: 'Riziko název ' + (i + 1) });
  pill(s, r[1], x + w - 1.22, y + 0.13, 1.02, 0.3, LVL[r[1]], 'Riziko úroveň ' + (i + 1));
  text(s, r[2], { x: x + 0.2, y: y + 0.5, w: w - 0.4, h: 0.5, fontSize: 14, color: C.text1, valign: 'top', objectName: 'Riziko řešení ' + (i + 1) });
});
text(s, 'Úroveň = pravděpodobnost × dopad', { x: 0.5, y: 4.82, w: 5, h: 0.24, fontSize: 11, italic: true, color: C.accent5, objectName: 'Legenda rizik' });
nav(s, S.RIZIKA, 7);
note(s, '4:40–5:40  Rizika hodnotím podle pravděpodobnosti a dopadu. Největší je čas – proto jsou funkce zmrazené a zbývá dokumentace podle milníků. Technologické riziko jsou chyby, které se ukážou až v buildu – přesně to se mi stalo s hledáním shaderů, takže dělám pravidelný testovací build. Výkon hlídám tím, že svět žije jen kolem hráče a ukládání je odložené. Ztrátu dat řeší Git a bezpečný zápis savu, který kontrolují testy. Bezpečnost a GDPR: hra je offline, bez účtů a bez sítě a osobní údaje nesbírá. A autorská práva – používám jen modely s licencí CC0, vlastní kód je pod MIT a zvuky si hra generuje sama.',
  'GDPR přesně: můj kód nikde nekomunikuje se sítí a Unity Analytics je vypnuté. Engine Unity u bezplatné licence posílá anonymní statistiky hardwaru (nejdou vypnout) – kdyby se ptali, řekni to takhle. Bezpečnost: vývojářská konzole s cheaty funguje i v buildu – do release ji schovám. Licence: CC0 = public domain (Kenney, Quaternius), evidence v THIRD_PARTY_LICENSES.md.');

// ═════════ 11 Business ═════════
pres.addSection({ title: 'Business' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Business' });
s.addText('Přínos a model', { placeholder: 'title' });
[
  ['Model', 'B2C, zdarma a open-source (MIT). Výsledek slouží jako portfolio.'],
  ['Hodnota', 'Hra ve dvou na jednom PC bez internetu. Kód jako ukázka pro studenty Unity.'],
  ['Udržitelnost', 'Bez serveru = 0 Kč provoz. Vydání na GitHubu a itch.io, krátká videa.'],
].forEach((b, i) => {
  const y = 1.22 + i * 1.2;
  circleNum(s, i + 1, 0.5, y + 0.02, 0.38, 'Business číslo ' + (i + 1));
  text(s, b[0], { x: 1.05, y, w: 3.4, h: 0.34, fontSize: 16, bold: true, color: C.accent2, objectName: 'Business nadpis ' + (i + 1) });
  text(s, b[1], { x: 1.05, y: y + 0.36, w: 3.45, h: 0.8, fontSize: 14, color: C.text1, valign: 'top', objectName: 'Business text ' + (i + 1) });
});
const th = (t) => ({ text: t, options: { bold: true, color: HEX.lt1, fill: { color: HEX.dk1 }, align: 'center' } });
const td = (t, opt) => ({ text: t, options: Object.assign({ color: HEX.dk1, align: 'center' }, opt || {}) });
const yes = (hl) => td('✓', { bold: true, color: HEX.accent3, fill: hl ? { color: TINT_GREEN } : undefined });
const no = () => td('✗', { color: HEX.accent4 });
s.addTable([
  [th('Hra'), th('Cena'), th('Bez internetu'), th('2 hráči / 1 PC')],
  [td('Sea of Thieves', { align: 'left' }), td('placená'), no(), no()],
  [td('Raft', { align: 'left' }), td('placená'), yes(), no()],
  [td('Dredge', { align: 'left' }), td('placená'), yes(), no()],
  [td('The Last Beacon', { align: 'left', bold: true, fill: { color: TINT_GREEN } }), td('zdarma', { bold: true, fill: { color: TINT_GREEN } }), yes(true), yes(true)],
], { x: 4.85, y: 1.22, w: 4.65, colW: [1.55, 0.95, 1.05, 1.1], rowH: 0.46, fontFace: 'Calibri', fontSize: 13, valign: 'middle', border: { type: 'solid', pt: 0.75, color: 'D5E2E9' }, objectName: 'Srovnání her' });
text(s, 'Mezera na trhu: klidná hra pro dva na jednom počítači, zdarma a offline.', { x: 4.85, y: 3.6, w: 4.65, h: 0.6, fontSize: 14, bold: true, color: C.accent2, valign: 'top', objectName: 'Mezera na trhu' });
text(s, 'Srovnání podle veřejných informací o hrách (11/2026).', { x: 4.85, y: 4.25, w: 4.65, h: 0.26, fontSize: 10, italic: true, color: C.accent5, objectName: 'Zdroj srovnání' });
nav(s, S.BUSINESS, 8);
note(s, '5:40–6:15  Projekt není komerční. Je to hra pro koncové hráče, zdarma a open-source pod licencí MIT – výsledek slouží jako portfolio a jako ukázka pro studenty, jak postavit větší hru v Unity. Ve srovnání s podobnými hrami: Sea of Thieves, Raft i Dredge jsou placené a žádná nenabízí hru ve dvou na jednom počítači bez internetu – to je moje mezera. Distribuce přes GitHub a itch.io a provoz nestojí nic, protože není žádný server.',
  'Fakta ke srovnání: Sea of Thieves vyžaduje připojení k internetu i pro hru o samotě; Raft má co-op jen online; Dredge je pouze pro jednoho hráče. Kdyby chtěli monetizaci: dobrovolný příspěvek na itch.io („zaplať, kolik chceš“), ale primární cíl je portfolio, ne zisk.');

// ═════════ 12 Repozitář, řízení a plán ═════════
pres.addSection({ title: 'Plán' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Plán' });
s.addText('Repozitář, řízení a plán do dubna', { placeholder: 'title' });
card(s, 0.5, 1.22, 5.2, 2.55, 'Karta repozitář');
text(s, 'GitHub repozitář', { x: 0.7, y: 1.3, w: 4.8, h: 0.34, fontSize: 15, bold: true, color: C.accent2, objectName: 'Nadpis repozitář' });
const TREE = [
  ['README.md', 'popis, spuštění, ovládání'],
  ['docs/', 'charter, práce, příručka, prezentace'],
  ['Assets/Scripts/', '59 skriptů v C#'],
  ['Assets/Tests/', 'automatické testy'],
  ['LICENSE', 'MIT pro vlastní kód'],
  ['THIRD_PARTY_LICENSES.md', 'evidence cizích assetů'],
];
text(s, TREE.flatMap((r, i) => [
  { text: r[0], options: { fontFace: 'Courier New', bold: true, color: C.text1 } },
  { text: '  ' + r[1], options: { color: C.accent5, breakLine: i < TREE.length - 1 } },
]), { x: 0.7, y: 1.7, w: 4.85, h: 1.98, fontSize: 12, valign: 'top', paraSpaceAfter: 4, objectName: 'Struktura repozitáře' });
[['224', 'commitů'], ['21', 'pull requestů'], ['26', 'automatických testů']].forEach((st, i) => {
  const y = 1.22 + i * 0.87;
  card(s, 5.9, y, 3.6, 0.77, 'Statistika ' + st[1]);
  text(s, st[0], { x: 6.05, y, w: 1.1, h: 0.77, fontFace: 'Cambria', fontSize: 26, bold: true, color: C.accent2, align: 'right', valign: 'middle', objectName: 'Statistika číslo ' + (i + 1) });
  text(s, st[1], { x: 7.3, y, w: 2.1, h: 0.77, fontSize: 14, color: C.text1, valign: 'middle', objectName: 'Statistika popis ' + (i + 1) });
});
const ms = [['11. 11.', 'Charter'], ['9. 12.', 'Osnova'], ['20. 1.', 'Návrh'], ['3. 3.', 'Kód'], ['31. 3.', 'Build'], ['14. 4.', 'Release']];
s.addShape(pres.shapes.LINE, { x: 1.1, y: 4.46, w: 7.8, h: 0, line: { color: C.accent5, width: 2 }, objectName: 'Osa' });
ms.forEach((m, i) => {
  const cx = 1.1 + i * 1.56;
  const fill = i === 0 ? C.accent3 : i === ms.length - 1 ? C.accent1 : C.accent2;
  s.addShape(pres.shapes.OVAL, { x: cx - 0.15, y: 4.31, w: 0.3, h: 0.3, fill: { color: fill }, line: { color: C.background1, width: 2 }, objectName: 'Milník ' + (i + 1) });
  text(s, m[0], { x: cx - 0.7, y: 3.98, w: 1.4, h: 0.28, fontSize: 13, bold: true, color: C.text1, align: 'center', objectName: 'Datum ' + (i + 1) });
  text(s, m[1], { x: cx - 0.7, y: 4.66, w: 1.4, h: 0.28, fontSize: 13, color: C.accent5, align: 'center', objectName: 'Název ' + (i + 1) });
});
nav(s, S.PLAN, 9);
note(s, '6:15–6:50  Projekt řídím přes GitHub: každá větší změna jde přes vlastní větev a pull request – zatím dvacet jedna pull requestů a přes dvě stě dvacet commitů – a dvacet šest automatických testů. V repozitáři je README, složka docs s dokumentací a licence. Plán: devátého prosince osnova dokumentace, dvacátého ledna datový model a wireframy, třetího března úklid kódu, třicátého prvního března testovací build a čtrnáctého dubna finální release a obhajoba.',
  'Čísla ověřena 10. 11. 2026: 224 commitů (git rev-list), 21 sloučených PR (gh pr list), 26 testů (22 EditMode + 4 PlayMode), 59 skriptů v Assets/Scripts. Zelený milník = dnešek.');

// ═════════ 13 Dotazy – rozcestník ═════════
pres.addSection({ title: 'Otázky' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Otázky' });
s.addText('Děkuji za pozornost · dotazy?', { placeholder: 'title' });
text(s, 'Klikni na téma, ke kterému se komise ptá', { x: 0.5, y: 1.15, w: 9, h: 0.35, fontSize: 15, color: C.accent5, objectName: 'Podtitul dotazy' });
[
  ['Co a pro koho', S.TEMA], ['Rozsah a MVP', S.ROZSAH], ['Architektura', S.ARCH], ['Tok dat', S.TOK],
  ['Ukázka hry', S.DEMO], ['Rizika', S.RIZIKA], ['Business', S.BUSINESS], ['Repo a plán', S.PLAN],
].forEach((b, i) => {
  const col = i % 4, row = Math.floor(i / 4);
  const x = 0.5 + col * 2.29, y = 1.72 + row * 1.55, w = 2.13;
  card(s, x, y, w, 1.3, 'Dlaždice ' + b[0]);
  circleNum(s, i + 1, x + 0.18, y + 0.46, 0.38, 'Číslo dlaždice ' + (i + 1));
  text(s, b[0], { x: x + 0.66, y: y + 0.1, w: w - 0.76, h: 1.1, fontSize: 15, bold: true, color: C.text1, valign: 'middle', objectName: 'Text dlaždice ' + (i + 1) });
  link(s, b[1], x, y, w, 1.3, 'Odkaz dlaždice ' + (i + 1), 'Přejít: ' + b[0]);
});
nav(s, S.DOTAZY, 10);
note(s, '6:50–7:00  Děkuji za pozornost, rád odpovím na vaše dotazy.',
  'Pravděpodobné dotazy (rychlé odpovědi): Proč JSON a ne databáze? – stav je jeden objekt, hra je offline. Proč je moře jedna plocha? – tisíce dlaždic by hru zpomalily. Jak vzniká ostrov? – jádro + náhodné rozrůstání + zaplnění zálivů, každých 40 políček s 30% šancí. Co se stane se starým savem? – saveVersion + migrace, hlídá to 6 testů. Jak funguje split-screen? – druhý hráč je kopie prvního bez komponent, které mají být jednou; dvě kamery. Jak testuješ? – 26 automatických testů, ruční scénáře, konzole s presety příběhu, testovací build.');

// ═════════ 14–17 Detaily architektury (skryté) ═════════
pres.addSection({ title: 'Příloha: detaily' });
const DETAILS = [
  { title: 'Detail: rozhraní (UI)', head: 'Rozhraní (UI)', color: C.accent2,
    bullets: ['Menu, pauza, obchody, dialogy a dopis: IMGUI (OnGUI)', 'HUD, hotbar a minimapa: runtime uGUI', 'Kruhová minimapa s kompasem a šipkou k cíli', 'Čeština a angličtina přes jednu třídu Loc'],
    classes: ['HUDCounter', 'MinimapUIRenderer', 'MainMenuManager', 'PauseMenu', 'ShopUI', 'MapScreen', 'LetterScreen'] },
  { title: 'Detail: herní logika', head: 'Herní logika', color: C.accent2,
    bullets: ['Svět se generuje jen kolem hráče (28 × 28 dlaždic)', 'Ostrovy: organický tvar, molo, maják, 40 % nepřátelských', 'Souboje: piráti, děla ostrovů, boss, náměr kamerou', 'Příběh: tři mega ostrovy a dva konce', 'Maják: výkupna a obchody ve vlastní scéně'],
    classes: ['GridManager', 'PlayerController', 'CombatDirector', 'PirateShip', 'MegaIslandMarker', 'StoryNpc', 'LighthouseManager'] },
  { title: 'Detail: stav hry', head: 'Stav hry', color: C.accent1,
    bullets: ['Veškerý ukládaný stav je v jednom objektu GameData', 'GameSession přežije přechod moře ↔ maják', 'Data obou hráčů v poli players[2]', 'Čísla ekonomiky na jednom místě (EconomyConfig)', 'Hodnoty TileType se nepřečíslovávají (staré savy)'],
    classes: ['GameSession', 'GameData', 'PlayerState', 'EconomyConfig', 'TileType'] },
  { title: 'Detail: ukládání', head: 'Ukládání', color: C.accent3,
    bullets: ['JSON ve složce hráče, 3 nezávislé sloty', 'Zápis na pozadí přes dočasný soubor a File.Replace', 'Nejvýš 1× za 8 s, navíc při ukončení a ztrátě okna', 'Starý save převede migrace podle saveVersion'],
    classes: ['SaveManager', 'JsonUtility', 'save_0.json', 'save_1.json', 'save_2.json'] },
];
DETAILS.forEach((d) => {
  const s3 = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Příloha: detaily' });
  s3.addText(d.title, { placeholder: 'title' });
  card(s3, 0.5, 1.22, 5.6, 3.25, 'Karta detail');
  s3.addShape(pres.shapes.OVAL, { x: 0.75, y: 1.44, w: 0.26, h: 0.26, fill: { color: d.color }, line: noLine(d.color), objectName: 'Tečka vrstvy' });
  text(s3, d.head, { x: 1.15, y: 1.37, w: 4.8, h: 0.4, fontSize: 18, bold: true, color: C.text1, valign: 'middle', objectName: 'Název vrstvy' });
  text(s3, d.bullets.map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < d.bullets.length - 1 } })), { x: 0.75, y: 1.88, w: 5.1, h: 2.5, fontSize: 15, color: C.text1, valign: 'top', paraSpaceAfter: 8, objectName: 'Seznam detail' });
  text(s3, 'Hlavní třídy a soubory', { x: 6.4, y: 1.22, w: 3.1, h: 0.35, fontSize: 16, bold: true, color: C.accent2, objectName: 'Nadpis tříd' });
  d.classes.forEach((c, k) => {
    const y = 1.66 + k * 0.4;
    s3.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 6.4, y, w: 3.1, h: 0.33, rectRadius: 0.08, fill: { color: C.background2 }, line: noLine(C.background2), objectName: 'Třída ' + c });
    text(s3, c, { x: 6.52, y, w: 2.9, h: 0.33, fontFace: 'Courier New', fontSize: 12, color: C.text1, valign: 'middle', objectName: 'Třída text ' + c });
  });
  s3.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 0.5, y: 4.6, w: 2.8, h: 0.4, rectRadius: 0.1, fill: { color: C.accent2 }, line: noLine(C.accent2), objectName: 'Tlačítko zpět' });
  text(s3, '← Zpět na architekturu', { x: 0.5, y: 4.6, w: 2.8, h: 0.4, fontSize: 14, bold: true, color: C.background1, align: 'center', valign: 'middle', objectName: 'Tlačítko zpět text' });
  link(s3, S.ARCH, 0.5, 4.6, 2.8, 0.4, 'Odkaz zpět', 'Zpět na architekturu');
  nav(s3, S.ARCH, 4);
  note(s3, 'Detail vrstvy architektury (skrytý slide). Zpět tlačítkem „Zpět na architekturu“ nebo lištou dole.');
});

// ── Zápis + přechody a skryté slidy ─────────────────────────
const FADE = '<p:transition spd="med"><p:fade/></p:transition>';
const MORPH_XML = '<mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"><mc:Choice xmlns:p159="http://schemas.microsoft.com/office/powerpoint/2015/09/main" Requires="p159"><p:transition spd="slow"><p159:morph option="byObject"/></p:transition></mc:Choice><mc:Fallback><p:transition spd="slow"><p:fade/></p:transition></mc:Fallback></mc:AlternateContent>';
async function postProcess() {
  const zip = await JSZip.loadAsync(fs.readFileSync(OUT));
  const names = Object.keys(zip.files).filter((n) => /^ppt\/slides\/slide\d+\.xml$/.test(n));
  for (const n of names) {
    const idx = parseInt(n.match(/slide(\d+)\.xml/)[1], 10);
    let x = await zip.file(n).async('string');
    const trans = MORPH.includes(idx) ? MORPH_XML : FADE;
    if (!x.includes('<p:transition')) {
      if (x.includes('</p:clrMapOvr>')) x = x.replace('</p:clrMapOvr>', '</p:clrMapOvr>' + trans);
      else x = x.replace('</p:cSld>', '</p:cSld>' + trans);
    }
    if (HIDDEN.includes(idx)) x = x.replace(/<p:sld /, '<p:sld show="0" ');
    zip.file(n, x);
  }
  // Přebal bez položek složek ("ppt/" apod.) — s nimi soubor občas nejde otevřít přes cloud (OneDrive/Teams).
  const clean = new JSZip();
  for (const n of Object.keys(zip.files)) {
    if (zip.files[n].dir) continue;
    clean.file(n, await zip.file(n).async('uint8array'), { createFolders: false });
  }
  fs.writeFileSync(OUT, await clean.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  await postProcess();
  console.log('OK ' + OUT);
})();
