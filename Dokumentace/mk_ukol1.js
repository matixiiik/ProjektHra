// Generuje Ukol1_Vymezeni_tematu.docx (1 strana, prosty text bez tabulek)
const fs = require('fs');
const { Document, Packer, Paragraph, TextRun, BorderStyle, AlignmentType } = require('docx');
const F = 'Calibri';
const P = (t, o = {}) => new Paragraph({ spacing: { after: 100, line: 276 }, alignment: AlignmentType.LEFT, ...o, children: Array.isArray(t) ? t : [new TextRun({ text: t, font: F, size: 22 })] });
const H = (t) => new Paragraph({ keepNext: true, spacing: { before: 200, after: 60 }, children: [new TextRun({ text: t, font: F, bold: true, size: 26, color: '1F4E79' })] });
const b = (t) => new TextRun({ text: t, font: F, size: 22, bold: true });
const r = (t) => new TextRun({ text: t, font: F, size: 22 });
const doc = new Document({ sections: [{
  properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1000, bottom: 900, left: 1200, right: 1200 } } },
  children: [
    new Paragraph({ spacing: { after: 40 }, children: [new TextRun({ text: 'Poslední maják', font: F, bold: true, size: 44, color: '1F4E79' })] }),
    new Paragraph({ spacing: { after: 120 }, border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: '1F4E79', space: 2 } }, children: [new TextRun({ text: 'IT projekt – úkol 1: téma, vize a cílová skupina  |  Martin Štěpánek', font: F, size: 20, color: '555555' })] }),

    H('Téma'),
    P('Poslední maják je 3D hra v Unity, ve které plavíte lodí po nekonečném moři. Rybaříte, vytahujete poklady z vraků, prodáváte je v majácích na ostrovech a za peníze si vylepšujete loď. Moře se generuje samo podle toho, kam jedete, takže nikdy nedojde. Do toho přicházejí piráti a nepřátelské ostrovy s děly a postupně se odemyká příběh o starém námořníkovi a jeho ztraceném bratrovi. Hra se dá hrát i ve dvou na jednom počítači.'),

    H('Pro koho je a co mu přinese'),
    P('Pro hráče, kteří si chtějí v klidu zahrát bez stresu a bez učení složitého ovládání – ideálně kolem 12 až 25 let, a pro dva kamarády, kteří chtějí něco hrát společně na jednom PC. Přinese jim uvolnění, jasný cíl (lepší loď, další ostrov) a příběh, který dává důvod plout dál. Hra běží offline, není třeba nic instalovat ani se registrovat. Mým cílem je při tom ukázat celý postup tvorby většího softwaru, od nápadu až po hotový build, dokumentaci a licence.'),

    H('Je to zvládnutelné?'),
    P([b('Co už je hotové. '), r('Základ (svět, plavba, rybaření, obchody, ukládání do slotů, split-screen) vznikl na jaře. V září jsem přidal souboje s piráty a ostrovy, celý příběh se třemi speciálními ostrovy a dvěma konci, češtinu s angličtinou a zrychlil jsem hru, aby neseklo generování světa a ukládání. Hru už lze dohrát od začátku do konce.')]),
    P([b('Co mě čeká. '), r('Kód už nebudu výrazně rozšiřovat, zbývá hlavně to, co k projektu patří okolo:')]),
    P([b('Říjen – listopad: '), r('project charter, schéma architektury, přesun repozitáře na školní GitHub, výběr licence, prezentace na 11. 11.')], { indent: { left: 360 }, spacing: { after: 50 } }),
    P([b('Prosinec – leden: '), r('osnova a první kapitoly dokumentace, datový model, use cases a wireframy, evidence použitých assetů.')], { indent: { left: 360 }, spacing: { after: 50 } }),
    P([b('Únor – březen: '), r('oprava chyb, úklid a okomentování kódu, testování, první testovací build a draft celé dokumentace.')], { indent: { left: 360 }, spacing: { after: 50 } }),
    P([b('Duben: '), r('finální release, uživatelská příručka, závěrečná dokumentace a nácvik obhajoby.')], { indent: { left: 360 } }),
    P([b('Rizika. '), r('Největší je čas: dokumentace zabere víc, než čekám, a tak nechci přidávat nové funkce. Menší riziko je výkon na slabších počítačích, na kterém jsem už pracoval. Modely a postavy jsou od autorů Kenney a Quaternius pod licencí CC0, takže s autorskými právy problém nevidím. Pracuji v Unity a C#, které znám, a hra nepotřebuje žádný server ani cizí službu.')]),
  ],
}]});
Packer.toBuffer(doc).then(x => { fs.writeFileSync('Ukol1_Vymezeni_tematu.docx', x); console.log('OK'); });
