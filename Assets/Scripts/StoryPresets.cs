using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryPresets.cs
//  Presety pro herní konzoli: `story 1` … `story 10` přenesou hráče přímo na místo,
//  kde se daná část příběhu odehrává, a dají mu všechno, co tam potřebuje (loď,
//  náboje, zbraň, vylepšení, mapu…). Slouží k testování — každou část hry jde takhle
//  samostatně vyzkoušet, aniž by se hrálo všechno od začátku.
//
//  Preset je IDEMPOTENTNÍ: jde spustit opakovaně, vždycky nejdřív uklidí předchozí
//  příběhové objekty (marker ostrova, strážce, děla, trezor, obludu…) a vynuluje
//  příběhový postup. Mince a ostatní věci se přepíšou podle presetu.
//
//   1  startovní ostrov, veslice                     (děda ještě nic nechce)
//   2  startovní ostrov, malá loď                    (děda: přines 1000 mincí + poklad)
//   3  startovní ostrov, střední loď + hist. poklad + 1000 mincí (jde ho odevzdat)
//   4  100 políček od Pirátského ostrova, střední loď, náboje, zbraň
//   5  za Pirátským ostrovem, velká loď — megalodon (objeví se po ~15 políčkách plavby)
//   6  100 políček od Hřbitova lodí, plná výbava (dopis s hádankou je v Deníku)
//   7  30 políček od Hřbitova lodí — kousky mapy v mělčině (megaTask 1)
//   8  30 políček od Hřbitova — všechny kousky mapy jsou sebrané, čeká podpalubí a vzkaz
//      (po přečtení vzkazu a odplutí přepadne hráče Bludný Holanďan)
//   9  100 políček od posledního ostrova — Bludný Holanďan zaútočí hned po vyplutí
//  10  30 políček od posledního ostrova (konfrontace s bratrem)
// ─────────────────────────────────────────────────────────────────────────────

public static class StoryPresets
{
    public const int COUNT = 10;

    private const float FAR  = 100f; // "100 políček" před ostrovem
    private const float NEAR = 30f;  // kousek před ostrovem

    /// <summary>Provede preset `n` (1–10) pro hráče 1. Vrací popis pro konzoli.</summary>
    public static string Apply(int n, GridManager grid, PlayerController player, ShipModelSwitcher switcher)
    {
        if (grid == null || player == null) return "Preset nejde použít — chybí GridManager / hráč.";
        var d = grid.gameData;

        ClearStoryObjects();
        ResetStory(d);

        // Polohy mega ostrovů (stejné vzorce jako ve hře — GridManager.FirstMegaIslandPos / NextMegaIslandPos).
        Vector2Int i0 = GridManager.FirstMegaIslandPos(0, 0);
        Vector2Int i1 = GridManager.NextMegaIslandPos(i0.x, i0.y, 1);
        Vector2Int i2 = GridManager.NextMegaIslandPos(i1.x, i1.y, 2);

        string desc;
        switch (n)
        {
            case 1:
                SetGear(d, 0, false, false);
                d.storyStep = 0;
                PutOnStartIsland(grid, player);
                desc = "Startovní ostrov, veslice. Děda ještě nic nechce.";
                break;

            case 2:
                SetGear(d, 1, false, false);
                d.storyStep = 1;
                d.coins = 200;
                PutOnStartIsland(grid, player);
                desc = "Startovní ostrov, malá loď. Děda chce 1000 mincí + historický poklad.";
                break;

            case 3:
                SetGear(d, 2, false, false);
                d.storyStep = 1;
                d.coins = 1000;
                d.hasHistoricalTreasure = true;
                PutOnStartIsland(grid, player);
                desc = "Startovní ostrov, střední loď, historický poklad + 1000 mincí — jde ho odevzdat dědovi.";
                break;

            case 4:
                SetGear(d, 2, true, false);
                PlaceIslands(grid, 0);
                d.storyStep = 2;
                d.hasWaypoint = true; d.waypointX = i0.x; d.waypointY = i0.y;
                PutInBoat(grid, player, i0 - Direction(Vector2Int.zero, i0) * FAR);
                desc = $"100 políček od Pirátského ostrova [{i0.x}, {i0.y}], střední loď, náboje, zbraň.";
                break;

            case 5:
                SetGear(d, 3, true, false);
                PlaceIslands(grid, 1);
                MarkLetter(d, i1);
                // Hráč stojí těsně za Pirátským ostrovem na trase k dalšímu — megalodon se objeví po ~15 políčkách plavby.
                PutInBoat(grid, player, i0 + Direction(i0, i1) * 20f);
                desc = $"Za Pirátským ostrovem, velká loď. Popluj po trase k [{i1.x}, {i1.y}] — megalodon se objeví po ~15 políčkách.";
                break;

            case 6:
                SetGear(d, 3, true, true);
                PlaceIslands(grid, 1);
                MarkLetter(d, i1);
                d.ambush1Done = true;
                PutInBoat(grid, player, i1 - Direction(i0, i1) * FAR);
                desc = $"100 políček od Hřbitova lodí, plná výbava. Cíl je [{i1.x}, {i1.y}] (dopis je v Deníku, waypoint není nastaven).";
                break;

            case 7:
                SetGear(d, 3, true, true);
                PlaceIslands(grid, 1);
                MarkLetter(d, i1);
                d.ambush1Done = true;
                d.storyStep = 3;
                PutInBoat(grid, player, i1 - Direction(i0, i1) * NEAR);
                desc = "30 políček od Hřbitova lodí — v mělčině hledej kousky mapy (Space na vraku).";
                break;

            case 8:
                SetGear(d, 3, true, true);
                PlaceIslands(grid, 1);
                MarkLetter(d, i1);
                d.ambush1Done = true;
                d.storyStep = 3;
                d.megaTask = 2;          // všechny kousky mapy sebrané
                d.megaCluesMask = 7;
                PutInBoat(grid, player, i1 - Direction(i0, i1) * NEAR);
                desc = "30 políček od Hřbitova lodí, mapa složená — najdi podpalubí, přečti vzkaz a odplouvej (přepadne tě Holanďan).";
                break;

            case 9:
                SetGear(d, 3, true, true);
                PlaceIslands(grid, 2);
                MarkLetter(d, i1);
                d.ambush1Done = true;
                d.storyStep = 2;
                d.hasWaypoint = true; d.waypointX = i2.x; d.waypointY = i2.y;
                PutInBoat(grid, player, i2 - Direction(i1, i2) * FAR);
                desc = $"100 políček od posledního ostrova [{i2.x}, {i2.y}] — Bludný Holanďan zaútočí hned po vyplutí.";
                break;

            default: // 10
                SetGear(d, 3, true, true);
                PlaceIslands(grid, 2);
                MarkLetter(d, i1);
                d.ambush1Done = true;
                d.ambush2Done = true;
                d.storyStep = 3;
                PutInBoat(grid, player, i2 - Direction(i1, i2) * NEAR);
                desc = $"30 políček od posledního ostrova [{i2.x}, {i2.y}] — konfrontace s bratrem (potop jeho loď).";
                break;
        }

        if (switcher != null) switcher.Apply();
        grid.Save();
        grid.NotifyWorldChanged();
        return $"<color=#44ff44>story {n}</color>: {desc}";
    }

