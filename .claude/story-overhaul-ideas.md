# Příběh od 1. mega ostrova do konce — nápady na vylepšení (26. 11. 2026)

Uživatel příběh za megalodonem a Holanďanem ještě nikdy nedohrál — teď ho projde sám
a pošle seznam změn. Tohle jsou moje návrhy, které se k jeho seznamu přidají.
**Nic z toho ještě není implementované.** Pořadí = priorita (A logika → B vzhled levně → C větší věci).

## A. Logické díry a nekonzistence (opravit vždy)

1. **„Hráč dorazil na ostrov“ není jednoznačné.** U Hřbitova lodí se `megaTask` nastaví na 1 už při
   postavení ostrova (hráč může být 400 políček daleko). Deník jsem 26. 11. opravil (`storyStep >= 3`),
   ale zbytek kódu (HUD, StoryNpc, hinty) to bere různě. → Zavést jednu metodu
   `GameData.IsOnCurrentMegaIsland()` a používat ji všude.
2. **Děda po 1. ostrově mlčí.** HUD při `megaTask >= 3` píše „Vrať se za dědou“, ale po přečtení dopisu
   `GiveNextMegaIsland` rovnou posune hru dál (`storyStep = 2`) — návrat za dědou nic nedělá. Rozhodnout:
   buď děda dostane repliky po každém ostrově (doporučeno, posílí příběh), nebo ten řádek pryč.
3. **Megalodon / Holanďan — spouštěče podle vzdálenosti** (`StoryEvents`): ověřit, že se nemohou
   spustit dvakrát, po smrti hráče, po načtení savu uprostřed souboje, ani ve split-screenu u P2.
4. **Smrt uprostřed příběhu** smaže výbavu (náboje, loď) → na 3. ostrově se hráč nemusí dát do kupy.
   Návrh: „kontrolní bod kapitoly“ — při startu kapitoly záloha výbavy, respawn u posledního mega
   ostrova s veslicí + základní municí.
5. **Konec hry:** po volbě ušetřit/zabít jen jiná replika dědy. Chybí pocit „dohráno“.
6. Projet presety `story 4–10` jeden po druhém a zkontrolovat, že každý sedí na aktuální kód.

## B. Velký efekt za málo práce (vzhled a atmosféra)

1. **Úvodní karta kapitoly** při připlutí k mega ostrovu: pergamenový pruh „Kapitola 4 — Pirátský
   ostrov“ (názvy už jsou v `JournalScreen`), pomalé ztmavení + zvukový akord.
2. **Checklist úkolů ostrova v HUD** místo jedné věty: „Děla 2/4 · Stráže 5/6 · Hlídková loď ✗“.
3. **Barevná nálada podle ostrova** (URP Volume / mlha / barva světla): Pirátský ostrov teplý západ
   slunce, Hřbitov lodí studená zelenošedá mlha, Poslední ostrov bouře (tmavé nebe, déšť částicemi,
   blesky = záblesk světla). Nejvíc „wow“ za nejmíň kódu.
4. **Megalodon:** stín pod hladinou a ploutev dřív, než zaútočí; varovná hláška „Voda se čeří…“;
   po poražení trofej (nový předmět kořisti „Zub megalodona“, drahý).
5. **Holanďan:** při odjezdu se nahrne mlha, zelené světlo lucerny, děla svítí zeleně; po potopení
   mlha odejde.
6. **Hudba generovaná kódem** (SoundManager už umí tóny): jednoduchá smyčka akordů pro každou
   kapitolu + napětí v souboji s bossem.
7. Vlajky a plachty se vlní (shader / jednoduchá animace vrcholů), pěna u břehu.

## C. Větší novinky (aby byl příběh „propracovaný“)

1. **Bratrův deník — sběratelské stránky** (5–6 kusů) schované na ostrovech po cestě; v Deníku se
   skládají do příběhu, proč bratr odešel. Přidává průzkum a vysvětluje motivaci bez dlouhých monologů.
2. **Děda reaguje na každý ostrov** (krátký dialog po návratu nebo „dopis po rackovi“ přes LetterScreen).
3. **Souboj s bratrovou lodí jako finále:** dvě fáze (loď → bratr na břehu), hláška/monolog během
   souboje, volba jako pergamenový dopis se dvěma tlačítky.
4. **Dva viditelné konce:** ušetřit → bratr se objeví na startovním ostrově vedle dědy (vlastní repliky);
   zabít → na jeho ostrově zůstane vrak a hrob, děda smutnější replika.
5. **Závěrečná obrazovka:** pergamen „Konec“ se statistikami (dny na moři, ryby, potopení piráti,
   mince, zvolený konec) + titulky, pak volné hraní dál.
6. **Volitelný „hard mode“ / New Game+** po dohrání (piráti silnější, lepší kořist).

## Jak dál
1. Uživatel dohraje příběh od `story 4` a pošle, co mu vadí.
2. Spojit jeho seznam s body A–C, rozdělit na fáze (A celé → B → C), každou fázi do vlastní větve/PR.
3. Ke každé fázi: kompilace, testy, Play přes MCP se screenshoty (před/po), záloha savů.
