// Generuje Uzivatelska_prirucka_Posledni_majak.docx
// Sdílí pomocné funkce (styly, tabulky, obrázky) s mk_prace.js — bere z něj úvod souboru.
const fs = require('fs');
const src = fs.readFileSync(require('path').join(__dirname, 'mk_prace.js'), 'utf8');
const head = src.slice(0, src.indexOf('// ───────── Titulní strana'));
const lib = new Function('require', '__dirname', head +
  '; return { d, P, B, N, H1, H1n, H2, H3, CAP, IMG, table, spacer, numbering, FONT };')(require, __dirname);
const { d, P, B, N, H1n, H2, H3, IMG, table, spacer, numbering, FONT } = lib;
const { Document, Packer, Paragraph, TextRun, AlignmentType, Footer, PageNumber } = d;

const c = [
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 300, after: 120 },
    children: [new TextRun({ text: 'Poslední maják', font: FONT, bold: true, size: 56, color: '1F4E79' })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 300 },
    children: [new TextRun({ text: 'Uživatelská příručka', font: FONT, size: 32, color: '555555' })] }),
  P('Verze hry 0.1.0 · autor: Martin Štěpánek · příloha B ročníkové práce'),

  H2('1 O hře'),
  P('Poslední maják je 3D hra, ve které plujete lodí po nekonečném moři. Rybaříte, vytahujete poklady z vraků, prodáváte je v majácích na ostrovech a za peníze si vylepšujete loď. Cestou potkáte piráty a nepřátelské ostrovy s děly a postupně odemknete příběh o starém námořníkovi a jeho ztraceném bratrovi. Hru můžete hrát sami, nebo ve dvou na jednom počítači (rozdělená obrazovka).'),
  P('Hra běží úplně offline: nevyžaduje internet, registraci ani instalaci a neshromažďuje žádné osobní údaje.'),

  H2('2 Požadavky a spuštění'),
  B('Windows 10 nebo 11 (64 bit), grafická karta s podporou DirectX 11.'),
  B('Asi 100 MB volného místa na disku (uložené hry zabírají několik MB navíc).'),
  B('Klávesnice a myš; pro druhého hráče stačí stejná klávesnice (šipky a numerická část).'),
  B('Testováno na notebooku s integrovanou grafikou Intel Iris Xe.'),
  P('Spuštění: rozbalte ZIP do libovolné složky a spusťte soubor PosledniMajak.exe. Instalace není potřeba.'),

  H2('3 Hlavní menu'),
  ...IMG('menu.png', 380, 'Hlavní menu'),
  B('Nová hra – vyberete jeden ze tří slotů pro uložení (začne od nuly, případný starý postup v slotu se přepíše).'),
  B('Pokračovat – načte uloženou hru ze zvoleného slotu.'),
  B('Multiplayer (split screen) – hra pro dva hráče na jednom počítači.'),
  B('Jazyk – tlačítko dole přepíná češtinu a angličtinu; jazyk lze změnit i v pauze.'),
  B('Konec – ukončí hru. Hra se ukládá průběžně, není třeba ukládat ručně.'),

  H2('4 Ovládání'),
  table([3400, 3100, 3100], [
    ['Akce', 'Hráč 1', 'Hráč 2'],
    ['Pohyb lodě / postavy', 'W A S D', 'šipky'],
    ['Nastoupit / vystoupit z lodě, vejít do majáku, mluvit s postavou', 'E', 'Numpad 1'],
    ['Rybařit, těžit vrak, kopat', 'Mezerník', 'Numpad 0'],
    ['Střelba (dělo lodě / zbraň v ruce)', 'levé tlačítko myši', 'Numpad *'],
    ['Oprava lodě u mola nebo v obchodě', 'R', 'Numpad /'],
    ['Velká mapa (po koupi mapy)', 'M', 'Numpad 2'],
    ['Pauza', 'Esc', 'Numpad Enter'],
  ]),
  spacer(),
  B('Kamera: pohled se otáčí při držení pravého tlačítka myši, kolečko myši přibližuje a oddaluje; při maximálním přiblížení se přepne do pohledu z první osoby.'),
  B('Plynulý pohyb jde vždy ve směru, kam se dívá kamera.'),

  H2('5 Jak se hra hraje'),
  ...IMG('hud.png', 400, 'Obrazovka hry: minimapa, zdraví, mince a lišta předmětů'),
  H3('Obrazovka'),
  B('Vlevo dole: kulatá minimapa se světovými stranami (S, J, V, Z) a ukazatele zdraví lodě a postavy.'),
  B('Vpravo nahoře: nalovené ryby, vytěžené poklady a mince.'),
  B('Dole uprostřed: lišta předmětů (zbraň, náboje, historický poklad).'),
  B('Nahoře: aktuální úkol nebo nápověda, co udělat dál.'),
  H3('Výdělek'),
  N('Plujte po moři a hledejte místa s rybami a vraky. Zastavte se na nich a držte mezerník, dokud se ukazatel nedokončí.'),
  N('Doplujte k ostrovu, u mola vystupte klávesou E a dojděte k majáku.'),
  N('V majáku prodejte kořist ve výkupně. Jedna ryba je 1 mince, jeden poklad 5 mincí.'),
  H3('Obchod v majáku'),
  ...IMG('obchod.png', 380, 'Obchod s vylepšeními'),
  P('Za mince koupíte rychlejší loď, lepší prut a rychlejší těžbu, větší lodě (malá 180, střední 450, velká 1000 mincí), náboje a zbraň. Ceny se na různých ostrovech mírně liší. V dalším pultu si můžete vzít úkoly s odměnou.'),
  H3('Piráti a nepřátelské ostrovy'),
  P('Na moři vás mohou napadnout pirátské lodě. Nepřátelské ostrovy (mají červenou vlajku na majáku) střílejí z děl. Střílíte levým tlačítkem myši a míříte podle sklonu kamery. Když loď přijde o zdraví, začnete plavat a musíte ji opravit v obchodě; když přijdete o vlastní zdraví, nastane smrt: v menu si vyberete návrat na nejbližší ostrov (mince zůstanou, kořist, náboje a vylepšení zmizí) nebo hlavní menu. Zničením posledního děla nepřátelského ostrova získáte odměnu.'),
  H3('Příběh'),
  P('U startovního ostrova sedí starý námořník. Promluvte s ním klávesou E: nejprve si musíte koupit loď a přinést mu 1 000 mincí a historický poklad. Poté dostanete souřadnice prvního ze tří velkých příběhových ostrovů, šipka na minimapě vás povede. Hra končí jedním ze dvou konců podle vaší volby.'),

  H2('6 Hra pro dva hráče'),
  P('V hlavním menu zvolte Multiplayer. Obrazovka se rozdělí: první hráč vlevo (WASD, myš), druhý vpravo (šipky a numerická klávesnice). Každý má vlastní peníze, kořist a vybavení; v pauze si mohou hráči převádět mince. Obchod, mapa nebo dialog jednoho hráče druhého nezastaví.'),

  H2('7 Uložené hry'),
  P('Hra se ukládá automaticky do tří slotů. Soubory najdete ve složce:'),
  P('C:\\Users\\<vaše jméno>\\AppData\\LocalLow\\matixiiik\\Poslední maják\\save_0.json až save_2.json', { alignment: AlignmentType.LEFT }),
  B('Zálohu uděláte zkopírováním těchto souborů.'),
  B('Když je uložení poškozené, hra to pozná a začne novou hru; proto si postup, na kterém vám záleží, zálohujte.'),
  B('Starší uložené hry se při načtení automaticky převedou na aktuální formát.'),

  H2('8 Řešení problémů'),
  table([3000, 6600], [
    ['Problém', 'Řešení'],
    ['Hra se nespustí nebo hned skončí', 'Aktualizujte ovladače grafické karty a ověřte podporu DirectX 11. Soubor PosledniMajak.exe spouštějte z rozbalené složky (ne přímo ze ZIPu).'],
    ['Hra se seká', 'Zavřete ostatní náročné aplikace. Hra ukládá průběžně na pozadí; dlouhé záseky prosím nahlaste s popisem počítače.'],
    ['Zmizel postup', 'Zkontrolujte, zda pokračujete ze správného slotu (menu Pokračovat). Zálohy uložených her viz kapitola 7.'],
    ['Nevím, kam plout', 'Klávesa M otevře mapu (po koupi mapy); klikem nastavíte cíl, na minimapě ho ukáže šipka. Nápověda k aktuálnímu úkolu je nahoře na obrazovce.'],
    ['Chci zkusit jiný jazyk', 'Tlačítko Jazyk v hlavním menu nebo v pauze.'],
  ]),
  spacer(),
  P('Pro vývojáře a zvídavé: klávesa ` (zpětný apostrof) otevře vývojářskou konzoli s příkazy pro rychlé testování (např. get money, tp x y). Konzole je jen v angličtině a není určena pro běžné hraní.'),
  P('Zdrojový kód a licence: https://github.com/matixiiik/ProjektHra (kód pod licencí MIT, modely Kenney a Quaternius pod licencí CC0).'),
];

const doc = new Document({
  creator: 'Martin Štěpánek', title: 'Poslední maják – uživatelská příručka',
  styles: { default: { document: { run: { font: FONT, size: 22 } } } },
  numbering,
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 18 })] })] }) },
    children: c,
  }],
});
d.Packer.toBuffer(doc).then(b => { fs.writeFileSync('Uzivatelska_prirucka_Posledni_majak.docx', b); console.log('OK'); });
