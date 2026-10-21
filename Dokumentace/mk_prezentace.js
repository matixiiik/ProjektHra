// Generuje Prezentace_The_Last_Beacon.pptx (čtvrtletní prezentace 11. 11., 7 minut)
// Spuštění: NODE_PATH=<složka s node_modules> node mk_prezentace.js
// Motiv barev: hluboké moře + lucerna majáku (jantarová).
const pptxgen = require('pptxgenjs');
const path = require('path');
const SKILL = process.env.SKILL_PPTX || 'C:/Users/matix/AppData/Local/Claude-Data/claude_pzs8sxrjxfjjc/local-agent-mode-sessions/skills-plugin/052f3edc-54b3-4acf-942f-b58b343f6d1b/679b4655-be0e-43be-b6dc-ff9660aa7ac7/skills/pptx';
const { applyTheme } = require(SKILL + '/scripts/apply_theme.js');

const THEME = {
  name: 'The Last Beacon',
  headFontFace: 'Cambria',
  bodyFontFace: 'Calibri',
  colors: {
    dk1: '0B2A43', lt1: 'FFFFFF', dk2: '1C7293', lt2: 'EAF2F6',
    accent1: 'F2A93B', accent2: '1C7293', accent3: '2E8B6F',
    accent4: 'C8553D', accent5: '5F7482', accent6: '0B2A43',
    hlink: '1C7293', folHlink: '5F7482',
  },
};

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10" x 5.625"
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = 'The Last Beacon – čtvrtletní prezentace';
pres.author = 'Martin Štěpánek';
const C = pres.SchemeColor;
const IMG = path.join(__dirname, 'obrazky');

// ── Layouty ────────────────────────────────────────────────
pres.defineSlideMaster({
  title: 'TITULNI', background: { color: THEME.colors.dk1 },
  objects: [],
});
pres.defineSlideMaster({
  title: 'OBSAH', background: { color: 'FFFFFF' },
  objects: [
    { placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 0.3, w: 9, h: 0.8, fontFace: 'Cambria', fontSize: 34, bold: true, color: C.text1, margin: 0, valign: 'middle', align: 'left' }, text: '' } },
  ],
  slideNumber: { x: 9.2, y: 5.2, w: 0.5, h: 0.3, fontFace: 'Calibri', fontSize: 11, color: C.accent5, align: 'right' },
});

const lamp = (s, x, y, d, name) => s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: C.accent1 }, line: { color: C.accent1, width: 0 }, objectName: name });
const circleNum = (s, n, x, y, d, name, col) => {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: col || C.accent2 }, line: { color: col || C.accent2, width: 0 }, objectName: name });
  s.addText(String(n), { x, y, w: d, h: d, align: 'center', valign: 'middle', fontSize: 14, bold: true, color: C.background1, margin: 0, isTextBox: true, objectName: name + ' text' });
};

