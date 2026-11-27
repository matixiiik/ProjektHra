using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  AimAssist.cs
//  Pomocník míření pro hráče (lodní dělo i pěší zbraň). Ve třetí osobě se už
//  NEmíří sklonem kamery (to posílalo koule do vody nebo do nebe, podle toho,
//  jak hráč zrovna natočil kameru). Místo toho:
//    • vodorovný směr určuje hráč (příď lodi / pohled kamery u pěší zbraně),
//    • výšku dorovná AimAssist: když je v úzkém kuželu před hráčem cíl (pirát,
//      dělo na věži, věž pevnosti, stráž, obluda), koule letí přesně na něj;
//      jinak letí vodorovně (rovně dopředu).
//  V první osobě zůstává ruční míření kamerou (PlayerController.AimElevationDeg).
//
//  Když je cíl v kuželu, střela se k němu natočí i do strany (o pár stupňů) — u plující
//  lodi s předsazením, kam dopluje, než koule doletí. Mimo kužel se nic nedorovnává.
//
//  Statická třída bez stavu — jen projde existující seznamy nepřátel.
// ─────────────────────────────────────────────────────────────────────────────

public static class AimAssist
{
    public const float RANGE     = 32f;  // koule letí 15 j/s × 2,4 s ≈ 36 j — kousek rezervy
    public const float CONE_DEG  = 14f;  // jak daleko od směru střelby se cíl ještě "chytí"
    private const float MIN_DIST = 1.2f; // cíl přímo u hlavně neber (stejně by se netrefil)
    private const float MAX_ELEV = 40f;  // nejstrmější automatický náměr
    private const float BALL_SPEED = 15f; // rychlost koule (CannonBall.SPEED) — pro předsazení pohyblivého cíle

    /// <summary>
    /// Najde cíl před hráčem: z bodu `from` ve vodorovném směru `dirH`. Vrací true a bod,
    /// kam mířit (střed cíle). Vybírá cíl nejblíž ose střelby (nejmenší úhel).
    /// </summary>
    public static bool FindTarget(Vector3 from, Vector3 dirH, out Vector3 point)
    {
        point = Vector3.zero;
        dirH.y = 0f;
        if (dirH.sqrMagnitude < 0.0001f) return false;
        dirH.Normalize();

        float bestAngle = CONE_DEG;
        bool  found     = false;

        // Pirátská loď sedí kousek pod hladinou (SHIP_Y) — míří se nad ni, ať koule nespadne do vody.
        // Pluje-li, míří se tam, kde bude, až koule doletí (předsazení = rychlost × doba letu).
        foreach (var p in PirateShip.All)
        {
            if (p == null) continue;
            Vector3 pos = p.transform.position + Vector3.up * 0.75f;
            float flightTime = Vector3.Distance(from, pos) / BALL_SPEED;
            Vector3 lead = p.Velocity; lead.y = 0f;
            Consider(from, dirH, pos + lead * flightTime, ref bestAngle, ref point, ref found);
        }

        var cd = CombatDirector.Instance;
        if (cd != null)
            foreach (var c in cd.Cannons)
                if (c != null) Consider(from, dirH, c.transform.position + Vector3.up * 0.3f, ref bestAngle, ref point, ref found);

        foreach (var g in LandGuard.All)
            if (g != null) Consider(from, dirH, g.transform.position + Vector3.up * 0.6f, ref bestAngle, ref point, ref found);

        // Věže pevnosti — jen ty bez děla (na věž s dělem se míří přímo na dělo, viz výš).
        foreach (var t in FortressTower.All)
            if (t != null && t.Standing && !t.HasLiveCannon) Consider(from, dirH, t.AimPoint, ref bestAngle, ref point, ref found);

        if (SeaMonster.Instance != null)
        {
            Vector3 m = SeaMonster.Instance.transform.position;
            Consider(from, dirH, new Vector3(m.x, 0.2f, m.z), ref bestAngle, ref point, ref found); // obluda je u hladiny
        }
        return found;
    }

    // Jeden kandidát na cíl: když je v dostřelu a blíž ose střelby než dosavadní nejlepší,
    // stane se novým nejlepším (bestAngle / point / found se přepíšou).
    private static void Consider(Vector3 from, Vector3 dirH, Vector3 p, ref float bestAngle, ref Vector3 point, ref bool found)
    {
        Vector3 to = p - from; to.y = 0f;
        float dist = to.magnitude;
        if (dist < MIN_DIST || dist > RANGE) return;
        float angle = Vector3.Angle(dirH, to);
        if (angle >= bestAngle) return;
        bestAngle = angle;
        point     = p;
        found     = true;
    }

    /// <summary>Náměr ve stupních (kladný = nahoru), aby přímá střela z `from` letěla do `point`.</summary>
    public static float ElevationTo(Vector3 from, Vector3 point)
    {
        Vector3 to = point - from;
        float flat = new Vector2(to.x, to.z).magnitude;
        if (flat < 0.01f) return 0f;
        return Mathf.Clamp(Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg, -MAX_ELEV, MAX_ELEV);
    }
}