    // ── Stav hry ─────────────────────────────────────────────────────────────
    // Vynuluje příběhový postup (ne mince ani upgrady — ty nastaví SetGear).
    private static void ResetStory(GameData d)
    {
        d.storyStep = 0;
        d.hasHistoricalTreasure = false;
        d.storyIslandActive = false;
        d.megaIndex = 0; d.megaTask = 0; d.megaCode = 0; d.megaCluesMask = 0;
        d.ambush1Done = false; d.ambush2Done = false;
        d.storyDone = false; d.storyEnding = 0;
        d.hasLetter = false; d.letterX = 0; d.letterY = 0;
        d.routeStartSet = false; d.routeStartX = 0; d.routeStartY = 0;
        d.hasWaypoint = false;
        d.boatWrecked = false; d.boatNeedsRehome = false;
        d.boatHealth = 100; d.playerHealth = 100;
        d.activeHotbarSlot = -1;
    }

    // Výbava hráče 1: loď, mapa, případně náboje + zbraň a všechna vylepšení.
    private static void SetGear(GameData d, int shipLevel, bool ammoAndWeapon, bool fullUpgrades)
    {
        d.shipLevel = shipLevel;
        d.hasMap = true; // mapa je u všech částí příběhu

        d.hasHandWeapon = ammoAndWeapon;
        d.ammo          = ammoAndWeapon ? 80 : 0;
        d.handAmmo      = ammoAndWeapon ? 80 : 0;

        d.hasSpeedUpgrade = d.hasRodUpgrade = d.hasMiningUpgrade = fullUpgrades;
        d.sellBonus = fullUpgrades;
        if (fullUpgrades) d.coins = Mathf.Max(d.coins, 500);
    }

    // Dopis z trezoru je přečtený (hádanka s polohou Hřbitova lodí, jde znovu přečíst v Deníku).
    private static void MarkLetter(GameData d, Vector2Int island)
    {
        d.hasLetter = true;
        d.letterX = island.x;
        d.letterY = island.y;
    }

