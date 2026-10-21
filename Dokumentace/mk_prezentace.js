// Generuje Prezentace_The_Last_Beacon.pptx (čtvrtletní prezentace 11. 11., 7 minut)
// INTERAKTIVNÍ: klikání funguje jen v režimu prezentace (F5).
//   - navigační lišta dole (skok na libovolnou část),
//   - slide 5: galerie, klik na náhled / krok zvětší vybraný snímek (přechod Morph),
//   - slide 4: klikací schéma architektury (klik na vrstvu = detail),
//   - slide 3: karta „Hotovo“ skočí na ukázku,
//   - slide 8: rozcestník pro dotazy.
// Skryté slidy (galerie, detaily architektury) se při běžném listování přeskakují.
// Spuštění: NODE_PATH=<složka s node_modules> node mk_prezentace.js
const pptxgen = require('pptxgenjs');
const path = require('path');
const fs = require('fs');
const SKILL = process.env.SKILL_PPTX || 'C:/Users/matix/AppData/Local/Claude-Data/claude_pzs8sxrjxfjjc/local-agent-mode-sessions/skills-plugin/052f3edc-54b3-4acf-942f-b58b343f6d1b/679b4655-be0e-43be-b6dc-ff9660aa7ac7/skills/pptx';
const { applyTheme } = require(SKILL + '/scripts/apply_theme.js');
const JSZip = require(require.resolve('jszip', { paths: [path.dirname(require.resolve('pptxgenjs'))] }));

const OUT = path.join(__dirname, 'Prezentace_The_Last_Beacon.pptx');
const THEME = {
  name: 'The Last Beacon', headFontFace: 'Cambria', bodyFontFace: 'Calibri',
  colors: { dk1: '0B2A43', lt1: 'FFFFFF', dk2: '1C7293', lt2: 'EAF2F6', accent1: 'F2A93B', accent2: '1C7293', accent3: '2E8B6F', accent4: 'C8553D', accent5: '5F7482', accent6: '0B2A43', hlink: '1C7293', folHlink: '5F7482' },
};

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10" x 5.625"
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = 'The Last Beacon – čtvrtletní prezentace';
pres.author = 'Martin Štěpánek';
const C = pres.SchemeColor;
const IMG = path.join(__dirname, 'obrazky');

// ── Pořadí slidů (čísla pro odkazy) ────────────────────────
const S = { TITLE: 1, TEMA: 2, ROZSAH: 3, ARCH: 4, DEMO: 5, DEMO_FIRST: 5, RIZIKA: 9, PLAN: 10, DOTAZY: 11, D_UI: 12, D_LOGIKA: 13, D_STAV: 14, D_SAVE: 15 };
const HIDDEN = [6, 7, 8, 12, 13, 14, 15];
const MORPH = [6, 7, 8];

pres.defineSlideMaster({ title: 'TITULNI', background: { color: THEME.colors.dk1 }, objects: [] });
pres.defineSlideMaster({
  title: 'OBSAH', background: { color: 'FFFFFF' },
  objects: [{ placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 0.3, w: 9, h: 0.8, fontFace: 'Cambria', fontSize: 34, bold: true, color: C.text1, margin: 0, valign: 'middle', align: 'left' }, text: '' } }],
});

