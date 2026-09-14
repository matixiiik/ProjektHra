using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryEvents.cs
//  Centralizuje kontroly "má se tu teď stát něco z příběhu", volané z
//  PlayerController.OnEnteredTile (při každém přechodu na nové políčko).
//  Zatím jen mořská obluda mezi ostrovem 1 a 2 (Krok 5, viz story-plan.md §4).
// ─────────────────────────────────────────────────────────────────────────────

public static class StoryEvents
{
    private const float ROUTE_RANGE   = 30f; // jak blízko trasy start→cíl obluda čeká
    private const float SPAWN_DISTANCE = 50f; // jak daleko od hráče se obluda vynoří

    /// <summary>Vyvolá mořskou obludu, když hráč pluje po trase k dalšímu mega
    /// ostrovu (mezi ostrovem 1 a 2) a ještě obludu neporazil. Bezpečné volat
    /// opakovaně — sama si pohlídá, že existuje nejvýš jedna.</summary>
    public static void CheckMonster(GridManager grid)
    {
        if (SeaMonster.Instance != null) return; // už existuje

        var d = grid.gameData;
        if (d.storyStep != 2 || d.megaIndex < 1 || d.ambush1Done || !d.storyIslandActive) return;

        PlayerController player = NearestSailingPlayerNearRoute(d);
        if (player == null) return;

        // Vynoří se daleko před hráčem, ve směru jeho plavby (ať má čas si jí všimnout).
        Vector3 ahead = player.transform.position + player.transform.forward * SPAWN_DISTANCE;
        SeaMonster.Spawn(new Vector3(ahead.x, 0f, ahead.z));
    }

    // Nejbližší plující hráč, který je do ROUTE_RANGE od úsečky start (0,0) → cílový mega ostrov.
    private static PlayerController NearestSailingPlayerNearRoute(GameData d)
    {
        Vector2 start  = Vector2.zero;
        Vector2 target = new Vector2(d.storyIslandX, d.storyIslandY);

        foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!pc.IsSailing) continue;
            Vector2 pos = new Vector2(pc.transform.position.x, pc.transform.position.z);
            if (DistToSegment(pos, start, target) <= ROUTE_RANGE) return pc;
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
