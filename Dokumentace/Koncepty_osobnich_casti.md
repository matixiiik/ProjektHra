# Koncepty osobních částí ročníkové práce

Tohle jsou **návrhy**, které si přepiš vlastními slovy. Části, kde se píše
„já“, mají být tvoje — u obhajoby musíš umět vysvětlit každou větu. Nic z toho
zatím není v `Rocnikova_prace_Posledni_majak.docx` (jsou tam žlutá pole).

## 1. Anglická anotace (návrh, překlad české anotace)

> The thesis describes the design and implementation of *The Last Lighthouse*
> (Czech title: *Poslední maják*), a 3D game made in Unity 6 using C#. The
> player sails across a practically endless, procedurally generated ocean,
> fishes, salvages treasure from wrecks, fights pirates and hostile islands,
> buys ship upgrades in lighthouses and completes quests. A short story about
> an old sailor's lost brother gradually unlocks and ends with a choice between
> two endings. The game supports local split-screen for two players, three save
> slots and Czech–English localisation.
>
> The documentation covers the motivation and goals, the technologies used,
> the architecture and data model, the key parts of the implementation (world
> generation, saving, economy, combat, story), the testing approach, the
> licensing and legal aspects, and a self-assessment of the result.
>
> **Keywords:** Unity, C#, procedural generation, game architecture, data
> persistence, split-screen, JSON.

(„The Last Lighthouse“ je můj překlad názvu — pokud chceš, nech i v angličtině
„Poslední maják“.)

## 2. Osobní přínos (kap. 7.3) — osnova, doplň svými slovy

Napiš 5–8 vět. Podklady z vývoje, ze kterých můžeš vyjít:

- **Co jsem se naučil:** architektura hry kolem jednoho zdroje pravdy
  (`GameSession`/`GameData`); procedurální generování (ostrovy, terén);
  práce s Gitem a pull requesty; měření výkonu (odložený zápis: z ~800 ms na
  0,13 ms); migrace dat mezi verzemi savu; automatické testy.
- **Co bylo nejtěžší:** sekání při ukládání a generování světa; velký stav hry
  (dva hráči, příběh, ekonomika) a jeho kompatibilita se starými savy; chyby,
  které se ukázaly až v buildu (shadery) nebo až testem (migrace).
- **Co bych udělal jinak:** dřív psát automatické testy; dřív dělat build a
  smoke test; dřív sjednotit data obou hráčů; průběžně psát dokumentaci.
- **Co dál:** síťový multiplayer (plán v `.claude/network-multiplayer-plan.md`),
  hudba, další mega ostrovy.

## 3. Použití AI asistenta (kap. 6.4) — nutné potvrdit u vedoucí

Fakta, ať je text pravdivý a konkrétní (uprav podle toho, co je skutečně tvoje):

- Nástroj: Claude Code (Anthropic), v terminálu a v desktopové aplikaci.
- K čemu se používal: psaní a refaktoring kódu, hledání chyb, ověřování
  v editoru přes Unity MCP, psaní testů, pracovní dokumentace, návrhy řešení.
- Co je moje: nápad hry, rozsah, příběh, herní pravidla a ekonomika, výběr
  architektury (jeden zdroj pravdy, dvě scény, JSON místo databáze), zadávání
  úkolů, rozhodování mezi variantami, kontrola a testování výsledku.
- **Musíš umět vysvětlit vlastními slovy** alespoň: jak se generuje ostrov,
  jak funguje ukládání a proč je odložené, co dělá `GameSession`, jak funguje
  migrace savu.
- **Ověř u vedoucí a ve školním řádu**, zda je rozsah použití AI přípustný a
  jak ho formálně uvést (příloha, prohlášení). Do prohlášení autora to
  v dokumentu už zmíněno je (kap. 6.4).

## 4. Prohlášení a podpis

V dokumentu je předpřipravené prohlášení. Doplň jen **místo, datum a podpis**
a před podpisem ho porovnej se skutečností (zejména větu o AI).

## 5. Otázky, které může komise dát (a stručné odpovědi k procvičení)

| Otázka | Stručná odpověď (rozveď vlastními slovy) |
|---|---|
| Proč JSON a ne databáze? | Hra je offline, jeden hráč/počítač; stav je jeden objekt `GameData`, který `JsonUtility` převede na text. Databáze by byla zbytečná složitost. |
| Proč je moře jedna plocha a ne dlaždice? | Tisíce objektů by zpomalily hru; 3D objekty mají jen ostrovy, ryby a vraky, moře je jedna plocha, která jede za hráčem. |
| Jak vzniká ostrov? | Pevné jádro + náhodné rozrůstání + zaplnění zálivů (`StampOrganicLand`); na mřížce každých 40 dlaždic s 30 % šancí a min. rozestupem 200. |
| Proč je zápis savu odložený? | Save má několik MB; zápis při každé rybě způsoboval půlvteřinové záseky. Nově se zapisuje nejvýš 1× za 8 s na pozadí. |
| Co se stane se starým savem po změně kódu? | `saveVersion` + `SaveManager.ParseAndMigrate` ho převede na aktuální formát; test to hlídá. |
| Proč `TileType` nikdy nepřečíslovat? | Ukládá se jako `int`; přečíslování by ve starém savu změnilo význam dlaždic. |
| Jak funguje split-screen? | Druhý hráč je kopie prvního bez komponent, které mají být jednou (kamera, HUD…); dvě kamery, každá půlka obrazovky; data v `players[2]`. |
| Jak jsi testoval? | Automatické testy (22 EditMode), ruční scénáře (protokol T1–T10), herní konzole s presety příběhu, smoke test buildu. |
| Co byla nejzajímavější chyba? | Save bez pole `saveVersion` se nemigroval; nebo `Shader.Find` v buildu (jen v buildu, ne v editoru). |
| Jaké licence používáš a proč? | Vlastní kód MIT (jednoduchá, permisivní); modely Kenney a Quaternius CC0. GDPR: žádné osobní údaje. |