// ── Pomocné funkce ─────────────────────────────────────────
const TRANSP = 'image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAC0lEQVR4nGNgQAcAABIAAXfx+gAAAAAASUVORK5CYII=';
// Průhledná klikací plocha (obrázek s odkazem na slide) – celá plocha reaguje na klik.
const link = (s, target, x, y, w, h, name, tip) => s.addImage({ data: TRANSP, x, y, w, h, hyperlink: { slide: target, tooltip: tip }, objectName: name, altText: tip });
const noLine = (c) => ({ color: c, width: 0 });
const circleNum = (s, n, x, y, d, name, fill, txt) => {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill || C.accent2 }, line: noLine(fill || C.accent2), objectName: name });
  s.addText(String(n), { x, y, w: d, h: d, align: 'center', valign: 'middle', fontSize: 14, bold: true, color: txt || C.background1, margin: 0, isTextBox: true, objectName: name + ' text' });
};
const NAV = [['Téma', S.TEMA], ['Rozsah', S.ROZSAH], ['Technika', S.ARCH], ['Ukázka', S.DEMO], ['Rizika', S.RIZIKA], ['Plán', S.PLAN], ['Otázky', S.DOTAZY]];
function nav(s, activeIdx, pageNo) {
  NAV.forEach((n, i) => {
    const x = 0.5 + i * 1.06, active = n[1] === activeIdx;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 5.14, w: 1.0, h: 0.28, rectRadius: 0.08, fill: { color: active ? C.accent2 : C.background2 }, line: noLine(active ? C.accent2 : C.background2), objectName: 'Nav ' + n[0] });
    s.addText(n[0], { x, y: 5.14, w: 1.0, h: 0.28, fontSize: 11, bold: active, color: active ? C.background1 : C.text1, align: 'center', valign: 'middle', margin: 0, isTextBox: true, objectName: 'Nav text ' + n[0] });
    link(s, n[1], x, 5.14, 1.0, 0.28, 'Nav odkaz ' + n[0], 'Přejít: ' + n[0]);
  });
  if (pageNo) s.addText(String(pageNo), { x: 9.2, y: 5.14, w: 0.3, h: 0.28, fontSize: 11, color: C.accent5, align: 'right', valign: 'middle', margin: 0, isTextBox: true, objectName: 'Číslo slidu' });
}
const WARN = '\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): ';
const note = (s, txt, mine) => s.addNotes(txt + (mine ? WARN + mine : ''));
const card = (s, x, y, w, h, name) => s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.12, fill: { color: C.background2 }, line: noLine(C.background2), objectName: name });

