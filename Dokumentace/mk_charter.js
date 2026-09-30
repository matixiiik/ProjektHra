// Generuje Project_Charter.docx (2 strany, jednoduchy text)
const fs = require('fs');
const { Document, Packer, Paragraph, TextRun, BorderStyle, AlignmentType, LevelFormat } = require('docx');
const F = 'Calibri';
const runs = (t, o = {}) => t.split(/(\[\[.*?\]\])/).filter(x => x).map(x => x.startsWith('[[')
  ? new TextRun({ text: '[' + x.slice(2, -2) + ']', font: F, size: 22, highlight: 'yellow', ...o })
  : new TextRun({ text: x, font: F, size: 22, ...o }));
const bold = (t) => new TextRun({ text: t, font: F, size: 22, bold: true });
const P = (t, o = {}) => new Paragraph({ spacing: { after: 100, line: 276 }, ...o, children: Array.isArray(t) ? t : runs(t) });
const LP = (lead, t) => new Paragraph({ spacing: { after: 100, line: 276 }, children: [bold(lead), ...runs(t)] });
const B = (t) => new Paragraph({ numbering: { reference: 'b', level: 0 }, spacing: { after: 40, line: 268 }, children: runs(t) });
const H = (t) => new Paragraph({ keepNext: true, spacing: { before: 200, after: 60 }, children: [new TextRun({ text: t, font: F, bold: true, size: 26, color: '1F4E79' })] });
const M = (d, t) => new Paragraph({ spacing: { after: 40 }, indent: { left: 360 }, children: [bold(d + '  '), ...runs(t)] });

const doc = new Document({
  numbering: { config: [{ reference: 'b', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 270 } } } }] }] },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1000, bottom: 900, left: 1200, right: 1200 } } },
    children: [
      new Paragraph({ spacing: { after: 40 }, children: [new TextRun({ text: 'Project Charter – Poslední maják', font: F, bold: true, size: 44, color: '1F4E79' })] }),
      new Paragraph({ spacing: { after: 120 }, border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: '1F4E79', space: 2 } }, children: runs('Autor: Martin Štěpánek  |  Vedoucí: Kristina Sedeke  |  IT projekt, 4. ročník  |  Verze 1.0, [[DATUM]]', { size: 20, color: '555555' }) }),

      H('1. Vize a cíl'),
      P('Dokončit a obhájit hru Poslední maják: klidnou 3D hru o plavbě po nekonečném moři, ve které hráč rybaří, těží poklady, bojuje s piráty a odemyká příběh. Výsledkem je hratelný build, veřejný repozitář a kompletní dokumentace do 14. 4. 2027.'),

      H('2. Cílová skupina a přínos'),
      P('Hráči přibližně 12–25 let, kteří chtějí klidnou hru bez stresu a bez učení složitého ovládání, a dvojice kamarádů, které baví hrát na jednom počítači. Získají uvolnění, jasný cíl a příběh. Přínos pro mě: celý postup tvorby většího softwaru od návrhu po release, dokumentaci a licence.'),

      H('3. Rozsah (scope)'),
      LP('Hotovo: ', 'nekonečný generovaný svět, ostrovy s majáky, rybaření a těžba, obchody a úkoly, souboje s piráty a nepřátelskými ostrovy, příběh se třemi speciálními ostrovy a dvěma konci, split-screen pro 2 hráče, ukládání do 3 slotů, čeština a angličtina.'),
      LP('Zbývá udělat: ', 'dokumentaci, testování, build a release, uživatelskou příručku, licence, úklid a okomentování kódu.'),
      LP('Nebude součástí: ', 'síťový multiplayer, mobilní verze, vlastní 3D modely, nové velké funkce. Nové nápady jdou do backlogu a řeší se až po odevzdání.'),
      LP('MVP: ', 'hru lze dohrát od nové hry po oba konce příběhu, ukládá se a načítá a dá se spustit z hotového buildu.'),

      H('4. Milníky'),
      M('11. 11.', 'Project Charter, skica architektury, GitHub na školním účtu s README a složkou /docs, prezentace před komisí (7 minut).'),
      M('9. 12.', 'osnova dokumentace, kapitoly 1 a 2, výběr licence, evidence cizích assetů.'),
      M('20. 1.', 'datový model, use cases, wireframy, rozpracovaná kapitola Architektura a návrh; pololetní prezentace.'),
      M('3. 3.', 'aktuální kód na GitHubu, úklid a komentáře, draft technických kapitol.'),
      M('31. 3.', 'testovací build, protokol testování, kompletní draft dokumentace.'),
      M('14. 4.', 'finální release, PDF dokumentace, uživatelská příručka, závěrečná prezentace.'),

      H('5. Technologie a architektura'),
      P('Unity 6 (6000.3.10f1) s Universal Render Pipeline, jazyk C#, Git a GitHub. Hra je jedna offline aplikace bez serveru. Stav hry je v jednom objektu (GameData), který drží GameSession a ukládá SaveManager do JSON souborů ve třech slotech. Nad tím stojí herní logika (svět, hráč, souboje, příběh) a rozhraní (menu, HUD, obchody). Model uložení hry nahrazuje klasický ERD. [[Sem vložit nakreslenou skicu architektury.]]'),

      H('6. Rizika a omezení'),
      B('Čas: dokumentace zabere víc, než čekám. Proto zmrazím nové funkce a budu držet harmonogram.'),
      B('Výkon na slabších počítačích: generování světa a ukládání už jsem zrychlil, před buildem to ještě změřím.'),
      B('Autorská práva: modely jsou od Kenney a Quaternius (CC0), zvuky vlastní; vše zapíšu do evidence assetů.'),
      B('Ztráta dat: kód je na GitHubu, ukládání používá dočasný soubor a záměnu, aby se save nepoškodil.'),
      B('Použití AI asistenta při vývoji: transparentně uvedu v dokumentaci a ověřím u vedoucí, že je to v pořádku. Kód musím umět vysvětlit sám.'),

      H('7. Business a PR (rámcově)'),
      P('Hra zůstane zdarma, případně nabídnu build na itch.io jako portfolio; kód pod licencí MIT [[potvrdit výběr]]. Komunitní přesah: krátká videa ze hry a možnost sdílet zajímavé ostrovy.'),

      H('8. Kritéria úspěchu'),
      B('Hra běží z buildu bez chyb a jde ji dohrát od začátku do konce.'),
      B('Repozitář obsahuje release, README, licenci a složku /docs; dokumentace je odevzdána v termínu.'),
      B('Zvládnu bez přípravy vysvětlit architekturu a klíčová řešení před komisí.'),

      new Paragraph({ spacing: { before: 300 }, children: runs('Schváleno: ______________________ (vedoucí)          Datum: ______________') }),
    ],
  }],
});
Packer.toBuffer(doc).then(x => { fs.writeFileSync('Project_Charter.docx', x); console.log('OK'); });