    // ── Ostrovy ──────────────────────────────────────────────────────────────
    // Postaví Pirátský ostrov a pak stejným krokem jako ve hře (GiveNextMegaIsland) posune
    // příběh na další ostrovy až po číslo `upToIndex`. Waypoint na další ostrov se nenastavuje
    // (hádanka z dopisu) — presety si ho případně nastaví samy.
    private static void PlaceIslands(GridManager grid, int upToIndex)
    {
        var d = grid.gameData;
        Vector2Int i0 = GridManager.FirstMegaIslandPos(0, 0);
        grid.PlaceMegaIsland(i0.x, i0.y); // marker ostrova 0 (megaIndex = 0)
        d.megaIndex = 0;
        d.megaTask  = 0;
        d.storyStep = 2;
        for (int k = 1; k <= upToIndex; k++) grid.GiveNextMegaIsland(false);
    }

    // Uklidí všechno, co po sobě zanechal předchozí příběhový ostrov / přepadení.
    private static void ClearStoryObjects()
    {
        if (MegaIslandMarker.Instance != null) Object.Destroy(MegaIslandMarker.Instance.gameObject);
        if (SeaMonster.Instance != null)       Object.Destroy(SeaMonster.Instance.gameObject);
        if (RivalNpc.Instance != null)         Object.Destroy(RivalNpc.Instance.gameObject);

        foreach (var v in Object.FindObjectsByType<VaultMechanism>(FindObjectsSortMode.None))
            Object.Destroy(v.gameObject);

        foreach (var c in Object.FindObjectsByType<HostileIslandCannon>(FindObjectsSortMode.None))
            if (c.islandKey == "mega") Object.Destroy(c.gameObject);

        for (int i = LandGuard.All.Count - 1; i >= 0; i--)
            if (LandGuard.All[i] != null) Object.Destroy(LandGuard.All[i].gameObject);

        for (int i = PirateShip.All.Count - 1; i >= 0; i--)
        {
            var s = PirateShip.All[i];
            if (s != null && (s.guardIslandKey == "mega" || s.isGhost)) Object.Destroy(s.gameObject);
        }

        // Hradby a trosky (kořenové objekty scény) po předchozím ostrově. Nejdřív se
        // vypnou — Destroy se provede až na konci snímku, ale nový marker se spouští
        // dřív a `GameObject.Find("Walls_…")` by ještě našlo starou hradbu (a novou
        // nepostavilo). Vypnuté objekty Find nevrací.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t != null && t.parent == null && (t.name.StartsWith("Walls_") || t.name == "WreckPiece"))
            {
                t.gameObject.SetActive(false);
                Object.Destroy(t.gameObject);
            }
    }

    // ── Umístění hráče ───────────────────────────────────────────────────────
    // Směr od bodu `from` k bodu `to` (jednotkový; když splývají, dolů).
    private static Vector2 Direction(Vector2Int from, Vector2Int to)
    {
        Vector2 dir = new Vector2(to.x - from.x, to.y - from.y);
        return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.down;
    }

    // Hráč pěšky na startovním ostrově, loď (veslice…) stojí ve vodě u mola.
    private static void PutOnStartIsland(GridManager grid, PlayerController player)
    {
        var d = grid.gameData;
        player.TeleportTo(0, 0); // vygeneruje svět kolem počátku

        Vector2Int spot = grid.NearestHarborTile(0, 0) ?? Vector2Int.zero;
        var water = grid.FindWaterNextTo(spot.x, spot.y);

        d.playerGridX = spot.x; d.playerGridY = spot.y;
        d.boatGridX = water != null ? water.Value.x : spot.x;
        d.boatGridY = water != null ? water.Value.y : spot.y;
        d.isOnFoot = true;
        player.ReloadFromData();
    }

    // Hráč v lodi na vodním políčku nejblíž danému bodu.
    private static void PutInBoat(GridManager grid, PlayerController player, Vector2 point)
    {
        var d = grid.gameData;
        int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
        player.TeleportTo(x, y); // vygeneruje svět kolem cíle, ať je kde hledat vodu

        Vector2Int spot = FindWater(grid, x, y);
        d.playerGridX = spot.x; d.playerGridY = spot.y;
        d.boatGridX   = spot.x; d.boatGridY   = spot.y;
        d.isOnFoot = false;
        player.ReloadFromData();
    }

    // Nejbližší vodní políčko od [x, y] (po čtvercových prstencích, do vzdálenosti 30).
    private static Vector2Int FindWater(GridManager grid, int x, int y)
    {
        for (int r = 0; r <= 30; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    TileType t = grid.GetTileType(x + dx, y + dy);
                    if (t == TileType.Water || t == TileType.Water_Fish) return new Vector2Int(x + dx, y + dy);
                }
        return new Vector2Int(x, y);
    }
}