// ───────── 1 Titulní ─────────
pres.addSection({ title: 'Úvod' });
let s = pres.addSlide({ masterName: 'TITULNI', sectionTitle: 'Úvod' });
s.addShape(pres.shapes.OVAL, { x: 0.5, y: 0.55, w: 0.28, h: 0.28, fill: { color: C.accent1 }, line: noLine(C.accent1), objectName: 'Lucerna' });
s.addText('The Last Beacon', { x: 0.5, y: 1.1, w: 4.5, h: 1.0, fontFace: 'Cambria', fontSize: 36, bold: true, color: C.background1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Titulek' });
s.addText('3D hra o plavbě po nekonečném moři', { x: 0.5, y: 2.15, w: 4.3, h: 0.9, fontSize: 20, color: C.background2, margin: 0, valign: 'top', isTextBox: true, objectName: 'Podtitulek' });
s.addText([
  { text: 'Martin Štěpánek', options: { bold: true, breakLine: true } },
  { text: 'IT projekt, 4. ročník', options: { breakLine: true } },
  { text: 'Čtvrtletní prezentace, 11. 11. 2026' },
], { x: 0.5, y: 3.9, w: 4.3, h: 1.1, fontSize: 14, color: C.background2, margin: 0, valign: 'top', isTextBox: true, objectName: 'Autor' });
s.addImage({ path: path.join(IMG, 'ostrov.png'), x: 5.1, y: 1.45, w: 4.4, h: 2.1, objectName: 'Snímek ostrova', altText: 'Ostrov s majákem ve hře' });
s.addText('Unity 6 · C# · offline · 1–2 hráči', { x: 5.1, y: 3.7, w: 4.4, h: 0.4, fontSize: 14, color: C.accent1, margin: 0, isTextBox: true, objectName: 'Stack' });
note(s, '0:00–0:20  Představení. The Last Beacon je 3D hra v Unity o plavbě po nekonečném moři. Dnes vám za 7 minut ukážu: co hra je a pro koho, co je hotové, architekturu, krátké demo, rizika a licence, a plán do dubna.',
  'Snímek ostrova má vpravo nahoře testovací „Mince: 1098809“ (cheat). Vyměň za čistý screenshot z nové hry, na slidu to schovat nejde. OVLÁDÁNÍ: prezentace je interaktivní, klikání funguje jen v režimu prezentace (F5). Dole na každém slidu je navigační lišta, klikem skočíš kamkoli. Skryté slidy (galerie, detaily architektury) se při klasickém listování šipkou přeskakují.');

// ───────── 2 Téma ─────────
pres.addSection({ title: 'Téma' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Téma' });
s.addText('Co to je a pro koho', { placeholder: 'title' });
[
  ['Co to je', 'Plavba po nekonečném moři: rybaření, poklady z vraků, obchod v majácích, souboje s piráty a příběh.'],
  ['Pro koho', 'Hráči kolem 12–25 let, kteří chtějí klidnou hru, a dva kamarádi na jednom PC.'],
  ['Co přinese', 'Uvolnění, jasný cíl (lepší loď) a příběh. Bez registrace a bez internetu.'],
].forEach((b, i) => {
  const y = 1.3 + i * 1.22;
  s.addText(b[0], { x: 0.5, y, w: 4.5, h: 0.32, fontSize: 16, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis ' + (i + 1) });
  s.addText(b[1], { x: 0.5, y: y + 0.34, w: 4.5, h: 0.8, fontSize: 14, color: C.text1, margin: 0, valign: 'top', isTextBox: true, objectName: 'Text ' + (i + 1) });
});
s.addImage({ path: path.join(IMG, 'hud.png'), x: 5.4, y: 1.3, w: 4.1, h: 1.96, objectName: 'Snímek HUD', altText: 'Rozhraní hry: minimapa, zdraví a hotbar' });
card(s, 5.4, 3.5, 4.1, 1.3, 'Karta cíle');
s.addText([
  { text: 'Cíl práce', options: { bold: true, color: C.accent2, breakLine: true } },
  { text: 'Hratelný build, kód na GitHubu a kompletní dokumentace do 14. 4. 2027.' },
], { x: 5.6, y: 3.55, w: 3.7, h: 1.2, fontSize: 14, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Cíl práce' });
nav(s, S.TEMA, 2);
note(s, '0:20–1:20  Hra je klidná: plujete, rybaříte, těžíte poklady z vraků, v majácích prodáváte a vylepšujete loď. Do toho piráti a příběh starého námořníka. Cílovka jsou hráči zhruba 12–25 let a dvojice kamarádů, protože se hraje i split-screen na jednom PC. Přínos: uvolnění, jasný cíl, žádný stres. Mým cílem je ukázat celý postup tvorby většího softwaru až po release a dokumentaci. Bariéry cílovky: výkon na slabších PC a ovládání bez tutoriálu.',
  'V úkolu 1 máš „ztracený poklad“, ve hře je hlavní linka ztracený bratr (poklad je jen historický poklad). Na slidu příběh nerozvádím, rozhodni, co řekneš. Screenshot HUD má cheatové mince.');

// ───────── 3 Rozsah ─────────
pres.addSection({ title: 'Rozsah' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Rozsah' });
s.addText('Rozsah: hotovo a zbývá', { placeholder: 'title' });
const cards = [
  ['Hotovo', C.accent3, ['Nekonečný svět, ostrovy a majáky', 'Rybaření, těžba, obchody', 'Souboje s piráty a ostrovy', 'Příběh se dvěma konci', 'Split-screen, CZ/EN, 3 sloty']],
  ['Zbývá', C.accent1, ['Dokumentace a obhajoba', 'Testování a oprava chyb', 'Build a release na GitHubu', 'Uživatelská příručka']],
  ['Nebude', C.accent5, ['Síťový multiplayer', 'Mobilní verze', 'Vlastní 3D modely', 'Nové velké funkce']],
];
cards.forEach((c, i) => {
  const x = 0.5 + i * 3.07;
  card(s, x, 1.3, 2.86, 2.65, 'Karta ' + c[0]);
  s.addShape(pres.shapes.OVAL, { x: x + 0.2, y: 1.5, w: 0.26, h: 0.26, fill: { color: c[1] }, line: noLine(c[1]), objectName: 'Tečka ' + c[0] });
  s.addText(c[0], { x: x + 0.58, y: 1.42, w: 2.1, h: 0.42, fontSize: 18, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Nadpis ' + c[0] });
  s.addText(c[2].map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < c[2].length - 1 } })), { x: x + 0.2, y: 1.95, w: 2.5, h: 1.95, fontSize: 14, color: C.text1, margin: 0, valign: 'top', paraSpaceAfter: 4, isTextBox: true, objectName: 'Seznam ' + c[0] });
});
link(s, S.DEMO, 0.5, 1.3, 2.86, 2.65, 'Odkaz Hotovo', 'Klikni: ukázka hotové hry');
s.addText('Klikni na „Hotovo“ pro ukázku hry', { x: 0.5, y: 4.0, w: 2.86, h: 0.25, fontSize: 11, italic: true, color: C.accent5, margin: 0, isTextBox: true, objectName: 'Nápověda Hotovo' });
[['60+', 'skriptů v C#'], ['18 000', 'řádků kódu'], ['200+', 'commitů na GitHubu']].forEach((st, i) => {
  const x = 0.5 + i * 3.07;
  s.addText(st[0], { x, y: 4.3, w: 1.45, h: 0.7, fontFace: 'Cambria', fontSize: 30, bold: true, color: C.accent2, margin: 0, valign: 'middle', align: 'right', isTextBox: true, objectName: 'Číslo ' + (i + 1) });
  s.addText(st[1], { x: x + 1.6, y: 4.3, w: 1.35, h: 0.7, fontSize: 14, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Popisek ' + (i + 1) });
});
nav(s, S.ROZSAH, 3);
note(s, '1:20–2:20  MVP je hotové a kód už výrazně nerozšiřuji. Hotovo: svět, rybaření, obchody, souboje, příběh se dvěma konci, split-screen, čeština a angličtina, tři sloty pro uložení. Zbývá hlavně to okolo: dokumentace, testování, build a release, příručka. Mimo rozsah: síťový multiplayer, mobily, vlastní modely, nové velké funkce – ty jdou do backlogu. Čísla: přes 60 skriptů, zhruba 18 tisíc řádků, přes 200 commitů.',
  'Kliknutí na kartu „Hotovo“ skočí rovnou na ukázku hry (slide 5). Čísla (60+ skriptů, 18 000 řádků, 200+ commitů) v úkolu 1 nejsou, přidal jsem je z repozitáře; řádky jsou včetně komentářů. Když je nechceš, smaž spodní řadu. „Nebude“ (síťový multiplayer) je z charteru, ne z úkolu 1.');

// ───────── 4 Architektura (klikací schéma) ─────────
pres.addSection({ title: 'Technika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Technika' });
s.addText('Architektura', { placeholder: 'title' });
s.addImage({ path: path.join(IMG, 'architektura.png'), x: 0.5, y: 1.2, w: 4.9, h: 3.54, objectName: 'Schéma architektury', altText: 'Schéma vrstev: rozhraní, herní logika, stav a ukládání' });
// klikací vrstvy schématu (souřadnice odečtené z vykresleného obrázku)
link(s, S.D_UI, 0.69, 1.51, 4.52, 0.66, 'Vrstva UI', 'Detail: prezentace (UI)');
link(s, S.D_LOGIKA, 0.69, 2.29, 2.18, 0.88, 'Vrstva logika hlavní scéna', 'Detail: herní logika');
link(s, S.D_LOGIKA, 3.04, 2.29, 2.17, 0.88, 'Vrstva logika maják', 'Detail: herní logika');
link(s, S.D_STAV, 0.69, 3.34, 4.52, 0.61, 'Vrstva stav', 'Detail: stav hry');
link(s, S.D_SAVE, 0.69, 4.05, 2.41, 0.56, 'Vrstva ukládání', 'Detail: ukládání');
s.addText('Klikni na vrstvu schématu pro detail', { x: 0.5, y: 4.78, w: 4.9, h: 0.28, fontSize: 12, italic: true, color: C.accent5, margin: 0, isTextBox: true, objectName: 'Nápověda schéma' });
[
  ['Unity 6, C#, URP', 'Jedna offline aplikace, žádný server.', null],
  ['Jeden zdroj pravdy', 'Stav hry v GameData, ukládá se do JSON, 3 sloty.', S.D_STAV],
  ['Nekonečný svět', 'Generuje se kolem hráče, moře je jedna plocha.', S.D_LOGIKA],
  ['Git a GitHub', 'Repozitář, README, licence a složka /docs.', null],
].forEach((a, i) => {
  const y = 1.2 + i * 0.95;
  circleNum(s, i + 1, 5.75, y + 0.05, 0.4, 'Číslo ' + (i + 1));
  s.addText(a[0], { x: 6.3, y, w: 3.2, h: 0.32, fontSize: 16, bold: true, color: C.text1, margin: 0, isTextBox: true, objectName: 'Nadpis ' + (i + 1) });
  s.addText(a[1], { x: 6.3, y: y + 0.34, w: 3.2, h: 0.55, fontSize: 14, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: 'Popis ' + (i + 1) });
  if (a[2]) link(s, a[2], 5.75, y, 3.75, 0.9, 'Odkaz bod ' + (i + 1), 'Klikni pro detail');
});
nav(s, S.ARCH, 4);
note(s, '2:20–3:20  Technologie: Unity 6, C#, Universal Render Pipeline, Git. Zdůvodnění: Unity i C# už znám, hra nepotřebuje server a běží offline. Architektura je vrstvená – nahoře rozhraní (HUD, menu, obchody), uprostřed herní logika (svět, hráč, souboje, příběh), dole stav a ukládání. Klíčové rozhodnutí: veškerý stav žije v jednom objektu GameData, který se ukládá do JSON. Místo databáze tedy model uložení hry. Svět je nekonečný, protože se generuje jen kolem hráče a moře je jedna plocha místo tisíců dlaždic – proto hra běží plynule. Když se komise zeptá, klikni na vrstvu schématu a ukaž detail.',
  'Slide Architektura není v úkolu 1, ale sylabus ho chce k 11. 11. Klikací vrstvy: UI, herní logika, stav hry, ukládání; z každého detailu vede tlačítko zpět. Detaily jsou skryté slidy 12–15 na konci souboru (v normálním listování se přeskakují).');

// ───────── 5–8 Ukázka: galerie se zvětšováním (Morph) ─────────
pres.addSection({ title: 'Ukázka' });
const GAL = [
  { key: 'ostrov', file: 'ostrov.png', step: 'Plavba a rybaření', alt: 'Loď u ostrova s majákem' },
  { key: 'majak', file: 'majak_interier.png', step: 'Maják: prodej a obchod', alt: 'Vnitřek majáku s pulty' },
  { key: 'pirati', file: 'pirati_ostrov.png', step: 'Souboj a pirátský ostrov', alt: 'Pirátský ostrov s hradbami' },
  { key: 'pevnost', file: 'pevnost.png', step: 'Pirátská pevnost', alt: 'Hradby pirátské pevnosti' },
];
const BIG = { x: 0.5, y: 1.3, w: 5.4, h: 2.58 }, TW = 1.76, TH = 0.84;
GAL.forEach((cur, st) => {
  const s2 = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Ukázka' });
  s2.addText('Ukázka hry', { placeholder: 'title' });
  let t = 0;
  GAL.forEach((g, gi) => {
    const isBig = gi === st;
    const pos = isBig ? BIG : { x: 0.5 + t * (TW + 0.06), y: 4.0, w: TW, h: TH };
    if (!isBig) t++;
    s2.addImage({ path: path.join(IMG, g.file), x: pos.x, y: pos.y, w: pos.w, h: pos.h, sizing: { type: 'cover', w: pos.w, h: pos.h }, objectName: '!!img_' + g.key, altText: g.alt });
    if (!isBig) link(s2, S.DEMO_FIRST + gi, pos.x, pos.y, pos.w, pos.h, 'Odkaz náhled ' + g.key, 'Zvětšit: ' + g.step);
  });
  GAL.forEach((g, gi) => {
    const y = 1.4 + gi * 0.78, active = gi === st;
    circleNum(s2, gi + 1, 6.3, y, 0.46, 'Krok ' + (gi + 1), active ? C.accent1 : C.accent2, active ? C.text1 : C.background1);
    s2.addText(g.step, { x: 6.95, y, w: 2.55, h: 0.46, fontSize: 16, bold: active, color: active ? C.text1 : C.accent5, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Krok text ' + (gi + 1) });
    if (!active) link(s2, S.DEMO_FIRST + gi, 6.3, y, 3.2, 0.46, 'Odkaz krok ' + (gi + 1), 'Zvětšit: ' + g.step);
  });
  s2.addText('Klikni na náhled nebo na krok', { x: 6.3, y: 4.55, w: 3.2, h: 0.3, fontSize: 12, italic: true, color: C.accent5, margin: 0, isTextBox: true, objectName: 'Nápověda galerie' });
  nav(s2, S.DEMO, 5);
  if (st === 0) {
    note(s2, '3:20–4:50  Živé demo (nebo záznam, kdyby nešel projektor). Postup: 1) plavba a rybaření, 2) vstup do majáku, prodej a nákup vylepšení, 3) krátký souboj s pirátskou lodí, 4) pirátská pevnost s hradbami; split-screen ukaž živě ve hře. Pro ilustraci klikej na náhledy: vybraný snímek se zvětší. Záložní plán: video 60–90 s a tyto snímky.',
      'Galerie jsou 4 slidy (5 a skryté 6–8), každý s jiným zvětšeným snímkem; přechod Morph zvětší vybraný obrázek plynule (potřebuje PowerPoint 2019 nebo novější, jinak se použije prolnutí). Snímky ostrova, majáku a pirátského ostrova mají testovací mince; snímky majáku a pirátské pevnosti jsou anglické (z EN verze). Starý screenshot menu jsem z galerie vyřadil, protože ukazoval původní název „Poslední maják“ / „Sea Adventure“ – pořiď nový z nové verze hry.');
  } else {
    note(s2, 'Galerie – zvětšený snímek: ' + GAL[st].step + '. Klikni na jiný náhled nebo krok, nebo pokračuj šipkou na další část.');
  }
});

// ───────── 9 Rizika ─────────
pres.addSection({ title: 'Rizika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Rizika' });
s.addText('Rizika a licence', { placeholder: 'title' });
s.addText('Rizika', { x: 0.5, y: 1.25, w: 4.4, h: 0.35, fontSize: 18, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis rizika' });
[
  ['Čas', 'Dokumentace zabere víc, než čekám, proto nepřidávám nové funkce.'],
  ['Výkon', 'Na slabších počítačích. Už jsem na tom pracoval.'],
  ['Autorská práva', 'Modely a postavy jsou CC0, problém nevidím.'],
].forEach((r, i) => {
  const y = 1.75 + i * 1.05;
  s.addText(r[0], { x: 0.5, y, w: 4.4, h: 0.3, fontSize: 16, bold: true, color: C.text1, margin: 0, isTextBox: true, objectName: 'Riziko ' + (i + 1) });
  s.addText(r[1], { x: 0.5, y: y + 0.32, w: 4.4, h: 0.6, fontSize: 14, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: 'Řešení ' + (i + 1) });
});
card(s, 5.4, 1.25, 4.1, 3.6, 'Karta licence');
s.addText('Licence a právo', { x: 5.65, y: 1.4, w: 3.6, h: 0.4, fontSize: 18, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis licence' });
const law = [['Modely a postavy', 'CC0, Kenney a Quaternius'], ['Kód', 'licence MIT (výběr dořeším)'], ['Technologie', 'Unity a C#, které znám'], ['Server a cizí služby', 'žádné, hra je offline']];
s.addText(law.flatMap((l, i) => [
  { text: l[0] + ': ', options: { bold: true, color: C.text1 } },
  { text: l[1], options: { color: C.text1, breakLine: i < law.length - 1 } },
]), { x: 5.65, y: 1.95, w: 3.6, h: 2.8, fontSize: 14, margin: 0, valign: 'top', paraSpaceAfter: 8, isTextBox: true, objectName: 'Seznam licencí' });
nav(s, S.RIZIKA, 6);
note(s, '4:50–5:50  Největší riziko je čas: dokumentace zabere víc, než čekám, proto nové funkce zmrazuji. Výkon: generování světa a ukládání jsem už zrychlil. Autorská práva: modely jsou CC0 od Kenney a Quaternius, vlastní kód pod MIT. Hra nepotřebuje žádný server ani cizí službu.',
  'Z původní verze slidu jsem sem přesunul: 1) riziko „ztráta dat“ (kód na GitHubu, bezpečný zápis savu přes dočasný soubor), 2) zvuky jsou vlastní, generované kódem, 3) hra neshromažďuje osobní údaje, 4) při vývoji jsi používal AI asistenta (Claude Code) – je to uvedeno v ročníkové práci, kap. 6.4. Doporučuji to s vedoucí vyjasnit a umět vysvětlit každou část kódu. Licence MIT: v úkolu 1 je jen „výběr licence“, v repozitáři LICENSE už MIT je.');

// ───────── 10 Business a plán ─────────
pres.addSection({ title: 'Plán' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Plán' });
s.addText('Business a plán do dubna', { placeholder: 'title' });
[['Zdarma', 'případně itch.io jako portfolio'], ['Komunita', 'videa, sdílení ostrovů'], ['Open-source', 'kód pod MIT, grafika CC0']].forEach((b, i) => {
  const x = 0.5 + i * 3.07;
  card(s, x, 1.2, 2.86, 1.1, 'Karta ' + b[0]);
  s.addText([
    { text: b[0], options: { bold: true, fontSize: 16, color: C.accent2, breakLine: true } },
    { text: b[1], options: { fontSize: 14, color: C.text1 } },
  ], { x: x + 0.2, y: 1.25, w: 2.5, h: 1.0, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Text ' + b[0] });
});
const ms = [['11. 11.', 'Charter'], ['9. 12.', 'Osnova'], ['20. 1.', 'Návrh'], ['3. 3.', 'Kód'], ['31. 3.', 'Build'], ['14. 4.', 'Release']];
s.addShape(pres.shapes.LINE, { x: 1.1, y: 3.5, w: 7.8, h: 0, line: { color: C.accent5, width: 2 }, objectName: 'Osa' });
ms.forEach((m, i) => {
  const cx = 1.1 + i * 1.56;
  s.addShape(pres.shapes.OVAL, { x: cx - 0.17, y: 3.33, w: 0.34, h: 0.34, fill: { color: i === ms.length - 1 ? C.accent1 : C.accent2 }, line: { color: C.background1, width: 2 }, objectName: 'Milník ' + (i + 1) });
  s.addText(m[0], { x: cx - 0.7, y: 3.0, w: 1.4, h: 0.3, fontSize: 14, bold: true, color: C.text1, align: 'center', margin: 0, isTextBox: true, objectName: 'Datum ' + (i + 1) });
  s.addText(m[1], { x: cx - 0.7, y: 3.72, w: 1.4, h: 0.3, fontSize: 14, color: C.accent5, align: 'center', margin: 0, isTextBox: true, objectName: 'Název ' + (i + 1) });
});
s.addText('Děkuji za pozornost. Dotazy?', { x: 0.5, y: 4.45, w: 6.5, h: 0.5, fontFace: 'Cambria', fontSize: 22, bold: true, color: C.accent2, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Závěr' });
nav(s, S.PLAN, 7);
note(s, '5:50–7:00  Business: hra zůstane zdarma, případně na itch.io jako portfolio; komunitní přesah přes krátká videa a sdílení zajímavých ostrovů; kód open-source pod MIT. Plán: 11. 11. charter a architektura, 9. 12. osnova dokumentace a licence, 20. 1. datový model, use cases a wireframy, 3. 3. úklid kódu a draft kapitol, 31. 3. testovací build a kompletní draft, 14. 4. finální release a obhajoba. Závěr: děkuji, dotazy – další slide je rozcestník pro dotazy.',
  'Business část (zdarma / itch.io / komunita / open-source) je z charteru, v úkolu 1 není. Časová osa odpovídá úkolu 1: říjen–listopad charter a architektura, prosinec–leden osnova + datový model / use cases / wireframy, únor–březen úklid kódu a testovací build, duben release.');

// ───────── 11 Dotazy – rozcestník ─────────
pres.addSection({ title: 'Otázky' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Otázky' });
s.addText('Dotazy?', { placeholder: 'title' });
s.addText('Klikni na téma, ke kterému se komise ptá', { x: 0.5, y: 1.1, w: 9, h: 0.35, fontSize: 16, color: C.accent5, margin: 0, isTextBox: true, objectName: 'Podtitul dotazy' });
[
  ['Co to je a pro koho', S.TEMA], ['Rozsah a stav', S.ROZSAH], ['Architektura', S.ARCH],
  ['Ukázka hry', S.DEMO], ['Rizika a licence', S.RIZIKA], ['Plán do dubna', S.PLAN],
].forEach((b, i) => {
  const col = i % 3, row = Math.floor(i / 3);
  const x = 0.5 + col * 3.07, y = 1.7 + row * 1.5;
  card(s, x, y, 2.86, 1.2, 'Dlaždice ' + b[0]);
  circleNum(s, i + 1, x + 0.2, y + 0.4, 0.4, 'Číslo dlaždice ' + (i + 1));
  s.addText(b[0], { x: x + 0.75, y: y + 0.1, w: 2.0, h: 1.0, fontSize: 16, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Text dlaždice ' + (i + 1) });
  link(s, b[1], x, y, 2.86, 1.2, 'Odkaz dlaždice ' + (i + 1), 'Přejít: ' + b[0]);
});
nav(s, S.DOTAZY, 8);
note(s, 'Rozcestník pro dotazy: kliknutím na dlaždici skočíš na příslušnou část. U architektury lze dál kliknout na vrstvu schématu.',
  'Je to poslední viditelný slide prezentace. Zpět na kteroukoli část přes dlaždice nebo navigační lištu dole.');

// ───────── 12–15 Detaily architektury (skryté) ─────────
pres.addSection({ title: 'Příloha: detaily' });
const DETAILS = [
  { idx: S.D_UI, title: 'Detail: prezentace (UI)', color: C.accent2,
    bullets: ['Menu, pauza, obchody a dialogy: IMGUI (OnGUI)', 'HUD a minimapa: runtime uGUI', 'Kruhová minimapa s kompasem a šipkou k cíli', 'Čeština a angličtina přes jednu třídu Loc'],
    classes: ['HUDCounter', 'MinimapUIRenderer', 'MainMenuManager', 'PauseMenu', 'ShopUI', 'MapScreen', 'DeathScreen'] },
  { idx: S.D_LOGIKA, title: 'Detail: herní logika', color: C.accent2,
    bullets: ['Svět z dlaždic se generuje jen kolem hráče (28 × 28)', 'Ostrovy: organický tvar, molo, maják, 40 % nepřátelských', 'Souboje: piráti, děla ostrovů, boss', 'Příběh: tři mega ostrovy a dva konce', 'Maják: výkupna a obchody ve vlastní scéně'],
    classes: ['GridManager', 'PlayerController', 'CombatDirector', 'PirateShip', 'StoryNpc', 'LighthouseManager'] },
  { idx: S.D_STAV, title: 'Detail: stav hry', color: C.accent1,
    bullets: ['Veškerý ukládaný stav je v jednom objektu GameData', 'GameSession přežije přechod mezi scénami', 'Čísla ekonomiky na jednom místě (EconomyConfig)', 'Staré savy zůstanou čitelné: hodnoty TileType se nepřečíslovávají'],
    classes: ['GameSession', 'GameData', 'EconomyConfig', 'TileType'] },
  { idx: S.D_SAVE, title: 'Detail: ukládání', color: C.accent3,
    bullets: ['JSON v uložené složce hráče, 3 nezávislé sloty', 'Zápis na pozadí přes dočasný soubor, save se nepoškodí', 'Nejvýš jednou za 8 s, jinak by hra sekala', 'Starý save se doplní výchozími hodnotami'],
    classes: ['SaveManager', 'JsonUtility', 'save_0.json', 'save_1.json', 'save_2.json'] },
];
DETAILS.forEach((d) => {
  const s3 = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Příloha: detaily' });
  s3.addText(d.title, { placeholder: 'title' });
  card(s3, 0.5, 1.3, 5.6, 3.2, 'Karta detail');
  s3.addShape(pres.shapes.OVAL, { x: 0.75, y: 1.52, w: 0.26, h: 0.26, fill: { color: d.color }, line: noLine(d.color), objectName: 'Tečka vrstvy' });
  s3.addText((d.title.replace('Detail: ', '').charAt(0).toUpperCase() + d.title.replace('Detail: ', '').slice(1)), { x: 1.15, y: 1.45, w: 4.8, h: 0.4, fontSize: 18, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Název vrstvy' });
  s3.addText(d.bullets.map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < d.bullets.length - 1 } })), { x: 0.75, y: 1.95, w: 5.1, h: 2.45, fontSize: 16, color: C.text1, margin: 0, valign: 'top', paraSpaceAfter: 8, isTextBox: true, objectName: 'Seznam detail' });
  s3.addText('Hlavní třídy a soubory', { x: 6.4, y: 1.3, w: 3.1, h: 0.35, fontSize: 16, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis tříd' });
  d.classes.forEach((c, k) => {
    const y = 1.75 + k * 0.42;
    s3.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 6.4, y, w: 3.1, h: 0.34, rectRadius: 0.08, fill: { color: C.background2 }, line: noLine(C.background2), objectName: 'Třída ' + c });
    s3.addText(c, { x: 6.5, y, w: 2.9, h: 0.34, fontSize: 14, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Třída text ' + c });
  });
  s3.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 0.5, y: 4.62, w: 2.8, h: 0.4, rectRadius: 0.1, fill: { color: C.accent2 }, line: noLine(C.accent2), objectName: 'Tlačítko zpět' });
  s3.addText('← Zpět na architekturu', { x: 0.5, y: 4.62, w: 2.8, h: 0.4, fontSize: 14, bold: true, color: C.background1, align: 'center', valign: 'middle', margin: 0, isTextBox: true, objectName: 'Tlačítko zpět text' });
  link(s3, S.ARCH, 0.5, 4.62, 2.8, 0.4, 'Odkaz zpět', 'Zpět na architekturu');
  nav(s3, S.ARCH, 4);
  note(s3, 'Detail vrstvy architektury (skrytý slide). Zpět tlačítkem „Zpět na architekturu“ nebo navigační lištou.');
});

// ───────── Zápis + přechody a skryté slidy ─────────
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
  fs.writeFileSync(OUT, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  await postProcess();
  console.log('OK');
})();
