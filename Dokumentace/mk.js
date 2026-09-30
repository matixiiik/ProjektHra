const fs = require('fs');
const d = require('docx');
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle, AlignmentType, LevelFormat, HeadingLevel } = d;
const FONT = 'Calibri';
const numbering = { config: [{ reference: 'b', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 220 } } } }] }] };
const run = (t, o = {}) => new TextRun({ text: t, font: FONT, ...o });
const P = (t, o = {}, ro = {}) => new Paragraph({ spacing: { after: 80 }, ...o, children: Array.isArray(t) ? t : [run(t, ro)] });
const B = (t, size) => new Paragraph({ numbering: { reference: 'b', level: 0 }, spacing: { after: 30 }, children: Array.isArray(t) ? t : [run(t, { size })] });
const H = (t, lvl = HeadingLevel.HEADING_2, size = 24) => new Paragraph({ heading: lvl, spacing: { before: 140, after: 60 }, keepNext: true, children: [run(t, { bold: true, size, color: '1F4E79' })] });
const bold = (t, size) => run(t, { bold: true, size });
const bd = { style: BorderStyle.SINGLE, size: 4, color: '999999' };
const borders = { top: bd, bottom: bd, left: bd, right: bd };
function table(widths, rows, { size = 19, head = true } = {}) {
  const total = widths.reduce((a, b) => a + b, 0);
  return new Table({
    width: { size: total, type: WidthType.DXA }, columnWidths: widths,
    rows: rows.map((r, ri) => new TableRow({
      tableHeader: head && ri === 0, cantSplit: true,
      children: r.map((c, ci) => new TableCell({
        borders, width: { size: widths[ci], type: WidthType.DXA },
        margins: { top: 40, bottom: 40, left: 80, right: 80 },
        shading: head && ri === 0 ? { type: ShadingType.CLEAR, fill: 'D9E2F3', color: 'auto' } : undefined,
        children: [new Paragraph({ spacing: { after: 20 }, children: [run(c, { size, bold: head && ri === 0 })] })]
      }))
    }))
  });
}
const stylesDef = { default: { document: { run: { font: FONT, size: 21 } } } };
const pageProps = (m) => ({ page: { size: { width: 11906, height: 16838 }, margin: { top: m, bottom: m, left: m, right: m } } });

// ───────── ÚKOL 1 ─────────
const s = 20;
const u1 = new Document({ styles: stylesDef, numbering, sections: [{ properties: pageProps(850), children: [
  new Paragraph({ spacing: { after: 20 }, children: [run('IT projekt – Úkol 1: Ukotvení tématu, vize, hodnoty a cílové skupiny', { size: 18, color: '666666' })] }),
  new Paragraph({ spacing: { after: 40 }, children: [run('Poslední maják', { bold: true, size: 40, color: '1F4E79' })] }),
  P([run('3D hra o plavbě po nekonečném oceánu (Unity)  |  Autor: Martin Štěpánek  |  4. ročník, IT projekt  |  Stav projektu ke dni 30. 6. 2026', { size: 18, color: '444444' })], { spacing: { after: 60 }, border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: '1F4E79', space: 2 } } }),
  H('1. Téma – co projekt řeší'),
  P([run('Poslední maják je 3D hra v enginu Unity, ve které hráč pluje po prakticky nekonečném oceánu, rybaří, těží poklady z vraků a za získané mince kupuje vylepšení lodi. Svět se generuje průběžně podle polohy hráče a ukládá se do tří slotů. Hru lze hrát i ve dvou na jednom počítači (split-screen).', { size: s })]),
  P([bold('Problém, který řeší: ', s), run('nabízí klidnou hru na volnou chvíli, která nenutí ke stresu ani k dlouhému učení ovládání – plavba, sbírání a postupné zlepšování lodi. Zároveň jde o ucelený softwarový celek, na kterém lze ukázat návrh architektury, práci s daty (ukládání) a generování obsahu.', { size: s })]),
  H('2. Cílová skupina a přínos'),
  B([bold('Kdo: ', s), run('hráči cca 12–25 let, kteří mají rádi klidné hry s postupným zlepšováním (typu Stardew Valley nebo sbíratelské hry); spolužáci a kamarádi, kteří si chtějí zahrát ve dvou na jednom počítači.', { size: s })]),
  B([bold('Přínos pro hráče: ', s), run('uvolnění, jasný cíl (lepší loď, více mincí), žádný časový tlak, bez registrace a bez internetu.', { size: s })]),
  B([bold('Přínos pro mě a školu: ', s), run('ukázka celého vývojového cyklu – návrh, implementace, testování, dokumentace, licencování.', { size: s })]),
  B([bold('Bariéry cílové skupiny: ', s), run('nároky na výkon 3D grafiky na slabších počítačích, ovládání srozumitelné bez tutoriálu, konkurence hotových her.', { size: s })]),
  H('3. Rámcová validace proveditelnosti (scope)'),
  P([bold('MVP (hotové v 3. ročníku): ', s), run('nekonečný svět z dlaždic, plavba lodi, rybaření a těžba pokladů, obchody s vylepšeními a úkoly, minimapa, tři sloty pro uložení, herní konzole, split-screen pro dva hráče. Vzniklo za cca 2,5 měsíce (duben–červen 2026, přes 20 commitů a desítky skriptů na GitHubu).', { size: s })]),
  P([bold('Rozšíření pro 4. ročník (maturita): ', s), run('(a) souboje s piráty a nepřátelskými ostrovy, (b) příběhová linka s několika speciálními ostrovy, (c) optimalizace výkonu a česko-anglická lokalizace.', { size: s })]),
  table([2300, 3900, 3900], [
    ['Riziko / omezení', 'Jak s ním nakládám', 'Stav'],
    ['Rozsah se rozroste', 'Pevný seznam funkcí, nové nápady jen do backlogu; jádro hry je hotové', 'Řízeno'],
    ['Výkon při nekonečném světě', 'Generuji jen okolí hráče, moře je jedna plocha místo 3D dlaždic', 'Ověřeno, dále měřit'],
    ['Autorská práva k modelům', 'Modely Kenney (licence CC0), vlastní kód; evidence zdrojů', 'Průběžně'],
    ['Neznámé technologie', 'Unity a C# už znám; novinky zkouším v malých prototypech', 'Nízké'],
  ], { size: 18 }),
  P([bold('Technologie: ', s), run('Unity 6, C#, Universal Render Pipeline, Git/GitHub. Hra je offline, nezávisí na externích službách – rizikem je jen čas a výkon.', { size: s })], { spacing: { before: 80, after: 40 } }),
  P([bold('Závěr: ', s), run('rozsah je pro jednoho autora reálný, funkční základ existuje a lze jej bezpečně rozšířit pro maturitní rok.', { size: s })]),
] }] });

