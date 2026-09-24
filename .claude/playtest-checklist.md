# Playtest checklist

Hraj a odškrtávej / dopisuj poznámky přímo sem (nebo do kopie). Cíl: dostat
konkrétní, akční seznam věcí k opravě — ne obecný dojem. U každého bugu
napiš **jak ho zopakovat** (co jsi dělal, kde, jaký byl výsledek vs. co jsi
čekal) — bez toho se to špatně hledá v kódu.

Testuj na `claude/upbeat-brown-gd0t07` (až bude smergovaný PR #1 — ať
netestuješ věci, co jsou už opravené). Grafiku/výkon/lokalizaci od
Notebook 4 zatím neřeš — to je jiná session, jiný účel.

## A. Základní smyčka (nová hra, prvních 15 minut)

- [ ] Veslice na startu — pohyb, otáčení kamerou, jede to plynule?
- [ ] Rybaření (Space na `Water_Fish`) — jasné, kdy to jde a kdy ne?
- [ ] Těžba vraku (Space na `Treasure`) — stejně.
- [ ] Prodej ve výkupně (maják) — ceny dávají smysl, tlačítka fungují?
- [ ] Koupě první lodi (small) — dost peněz na to za rozumnou dobu hraní?
- [ ] Dialog s dědou (E) — text dává smysl, jasné, co mám udělat dál?
- [ ] `M` otevře mapu (s koupenou mapou) — waypoint funguje, šipka na minimapě sedí?

**Poznámky/bugy:**

## B. Ekonomika a obchody

- [ ] Ceny na různých ostrovech se opravdu liší (cenový level)?
- [ ] Questy (obchod s questy) — zadání jasné, odměna sedí po splnění?
- [ ] Mega quest (mapa z bedny) — dopluj, vykopej, vyplať — všechno jasné?
- [ ] Upgrady (rychlost/prut/těžba) — cítíš rozdíl po koupi?
- [ ] Pěší zbraň + náboje — koupě, střelba, hotbar (klávesy 1/2/3) — jasné?
- [ ] Munice do lodního děla — teď by měla být vidět v pravém horním rohu
      za plavby s dělem (**oprava z PR #1** — zkontroluj, že se fakt objeví).

**Poznámky/bugy:**

## C. Souboj

- [ ] Nepřátelský ostrov — pozná se dopředu (červená vlajka, červená tečka
      na minimapě), nebo tě překvapí?
- [ ] Souboj s dělem lodi — trefit se, kolize, zvuk, feedback při zásahu
      (červený záblesk, cuknutí kamery) — funguje to?
- [ ] Piráti na moři — spawnují se v rozumné frekvenci? Boss health bar?
- [ ] Zničení posledního děla ostrova — "vyčištěno", hlídky se uvolní?
- [ ] Rozbití lodi (HP na 0) — panáček plave, trosky s nákladem jdou zachránit?
- [ ] Smrt (HP panáčka na 0) — respawn u nejbližšího ostrova, ztráta kořisti
      **a teď i pěší zbraně** (oprava z PR #1) — zkontroluj.

**Poznámky/bugy:**

## D. Příběh (celý oblouk — počítej s delší seancí)

- [ ] Krok 1→2: donesl jsi 1000 mincí + historický poklad, dostal souřadnice?
- [ ] Ostrov 1 (Piráti): obrana (děla/strážci/hlídka), puzzle s ozubenými
      koly u trezoru — **fér, nebo hádání?** Kolik minut ti to zabralo?
- [ ] Vzkaz v trezoru → návrat za dědou → souřadnice ostrova 2.
- [ ] Přepad na moři mezi ostrovy — objevil se, dával smysl?
- [ ] Ostrov 2 (Hřbitov lodí): Bludný Holanďan, 3 kusy mapy, podpalubí.
- [ ] Ostrov 3 (Konfrontace): bratrova loď, `RivalNpc` dialog, volba
      ušetřit/zabít — **text dává emočně smysl?** Odměna přiměřená?
- [ ] Deník (tlačítko v pauze) — ukazuje správný postup, nic nespoiluje?

**Poznámky/bugy:**

## E. Split-screen (na dvou sadách kláves, i sám na jedné klávesnici)

- [ ] P2 ovládání (šipky, Numpad) — funguje úplně všechno, co má P1?
- [ ] Obchod/mapa/dialog jednoho hráče **nezamrzne** druhého (zkus otevřít
      obchod P1, zatímco P2 dál pluje).
- [ ] **Smrt jednoho hráče nezamrzne druhého** (oprava z PR #1 — hlavní věc
      k ověření). Umři jako P1, sleduj jestli P2 dál hraje normálně.
- [ ] **Esc během dialogu/trezoru zavře jen to okno, neotevře navrch
      pauzu** (oprava z PR #1).
- [ ] Vstup do majáku v coopu — oba v jedné kulaté místnosti, funguje to?
- [ ] Převod peněz mezi hráči (pauza) — funguje?

**Poznámky/bugy:**

## F. Celkový dojem (subjektivní, ale důležité pro Krok 9 doladění)

- Tempo příběhu — vleče se někde, nebo je to naopak moc rychlé?
- Balance — je někde moc snadné/těžké vydělat, nebo moc drahé/levné nakoupit?
- Cokoliv, co tě jako hráče vyloženě otravovalo nebo zmátlo.

**Poznámky:**

---

Až to doděláš, pošli mi to zpátky (commitni tenhle soubor s vyplněnými
poznámkami, nebo mi text jen nalep do zprávy) — z toho uděláme konkrétní
seznam oprav.
