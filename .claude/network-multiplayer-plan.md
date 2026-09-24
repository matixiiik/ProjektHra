# Síťový multiplayer — plán (zatím jen návrh, NEIMPLEMENTOVÁNO)

Cíl (zadání uživatele): dva samostatné počítače přes síť, každý hraje "jako
svůj singleplayer" (vlastní kamera, vlastní pohled), ale oba na SPOLEČNÉ
mapě — vidí se navzájem, sdílí svět. Jiná věc než dnešní lokální split-screen
(`MultiplayerManager`) — tam běží obě instance `PlayerController` v jednom
Unity procesu na jednom PC, tady by šlo o dva samostatné Unity procesy na
dvou počítačích, co si musí povídat po síti.

**Status: čeká.** Podle domluvy s uživatelem (2026-09-24) se na tomhle
nezačne dřív, než branch `claude/upbeat-brown-gd0t07` (grafika/výkon/
lokalizace — druhá souběžná session) dojede a smerguje. Síťový multiplayer
by sahal do skoro stejných souborů (`PlayerController`, `GridManager`,
`GameData`, `CombatDirector`) — souběh by znamenal obrovské merge konflikty.

## Realita dnešní architektury (proč to není "jen přidat pár řádků")

- **Jeden lokální JSON soubor** (`SaveManager` → `Application.persistentDataPath
  /save_N.json`) = jediný zdroj pravdy. Žádný koncept "cizí hráč na jiném
  počítači".
- **Generování světa je deterministické** (stejný seed + algoritmus →
  stejná dlaždice na stejných souřadnicích) — TOHLE se dá mezi dvěma
  počítači "syncnout zadarmo", stačí stejný seed. Ale **mutovatelný stav**
  (vylovená ryba, vytěžený poklad, otevřená bedna, vyčištěný ostrov,
  mega quest) se dnes mění jen v lokální kopii — na dvou PC by se rozjel
  do dvou různých verzí světa, kdyby se neřešilo sdílení.
- **Souboj** (`CombatDirector`, piráti, děla) běží čistě lokálně per Unity
  instance — žádná autorita, žádný koncept "kdo rozhoduje o zásahu".
- **V projektu není žádný síťový balíček** (Netcode for GameObjects, Mirror,
  Fish-Net...) — začínalo by se od nuly.
- **`MultiplayerManager` dnešní split-screen NENÍ základ pro tohle** — klonuje
  `PlayerController` v JEDNOM procesu a čte/píše do JEDNÉ `GameData`. Síťová
  verze potřebuje úplně jiný mechanismus (replikace přes síť, ne `Instantiate`
  v paměti).

## Doporučená architektura: Unity Netcode for GameObjects (NGO), host-autoritativní