// ───────── 1 Titulní ─────────
pres.addSection({ title: 'Úvod' });
let s = pres.addSlide({ masterName: 'TITULNI', sectionTitle: 'Úvod' });
lamp(s, 0.5, 0.55, 0.28, 'Lucerna');
s.addText('The Last Beacon', { x: 0.5, y: 1.1, w: 4.5, h: 1.0, fontFace: 'Cambria', fontSize: 36, bold: true, color: C.background1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Titulek' });
s.addText('3D hra o plavbě po nekonečném moři', { x: 0.5, y: 2.15, w: 4.3, h: 0.9, fontSize: 20, color: C.background2, margin: 0, valign: 'top', isTextBox: true, objectName: 'Podtitulek' });
s.addText([
  { text: 'Martin Štěpánek', options: { bold: true, breakLine: true } },
  { text: 'IT projekt, 4. ročník', options: { breakLine: true } },
  { text: 'Čtvrtletní prezentace, 11. 11. 2026' },
], { x: 0.5, y: 3.9, w: 4.3, h: 1.1, fontSize: 14, color: C.background2, margin: 0, valign: 'top', isTextBox: true, objectName: 'Autor' });
s.addImage({ path: path.join(IMG, 'ostrov.png'), x: 5.1, y: 1.45, w: 4.4, h: 2.1, objectName: 'Snímek ostrova', altText: 'Ostrov s majákem ve hře' });
s.addText('Unity 6 · C# · offline · 1–2 hráči', { x: 5.1, y: 3.7, w: 4.4, h: 0.4, fontSize: 14, color: C.accent1, margin: 0, align: 'left', isTextBox: true, objectName: 'Stack' });
s.addNotes('0:00–0:20  Představení. The Last Beacon je 3D hra v Unity o plavbě po nekonečném moři. Dnes vám za 7 minut ukážu: co hra je a pro koho, co je hotové, architekturu, krátké demo, rizika a licence, a plán do dubna.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Snímek ostrova má vpravo nahoře testovací „Mince: 1098809“ (cheat). Pokud to nechceš ukazovat, udělej čistý screenshot z nové hry a vyměň obrázek.');

// ───────── 2 Téma a cílovka ─────────
pres.addSection({ title: 'Téma' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Téma' });
s.addText('Co to je a pro koho', { placeholder: 'title' });
const blocks = [
  ['Co to je', 'Plavba po nekonečném moři: rybaření, poklady z vraků, obchod v majácích, souboje s piráty a příběh.'],
  ['Pro koho', 'Hráči kolem 12–25 let, kteří chtějí klidnou hru, a dva kamarádi na jednom PC.'],
  ['Co přinese', 'Uvolnění, jasný cíl (lepší loď) a příběh. Bez registrace a bez internetu.'],
];
blocks.forEach((b, i) => {
  const y = 1.35 + i * 1.28;
  s.addText(b[0], { x: 0.5, y, w: 4.5, h: 0.32, fontSize: 16, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis ' + (i + 1) });
  s.addText(b[1], { x: 0.5, y: y + 0.34, w: 4.5, h: 0.85, fontSize: 14, color: C.text1, margin: 0, valign: 'top', isTextBox: true, objectName: 'Text ' + (i + 1) });
});
s.addImage({ path: path.join(IMG, 'hud.png'), x: 5.4, y: 1.35, w: 4.1, h: 1.96, objectName: 'Snímek HUD', altText: 'Rozhraní hry: minimapa, zdraví a hotbar' });
s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 5.4, y: 3.55, w: 4.1, h: 1.3, rectRadius: 0.12, fill: { color: C.background2 }, line: { color: C.background2, width: 0 }, objectName: 'Karta cíle' });
s.addText([
  { text: 'Cíl práce', options: { bold: true, color: C.accent2, breakLine: true } },
  { text: 'Hratelný build, kód na GitHubu a kompletní dokumentace do 14. 4. 2027.' },
], { x: 5.6, y: 3.6, w: 3.7, h: 1.2, fontSize: 14, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Cíl práce' });
s.addNotes('0:20–1:20  Hra je klidná: plujete, rybaříte, těžíte poklady z vraků, v majácích prodáváte a vylepšujete loď. Do toho piráti a příběh starého námořníka. Cílovka jsou hráči zhruba 12–25 let a dvojice kamarádů, protože se hraje i split-screen na jednom PC. Přínos: uvolnění, jasný cíl, žádný stres. Mým cílem je ukázat celý postup tvorby většího softwaru až po release a dokumentaci. Bariéry cílovky: výkon na slabších PC a ovládání bez tutoriálu.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): V úkolu 1 máš „ztracený poklad“, ve hře je ale hlavní linka ztracený bratr (poklad je jen historický poklad). Na slidu jsem příběh nijak nerozváděl, rozhodni, co řekneš. Screenshot HUD má také cheatové mince (viz slide 1).');

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
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.3, w: 2.86, h: 2.75, rectRadius: 0.12, fill: { color: C.background2 }, line: { color: C.background2, width: 0 }, objectName: 'Karta ' + c[0] });
  s.addShape(pres.shapes.OVAL, { x: x + 0.2, y: 1.5, w: 0.26, h: 0.26, fill: { color: c[1] }, line: { color: c[1], width: 0 }, objectName: 'Tečka ' + c[0] });
  s.addText(c[0], { x: x + 0.58, y: 1.42, w: 2.1, h: 0.42, fontSize: 18, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Nadpis ' + c[0] });
  s.addText(c[2].map((t, k) => ({ text: t, options: { bullet: true, breakLine: k < c[2].length - 1 } })), { x: x + 0.2, y: 1.95, w: 2.5, h: 2.0, fontSize: 14, color: C.text1, margin: 0, valign: 'top', paraSpaceAfter: 4, isTextBox: true, objectName: 'Seznam ' + c[0] });
});
const stats = [['60+', 'skriptů v C#'], ['18 000', 'řádků kódu'], ['200+', 'commitů na GitHubu']];
stats.forEach((st, i) => {
  const x = 0.5 + i * 3.07;
  s.addText(st[0], { x, y: 4.2, w: 1.45, h: 0.7, fontFace: 'Cambria', fontSize: 30, bold: true, color: C.accent2, margin: 0, valign: 'middle', align: 'right', isTextBox: true, objectName: 'Číslo ' + (i + 1) });
  s.addText(st[1], { x: x + 1.6, y: 4.2, w: 1.35, h: 0.7, fontSize: 14, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Popisek ' + (i + 1) });
});
s.addNotes('1:20–2:20  MVP je hotové a kód už výrazně nerozšiřuji. Hotovo: svět, rybaření, obchody, souboje, příběh se dvěma konci, split-screen, čeština a angličtina, tři sloty pro uložení. Zbývá hlavně to okolo: dokumentace, testování, build a release, příručka. Mimo rozsah: síťový multiplayer, mobily, vlastní modely, nové velké funkce – ty jdou do backlogu. Čísla: přes 60 skriptů, zhruba 18 tisíc řádků, přes 200 commitů.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Čísla (60+ skriptů, 18 000 řádků, 200+ commitů) v úkolu 1 nejsou, přidal jsem je z repozitáře. Řádky jsou včetně komentářů. Když je nechceš, smaž tři čísla ve spodní řadě. Ve „Zbývá“ je i oprava chyb a v „Nebude“ síťový multiplayer – to je z charteru, ne z úkolu 1.');

// ───────── 4 Architektura ─────────
pres.addSection({ title: 'Technika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Technika' });
s.addText('Architektura', { placeholder: 'title' });
s.addImage({ path: path.join(IMG, 'architektura.png'), x: 0.5, y: 1.2, w: 4.9, h: 3.54, objectName: 'Schéma architektury', altText: 'Schéma vrstev: rozhraní, herní logika, stav a ukládání' });
const arch = [
  ['Unity 6, C#, URP', 'Jedna offline aplikace, žádný server.'],
  ['Jeden zdroj pravdy', 'Stav hry v GameData, ukládá se do JSON, 3 sloty.'],
  ['Nekonečný svět', 'Generuje se kolem hráče, moře je jedna plocha.'],
  ['Git a GitHub', 'Repozitář, README, licence a složka /docs.'],
];
arch.forEach((a, i) => {
  const y = 1.2 + i * 0.95;
  circleNum(s, i + 1, 5.75, y + 0.05, 0.4, 'Číslo ' + (i + 1));
  s.addText(a[0], { x: 6.3, y, w: 3.2, h: 0.32, fontSize: 16, bold: true, color: C.text1, margin: 0, isTextBox: true, objectName: 'Nadpis ' + (i + 1) });
  s.addText(a[1], { x: 6.3, y: y + 0.34, w: 3.2, h: 0.55, fontSize: 14, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: 'Popis ' + (i + 1) });
});
s.addNotes('2:20–3:20  Technologie: Unity 6, C#, Universal Render Pipeline, Git. Zdůvodnění: Unity i C# už znám, hra nepotřebuje server a běží offline. Architektura je vrstvená – nahoře rozhraní (HUD, menu, obchody), uprostřed herní logika (svět, hráč, souboje, příběh), dole stav a ukládání. Klíčové rozhodnutí: veškerý stav žije v jednom objektu GameData, který se ukládá do JSON. Místo databáze tedy model uložení hry. Svět je nekonečný, protože se generuje jen kolem hráče a moře je jedna plocha místo tisíců dlaždic – proto hra běží plynule.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Slide Architektura není v úkolu 1 (ten ho nemá), ale sylabus ho chce k 11. 11. Schéma obsahuje názvy tříd, pokud je komise nebude zajímat, stačí zmínit čtyři body vpravo.');

// ───────── 5 Ukázka ─────────
pres.addSection({ title: 'Ukázka' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Ukázka' });
s.addText('Ukázka hry', { placeholder: 'title' });
s.addImage({ path: path.join(IMG, 'ostrov.png'), x: 0.5, y: 1.3, w: 5.4, h: 2.58, objectName: 'Snímek ostrova', altText: 'Ostrov s molem a majákem' });
s.addImage({ path: path.join(IMG, 'obchod.png'), x: 0.5, y: 4.0, w: 1.8, h: 0.86, objectName: 'Snímek obchodu', altText: 'Obchod v majáku' });
s.addImage({ path: path.join(IMG, 'menu.png'), x: 2.5, y: 4.0, w: 1.8, h: 0.86, objectName: 'Snímek menu', altText: 'Hlavní menu' });
const demo = ['Plavba a rybaření', 'Maják: prodej a obchod', 'Souboj s pirátem', 'Split-screen pro dva'];
demo.forEach((d, i) => {
  const y = 1.4 + i * 0.78;
  circleNum(s, i + 1, 6.3, y, 0.46, 'Krok ' + (i + 1));
  s.addText(d, { x: 6.95, y, w: 2.55, h: 0.46, fontSize: 16, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Krok text ' + (i + 1) });
});
s.addText('Živé demo nebo video, 60–90 s', { x: 6.3, y: 4.5, w: 3.2, h: 0.4, fontSize: 14, italic: true, color: C.accent5, margin: 0, isTextBox: true, objectName: 'Poznámka demo' });
s.addNotes('3:20–4:50  Živé demo (nebo záznam, kdyby nešel projektor). Postup: 1) plavba a rybaření – ukázat svět a minimapu, 2) vstup do majáku, prodej a nákup vylepšení, 3) krátký souboj s pirátskou lodí, 4) zapnout split-screen. Záložní plán: nahrané video 60–90 s a snímky na slidu.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Malé snímky pod hlavním obrázkem (obchod, menu) jsou jen ilustrační; obchod má taky cheatové mince. V úkolu 1 ukázka hry není, je to sylabem požadované demo.');

// ───────── 6 Rizika a právo ─────────
pres.addSection({ title: 'Rizika' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Rizika' });
s.addText('Rizika a licence', { placeholder: 'title' });
s.addText('Rizika', { x: 0.5, y: 1.25, w: 4.4, h: 0.35, fontSize: 18, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis rizika' });
const risks = [
  ['Čas', 'Dokumentace zabere víc, než čekám, proto nepřidávám nové funkce.'],
  ['Výkon', 'Na slabších počítačích. Už jsem na tom pracoval.'],
  ['Autorská práva', 'Modely a postavy jsou CC0, problém nevidím.'],
];
risks.forEach((r, i) => {
  const y = 1.75 + i * 1.05;
  s.addText(r[0], { x: 0.5, y, w: 4.4, h: 0.3, fontSize: 16, bold: true, color: C.text1, margin: 0, isTextBox: true, objectName: 'Riziko ' + (i + 1) });
  s.addText(r[1], { x: 0.5, y: y + 0.32, w: 4.4, h: 0.6, fontSize: 14, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: 'Řešení ' + (i + 1) });
});
s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 5.4, y: 1.25, w: 4.1, h: 3.6, rectRadius: 0.12, fill: { color: C.background2 }, line: { color: C.background2, width: 0 }, objectName: 'Karta licence' });
s.addText('Licence a právo', { x: 5.65, y: 1.4, w: 3.6, h: 0.4, fontSize: 18, bold: true, color: C.accent2, margin: 0, isTextBox: true, objectName: 'Nadpis licence' });
const law = [
  ['Modely a postavy', 'CC0, Kenney a Quaternius'],
  ['Kód', 'licence MIT (výběr dořeším)'],
  ['Technologie', 'Unity a C#, které znám'],
  ['Server a cizí služby', 'žádné, hra je offline'],
];
s.addText(law.flatMap((l, i) => [
  { text: l[0] + ': ', options: { bold: true, color: C.text1 } },
  { text: l[1], options: { color: C.text1, breakLine: i < law.length - 1 } },
]), { x: 5.65, y: 1.95, w: 3.6, h: 2.8, fontSize: 14, margin: 0, valign: 'top', paraSpaceAfter: 8, isTextBox: true, objectName: 'Seznam licencí' });
s.addNotes('4:50–5:50  Největší riziko je čas: dokumentace zabere víc, než čekám, proto nové funkce zmrazuji. Výkon: generování světa a ukládání jsem už zrychlil. Data: kód je na GitHubu a save se zapisuje přes dočasný soubor, aby se nepoškodil. Autorská práva: modely jsou CC0 od Kenney a Quaternius, zvuky jsem vygeneroval sám, vlastní kód pod MIT. Hra neshromažďuje osobní údaje. Hra nepotřebuje žádný server ani cizí službu.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Z původní verze slidu jsem sem přesunul: 1) riziko „ztráta dat“ (kód na GitHubu, bezpečný zápis savu přes dočasný soubor), 2) zvuky jsou vlastní, generované kódem, 3) hra neshromažďuje osobní údaje, 4) při vývoji jsi používal AI asistenta (Claude Code) – je to uvedeno v ročníkové práci, kap. 6.4. Doporučuji to s vedoucí vyjasnit a umět vysvětlit každou část kódu, protože se komise může zeptat. Licence MIT: v úkolu 1 je jen „výběr licence“, v repozitáři LICENSE už MIT je.');

// ───────── 7 Business a plán ─────────
pres.addSection({ title: 'Plán' });
s = pres.addSlide({ masterName: 'OBSAH', sectionTitle: 'Plán' });
s.addText('Business a plán do dubna', { placeholder: 'title' });
const biz = [['Zdarma', 'případně itch.io jako portfolio'], ['Komunita', 'videa, sdílení ostrovů'], ['Open-source', 'kód pod MIT, grafika CC0']];
biz.forEach((b, i) => {
  const x = 0.5 + i * 3.07;
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.2, w: 2.86, h: 1.1, rectRadius: 0.12, fill: { color: C.background2 }, line: { color: C.background2, width: 0 }, objectName: 'Karta ' + b[0] });
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
s.addText('Děkuji za pozornost. Dotazy?', { x: 0.5, y: 4.5, w: 9, h: 0.5, fontFace: 'Cambria', fontSize: 22, bold: true, color: C.accent2, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Závěr' });
s.addNotes('5:50–7:00  Business: hra zůstane zdarma, případně na itch.io jako portfolio; komunitní přesah přes krátká videa a sdílení zajímavých ostrovů; kód open-source pod MIT. Plán: 11. 11. charter a architektura, 9. 12. osnova dokumentace a licence, 20. 1. datový model, use cases a wireframy, 3. 3. úklid kódu a draft kapitol, 31. 3. testovací build a kompletní draft, 14. 4. finální release a obhajoba. Závěr: děkuji, dotazy.\n\n⚠ POUZE PRO MĚ (nezobrazí se při promítání): Business část (zdarma / itch.io / komunita / open-source) je z charteru, v úkolu 1 není. Časová osa odpovídá úkolu 1: říjen–listopad charter a architektura, prosinec–leden osnova + datový model / use cases / wireframy, únor–březen úklid kódu a testovací build, duben release. Úkol 1 má v říjnu–listopadu i opravu bugů.');

(async () => {
  await pres.writeFile({ fileName: path.join(__dirname, 'Prezentace_The_Last_Beacon.pptx') });
  await applyTheme(path.join(__dirname, 'Prezentace_The_Last_Beacon.pptx'), THEME);
  console.log('OK');
})();
