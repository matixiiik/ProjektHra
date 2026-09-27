using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryEvents.cs
//  Centralizuje kontroly "má se tu teď stát něco z příběhu", volané z
//  PlayerController.OnEnteredTile (při každém přechodu na nové políčko).
//  Zatím jen mořská obluda (megalodon) hned po Pirátském ostrově — na cestě
//  k dalšímu mega ostrovu (Krok 5, viz story-plan.md §4).
// ─────────────────────────────────────────────────────────────────────────────

public static class StoryEvents
{
    private const float ROUTE_RANGE    = 30f;  // jak blízko trasy start→cíl obluda čeká
    private const float MIN_FROM_START = 35f;  // obluda se objeví, až hráč od startu trasy odplul aspoň tolik (tj. "za prvním ostrovem")
    private const float SPAWN_DISTANCE = 40f;  // jak daleko od hráče se obluda vynoří

    /// <summary>Vyvolá mořskou obludu, když hráč pluje po trase od Pirátského ostrova k dalšímu
    /// mega ostrovu a ještě obludu neporazil. Bezpečné volat opakovaně — sama si pohlídá,
    /// že existuje nejvýš jedna.</summary>
    public static void CheckMonster(GridManager grid)
    {
        if (SeaMonster.Instance != null) return; // už existuje

        var d = grid.gameData;
        if (d.storyStep != 2 || d.megaIndex != 1 || d.ambush1Done || !d.storyIslandActive) return;

        // Trasa vede od předchozího mega ostrova (start) k tomu novému; u starých savů bez
        // zapamatovaného startu se bere počátek světa (0,0) jako dřív.
        Vector2 start  = d.routeStartSet ? new Vector2(d.routeStartX, d.routeStartY) : Vector2.zero;
        Vector2 target = new Vector2(d.storyIslandX, d.storyIslandY);

        PlayerController player = NearestSailingPlayerNearRoute(start, target);
        if (player == null) return;

        Vector3? spawn = FindWaterSpawn(grid, player);
        if (spawn == null) return; // teď není kam (samá souš) — zkusí se při dalším kroku
        SeaMonster.Spawn(spawn.Value);
    }

    // Bludný Holanďan (hlídač ostrova 2) — napadne hráče při odjezdu z Hřbitova lodí, na cestě
    // k poslednímu ostrovu. Stejná logika trasy jako u megalodona. Bezpečné volat opakovaně.
    private static PirateShip ghost; // živý Holanďan (nejvýš jeden)
    public static void CheckGhost(GridManager grid)
    {
        if (ghost != null) return; // už pronásleduje

        var d = grid.gameData;
        if (d.storyStep != 2 || d.megaIndex != 2 || d.ambush2Done || !d.storyIslandActive) return;

        Vector2 start  = d.routeStartSet ? new Vector2(d.routeStartX, d.routeStartY) : Vector2.zero;
        Vector2 target = new Vector2(d.storyIslandX, d.storyIslandY);

        PlayerController player = NearestSailingPlayerNearRoute(start, target);
        if (player == null) return;

        Vector3? spawn = FindWaterSpawn(grid, player);
        if (spawn == null) return;

        ghost = PirateShip.SpawnGhost(spawn.Value, 2); // velká loď
        ghost.SetHunter();
        if (CombatDirector.Instance != null)
        {
            CombatDirector.Instance.RegisterGuardShip(ghost); // boss bar
            CombatDirector.Instance.Toast(Loc.T("Z mlhy se vynořuje Bludný Holanďan…", "The Flying Dutchman looms out of the mist…"));
        }
    }

    // Nejbližší plující hráč, který je do ROUTE_RANGE od trasy a už od jejího začátku odplul.
    private static PlayerController NearestSailingPlayerNearRoute(Vector2 start, Vector2 target)
    {
        foreach (var pc in PlayerController.All)
        {
            if (!pc.IsSailing) continue;
            Vector2 pos = new Vector2(pc.transform.position.x, pc.transform.position.z);
            if (Vector2.Distance(pos, start) < MIN_FROM_START) continue; // ještě u ostrova
            if (DistToSegment(pos, start, target) <= ROUTE_RANGE) return pc;
        }
        return null;
    }

    // Místo ve vodě SPAWN_DISTANCE od hráče, nejlíp před ním (ve směru plavby); když tam
    // je souš, zkusí se další směry kolem dokola.
    private static Vector3? FindWaterSpawn(GridManager grid, PlayerController player)
    {
        Vector3 forward = player.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        // Střídavě vlevo/vpravo od směru plavby: 0, +30, -30, +60, -60, …
        for (int i = 0; i < 12; i++)
        {
            float angle = ((i + 1) / 2) * 30f * (i % 2 == 0 ? 1f : -1f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 p   = player.transform.position + dir * SPAWN_DISTANCE;

            TileType t = grid.GetTileType(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
            if (t == TileType.Empty || t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure)
                return new Vector3(p.x, 0f, p.z);
        }
        return null;
    }

    // Vzdálenost bodu p od úsečky a-b.
    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