**Proč NGO, a ne vlastní socket kód napsaný od nuly:** psaní vlastního
síťového protokolu (TCP/UDP, serializace, pořadí zpráv, výpadky spojení) je
obrovský zdroj jemných, těžko odhalitelných bugů — a bez Unity/dvou počítačů
po ruce (viz níže) bych je nemohl ověřit. NGO je oficiální, dobře
zdokumentovaný Unity balíček — řeší spojení/serializaci/replikaci za nás,
zbyde jen napojit HERNÍ logiku, což jde líp obhájit u zkoušky ("použil jsem
standardní řešení a napsal nad ním herní kód", ne "napsal jsem vlastní síťovou
vrstvu, co nikdo neprověřil").

**Princip:** jeden hráč je Host (server + klient v jednom), druhý se
připojí jako Client. Hostova `GameData`/svět je JEDINÝ zdroj pravdy;
klientova kopie je jen "pohled" (view) na replikovaná data, ne nezávislý
save. Souboj/piráti/generování mutovatelného stavu běží jen na hostovi,
klientovi se jen posílají výsledky.

### Fázový postup (MVP první, ne všechno najednou)

1. **Fáze 0 — příprava**: nainstalovat balíček `com.unity.netcode.gameobjects`
   (Package Manager, potřebuje otevřenou Unity), rozhodnout LAN (přímá IP,
   jednoduché) vs internet (potřebuje Unity Relay/Unity Gaming Services účet
   — zdarma, ale je to další účet a nastavení navíc).
2. **Fáze 1 — MVP, jen "vidím tě"**: Host + Client se spojí, každý vidí
   toho druhého jako pohybující se postavičku/loď na stejné mapě (pozice +
   natočení, žádná interakce). Nejmenší smysluplný krok, dá se samostatně
   otestovat, než se staví dál.
3. **Fáze 2 — sdílený svět**: události měnící dlaždice (rybaření, těžba,
   otevření bedny) se z klienta pošlou hostovi (RPC), host je vyhodnotí a
   výsledek (i s odměnou) pošle zpátky oběma — stejná dlaždice je pak
   vyřízená pro oba. Nutná domluva: **oddělené ekonomiky per hráč** (jako
   dnešní split-screen) nebo **jedna sdílená**?
4. **Fáze 3 — souboj**: piráti/děla se simulují jen na hostovi
   (`CombatDirector` → host-only), klientovi se replikují jen pozice a
   výsledky zásahů. Autorita nad poškozením = host.
5. **Fáze 4 — obchody/maják/příběh**: nákupy/quest odevzdání jdou přes
   RPC na hosta (autoritativně ověří, že hráč má dost mincí), místo
   přímého zápisu do lokální `GameData`.
6. **Fáze 5 — ukládání**: jen host ukládá sdílený svět. Klient buď nemá
   vlastní save (jen se připojuje k hostovu běhu), nebo se řeší zvlášť —
   **rozhodnutí uživatele nutné**.
7. **Fáze 6 — doladění**: výpadek spojení / reconnect, vyhlazení pohybu
   druhého hráče (interpolace, ať loď při menší latenci neškube).

### Co se musí přepracovat v existujícím kódu (velký zásah, ne kosmetika)

- `GameSession`/`GameData` — dnes jen `IsMultiplayer` (split-screen).
  Přibude třetí režim a jasné oddělení "moje lokální data" vs "hostova
  sdílená data".
- `PlayerController` — dnes čte/píše přímo do `gameData` podle
  `playerIndex` (0/1). Druhý hráč v síťovém režimu potřebuje být
  `NetworkObject` s `NetworkVariable`/RPC, ne lokální pole v jedné `GameData`.
- `GridManager` — generování terénu zůstává "zadarmo" díky determinismu,
  ale mutovatelný stav (vylovené, vytěžené, otevřené, vyčištěné) musí jít
  přes hosta.
- `SaveManager` — save = jen host.
- `CombatDirector` — jen host simuluje, klient přehrává.
- UI (`HUDCounter`, minimapa, obchody) — většinu jde znovupoužít, jen
  musí vědět, jestli čte "moje lokální" nebo "replikovaná sdílená" data.

### Otevřené otázky pro uživatele (rozhodnutí nutná PŘED kódováním)

1. LAN (stejná síť, přímá IP) jen pro začátek, nebo rovnou i přes internet
   (Unity Relay)?
2. Oddělené ekonomiky per hráč (jako dnešní split-screen), nebo jedna
   sdílená pokladna?
3. Ukládá jen host, nebo si i klient něco pamatuje?
4. Jak se síťová hra spouští v UI — zadání IP adresy? Tlačítka
   "Hostovat" / "Připojit se" v `MainMenuManager`?
5. Tempo: postupovat fázi po fázi s otestováním v Unity po každé (nutné —
   bez Unity a bez dvou reálných počítačů nejde tohle ověřit vůbec), nebo
   zkusit víc najednou a riskovat, že se to na první pokus nerozjede?

## Kdy na tohle sáhnout

Až se sjednotí `claude/upbeat-brown-gd0t07` s prací druhé session (grafika/
výkon/lokalizace) do `main` — jinak hrozí obří merge konflikty ve stejných
souborech. Až se to sjednotí, projít tenhle plán znovu (kód se mezitím
mohl posunout) a rozhodnout otázky výš.