// ───────── PLÁN ─────────
const plan = new Document({ styles: stylesDef, numbering, sections: [{ properties: pageProps(1000), children: [
  new Paragraph({ spacing: { after: 40 }, children: [run('Plán ročníkové (maturitní) práce – Poslední maják', { bold: true, size: 36, color: '1F4E79' })] }),
  P([run('Pracovní dokument: harmonogram, checklist milníků, kostra dokumentace a prezentace. Vychází ze sylabu IT projektu (konzultace 16. 9. – 28. 4.). Stav k 30. 9. 2026.', { size: 19, color: '555555' })]),
  H('1. Přehled: co už máme a co zbývá'),
  B('Hotovo (kód): nekonečný svět, plavba, rybaření a těžba, obchody v majáku, questy, souboje s piráty, nepřátelské ostrovy, příběh a tři mega ostrovy, split-screen, ukládání do 3 slotů, lokalizace CZ/EN, optimalizace ukládání i generování.'),
  B('Chybí (papíry): Project Charter, architektura, licence, datový model, use cases, wireframy, dokumentace, testování, build/release, prezentace. Kód se nyní nerozšiřuje – zaměření na dokumentaci a obhajobu.'),
  B('Pozor na hodnocení: dodržení termínů = 20 % známky (zpoždění se strhává), komise 40 %. Termíny proto berou přednost před dolaďováním hry.'),
  H('2. Harmonogram a odevzdávky'),
  table([1150, 3700, 5250], [
    ['Termín', 'Co odevzdat (PDF / GitHub)', 'Co k tomu použít z projektu'],
    ['30. 9.', 'Úkol 1: vymezení tématu (1 strana)', 'Ukol1_Vymezeni_tematu.docx'],
    ['11. 11.', 'Úkol 2: Project Charter + skica architektury + business/PR + GitHub (školní účet, /docs, README) + prezentace před komisí (7 min)', 'Schéma vrstev hry (GameSession, GridManager, SaveManager…), rizika, monetizace; přesun/zrcadlení repozitáře na školní účet'],
    ['9. 12.', 'Osnova dokumentace (kap. 1–2), první commity, licence', 'Licence vlastního kódu (MIT/Apache), tabulka cizích assetů (Kenney CC0 aj.), citace zdrojů'],
    ['20. 1.', 'Datový model/API, use cases, wireframy, rozpracovaná kapitola Architektura a návrh; pololetní prezentace', 'Model uložení hry (GameData → JSON, sloty) místo klasické DB; UC: rybaření, nákup, souboj; skici HUD/menu/obchodu'],
    ['3. 3.', 'Aktuální kód a funkční základ + draft technických kapitol', 'Popis realizovaných funkcí; kvalita kódu, komentáře, refaktoring'],
    ['31. 3.', 'Testovací build + kompletní draft dokumentace (kap. 1–4)', 'Build Windows, testovací protokol, uživatelská příručka; citace dle ISO 690'],
    ['14. 4.', 'Finální release na školním GitHubu + PDF dokumentace + závěrečná prezentace', 'Release, Making-of/příručka, demo'],
    ['28. 4.', 'Konzultace maturitní prezentace', 'Nácvik obhajoby'],
  ], { size: 18 }),
  H('3. Checklist po milnících'),
  H('Milník 1 – do 11. 11. (Project Charter + architektura + prezentace)', HeadingLevel.HEADING_3, 22),
  B('Charter: název, vize, cíl, cílovka, scope (co ANO / co NE), MVP, milníky, časový rozpočet, rizika, omezení, kritéria úspěchu.'),
  B('Architektura: schéma – Unity klient (hráč, svět, obchody, souboje) ↔ GameSession ↔ SaveManager (JSON na disk); scény SampleScene a LighthouseInterior.'),
  B('Business/PR: jednorázová cena na itch.io/Steamu, komunita (mody, YouTube), open-source varianta.'),
  B('GitHub: repozitář na školním účtu, složka /docs, README.md (popis, ovládání, spuštění).'),
  B('Prezentace 7 min (viz kap. 5) – nacvičit s časomírou.'),
  H('Milník 2 – do 9. 12. (osnova + licence)', HeadingLevel.HEADING_3, 22),
  B('Osnova dokumentace, kap. 1 Úvod a 2 Teoretická část (Unity, C#, procedurální generování, návrhové vzory).'),
  B('Licence: zvolit MIT (jednoduché, obhajitelné); soupis všech cizích assetů se zdrojem a licencí – model, zvuk, font.'),
  H('Milník 3 – do 20. 1. (návrh systému)', HeadingLevel.HEADING_3, 22),
  B('Diagram datového modelu (třídní diagram GameData, TileStatus, obchody); domluvit s vedoucí, že u hry nahrazuje ERD.'),
  B('Use Case diagram + 4–6 scénářů; wireframy menu, HUD, obchodu, mapy.'),
  B('Kapitola Architektura a návrh systému (rozpracovaná); lean canvas / monetizace.'),
  H('Milník 4 – do 3. 3. (kvalita kódu, bezpečnost)', HeadingLevel.HEADING_3, 22),
  B('Review kódu: komentáře, konvence, ošetření chyb (poškozený save). GDPR: hra neshromažďuje osobní údaje – uvést v dokumentaci.'),
  B('Draft kapitol o realizaci: generování světa, ukládání, souboje, příběh.'),
  H('Milník 5 – do 31. 3. (test a build)', HeadingLevel.HEADING_3, 22),
  B('Testovací plán a protokol (ruční scénáře, herní konzole s presety), seznam nalezených chyb a oprav.'),
  B('Build (.exe) + Release Candidate na GitHubu; uživatelská příručka; citace ISO 690.'),
  H('Milník 6 – do 14. 4. (odevzdání a obhajoba)', HeadingLevel.HEADING_3, 22),
  B('Finální release, PDF dokumentace, prezentace 5–10 min + živé demo, odpovědi na oponenta.'),
  H('4. Kostra dokumentace (Word → PDF)'),
  table([700, 3000, 6400], [
    ['Kap.', 'Název', 'Obsah'],
    ['1', 'Úvod', 'Cíl, motivace, vize, cílová skupina, rozsah, struktura práce'],
    ['2', 'Teoretická část', 'Unity a C#, URP, herní smyčka, procedurální generování, ukládání dat (JSON), split-screen, lokalizace'],
    ['3', 'Analýza a návrh', 'Požadavky, use cases, wireframy, architektura, datový model, konkurence, business model'],
    ['4', 'Realizace', 'Svět a ostrovy, hráč a loď, obchody a ekonomika, souboje, příběh, ukládání, výkon; ukázky kódu'],
    ['5', 'Testování a nasazení', 'Testy, chyby, build, release, uživatelská příručka'],
    ['6', 'Právní rámec', 'Licence vlastního kódu, licence cizích assetů, GDPR'],
    ['7', 'Závěr', 'Splnění cílů, omezení, možná rozšíření'],
    ['–', 'Přílohy, zdroje', 'Seznam zdrojů dle ISO 690, diagramy, odkaz na repozitář'],
  ], { size: 18 }),
  H('5. Prezentace (7 minut)'),
  table([1100, 3000, 6000], [
    ['Čas', 'Slide', 'Obsah'],
    ['0:00', '1 Téma a vize', 'Co je hra, komu je určena, jaký problém řeší'],
    ['1:00', '2 Scope a MVP', 'Co je hotové, co ne, rizika'],
    ['2:00', '3 Architektura', 'Schéma, technologie a jejich obhajoba'],
    ['3:30', '4 Živé demo / video', 'Plavba, rybaření, obchod, souboj (60–90 s)'],
    ['5:00', '5 Business a licence', 'Monetizace, komunita, licence assetů'],
    ['6:00', '6 Plán a závěr', 'Co zbývá do 14. 4., dotazy'],
  ], { size: 18 }),
  H('6. Otevřené otázky pro vedoucí'),
  B('Repozitář je zatím na osobním účtu (matixiiik) – jak přesně přesunout/zrcadlit na školní?'),
  B('Uznává se místo ERD model ukládání hry (JSON) a třídní diagram?'),
  B('Kolik uvádět jako „hotový základ“ – stav z jara, nebo dnešní?'),
] }] });

(async () => {
  fs.writeFileSync('Plan_rocnikove_prace.docx', await Packer.toBuffer(plan));
})();
