using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CannonBall.cs
//  Dělová koule — jednoduchý projektil BEZ fyziky (jako zbytek hry: jen pohyb
//  a kontrola vzdálenosti k cílům). Letí rovně dopředu, po chvíli zmizí.
//
//   • Side.Player  — vystřelil hráč: zasahuje piráty a děla nepřátelských ostrovů
//   • Side.Enemy   — vystřelil pirát / ostrovní dělo: zasahuje loď hráče
//
//  Vytváří se přes CannonBall.Fire(...). Model = malá tmavá koule z primitivu.
// ─────────────────────────────────────────────────────────────────────────────

public class CannonBall : MonoBehaviour
{
    public enum Side { Player, Enemy }

    private const float SPEED       = 15f;
    private const float LIFETIME    = 2.4f;
    private const float HIT_RADIUS  = 1.0f;
    private const float FLIGHT_Y    = 0.1f; // těsně nad hladinou
    private const float GRAVITY     = 15f;  // jen pro koule s náměrem (launchAngleDeg > 0)

    private Side    side;
    private float   damage;
    private Vector3 vel;      // vodorovná složka rychlosti
    private float   velY;     // svislá složka (jen když se střílí s náměrem)
    private float   life;

    /// <summary>Vystřelí dělovou kouli z bodu "from" ve směru "dir" (vodorovně).
    /// Nepovinný `launchAngleDeg` (0 = rovná dráha jako dřív) jí dá reálný
    /// oblouk — viz PlayerController.TryShoot/TryShootOnFoot, kde náměr řídí
    /// sklon kamery (přehled/oblouk podle toho, jak moc hráč kouká dolů).</summary>
    public static void Fire(Vector3 from, Vector3 dir, float damage, Side side, float launchAngleDeg = 0f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CannonBall";
        go.transform.localScale = Vector3.one * 0.32f;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh != null)
            {
                var m = new Material(sh);
                Color c = side == Side.Player ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.25f, 0.1f, 0.08f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
                mr.sharedMaterial = m;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        go.transform.position = new Vector3(from.x, FLIGHT_Y, from.z) + dir * 0.9f;

        float angleRad = launchAngleDeg * Mathf.Deg2Rad;
        var cb = go.AddComponent<CannonBall>();
        cb.side   = side;
        cb.damage = damage;
        cb.vel    = dir * SPEED * Mathf.Cos(angleRad);
        cb.velY   = SPEED * Mathf.Sin(angleRad);
    }

    void Update()
    {
        transform.position += vel * Time.deltaTime;

        // Svislý pohyb — bez náměru (velY==0) se drží přesně FLIGHT_Y jako dřív;
        // s náměrem letí obloukem (gravitace) a nikdy neklesne pod hladinu.
        velY -= GRAVITY * Time.deltaTime;
        float newY = transform.position.y + velY * Time.deltaTime;
        if (newY < FLIGHT_Y) { newY = FLIGHT_Y; velY = 0f; }
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        life += Time.deltaTime;
        if (life >= LIFETIME) { Destroy(gameObject); return; }

        if (side == Side.Player) CheckEnemyHits();
        else                     CheckPlayerHits();
    }

    // Hráčova koule → piráti a děla nepřátelských ostrovů.
    void CheckEnemyHits()
    {
        var dir = CombatDirector.Instance;
        if (dir == null) return;

        PirateShip pirate = dir.PirateNear(transform.position, HIT_RADIUS);
        if (pirate != null) { pirate.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

        HostileIslandCannon cannon = dir.CannonNear(transform.position, HIT_RADIUS);
        if (cannon != null) { cannon.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

        SeaMonster monster = dir.MonsterNear(transform.position, HIT_RADIUS);
        if (monster != null) { monster.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

        // Stacionární strážce mega ostrova (viz PlayerController.TryShootOnFoot —
        // hráčova pěší zbraň teď na ně umí střílet, ne jen E na blízko).
        foreach (var guard in LandGuard.All)
        {
            if (guard == null) continue;
            if ((guard.transform.position - transform.position).sqrMagnitude > HIT_RADIUS * HIT_RADIUS) continue;
            guard.TakeHit(damage);
            SoundManager.PlayHit();
            Destroy(gameObject);
            return;
        }
    }

    // Nepřátelská koule → hráč, který pluje, plave (rozbitá loď), NEBO je pěšky
    // (strážci mega ostrovů střílí i na pěší — viz LandGuard.cs). Pěšky jde
    // poškození přímo do panáčka, jinak DamageBoat sám rozhodne loď/panáček.
    void CheckPlayerHits()
    {
        foreach (var pc in PlayerController.All)
        {
            if (!pc.IsSailing && !pc.IsSwimming && !pc.IsOnFoot) continue;
            if ((pc.transform.position - transform.position).sqrMagnitude <= HIT_RADIUS * HIT_RADIUS)
            {
                if (pc.IsOnFoot) pc.DamagePlayer(Mathf.RoundToInt(damage));
                else              pc.DamageBoat(Mathf.RoundToInt(damage));
                SoundManager.PlayHit();
                Destroy(gameObject);
                return;
            }
        }
    }
}
