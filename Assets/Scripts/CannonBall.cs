using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CannonBall.cs
//  Dělová koule / náboj — jednoduchý projektil BEZ fyziky (jako zbytek hry: jen
//  pohyb a kontrola vzdálenosti k cílům). Po chvíli zmizí.
//
//   • Side.Player  — vystřelil hráč: zasahuje piráty, děla, strážce a příšeru
//   • Side.Enemy   — vystřelil pirát / ostrovní dělo / strážce: zasahuje hráče
//
//  Dva způsoby výstřelu:
//   • Fire(...)       — nepřátelé: rovně těsně nad hladinou (jako dřív)
//   • FireAimed(...)  — hráč: PŘÍMÁ dráha s náměrem nahoru/dolů podle kamery
//                       (viz PlayerController.AimElevationDeg). Vzhůru se dá trefit
//                       dělo na věži, příliš dolů koule spadne do vody.
//
//  Žádná střela neprojde majákem (a hradbami mega ostrova — MegaIslandMarker.BlocksShot).
// ─────────────────────────────────────────────────────────────────────────────

public class CannonBall : MonoBehaviour
{
    public enum Side { Player, Enemy }

    private const float SPEED       = 15f;
    private const float LIFETIME    = 2.4f;
    private const float HIT_RADIUS  = 1.0f;
    private const float HIT_HEIGHT  = 1.9f;   // jak moc smí být cíl výš/níž než střela (jen u hráčových střel)
    private const float FLIGHT_Y    = 0.1f;   // těsně nad hladinou (nepřátelské střely)
    private const float WATER_Y     = 0.05f;  // pod touhle výškou přímá střela spadla do vody
    private const float BUILDING_H  = 6f;     // do téhle výšky blokuje maják / hradby
    private const float ARM_TIME    = 0.12f;  // první chvíli střela stavby ignoruje (vylétá zpoza zdi / z věže)

    private Side    side;
    private float   damage;
    private Vector3 vel;       // vodorovná složka rychlosti
    private float   velY;      // svislá složka (u přímé střely = náměr)
    private bool    straight;  // true = přímá dráha bez gravitace (hráč)
    private float   life;

    // Sdílené materiály (jedna instance na stranu, ne nový materiál na každou střelu).
    private static Material playerMat, enemyMat;
    private static GridManager gridRef; // pro kontrolu majáků

    /// <summary>Nepřátelská střela: letí rovně těsně nad hladinou (bez náměru).</summary>
    public static void Fire(Vector3 from, Vector3 dir, float damage, Side side)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        var cb = Create(new Vector3(from.x, FLIGHT_Y, from.z) + dir * 0.9f, damage, side);
        cb.vel = dir * SPEED;
    }

    /// <summary>Hráčova střela: přímá dráha z výšky ústí hlavně, `elevationDeg` = náměr
    /// (kladný nahoru, záporný dolů). Vodorovný směr je `dir`.</summary>
    public static void FireAimed(Vector3 from, Vector3 dir, float elevationDeg, float damage, Side side)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        var cb = Create(from + dir * 0.9f, damage, side);
        float e = elevationDeg * Mathf.Deg2Rad;
        cb.vel      = dir * SPEED * Mathf.Cos(e);
        cb.velY     = SPEED * Mathf.Sin(e);
        cb.straight = true;
    }

    // Vytvoří objekt střely (koule) na dané pozici. Rychlost nastaví volající.
    private static CannonBall Create(Vector3 pos, float damage, Side side)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CannonBall";
        go.transform.localScale = Vector3.one * 0.32f;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Material m = GetMaterial(side);
            if (m != null) mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        go.transform.position = pos;

        var cb = go.AddComponent<CannonBall>();
        cb.side   = side;
        cb.damage = damage;
        return cb;
    }

    // Jeden sdílený materiál na stranu (dřív vznikal nový materiál pro každou střelu).
    private static Material GetMaterial(Side side)
    {
        Material existing = side == Side.Player ? playerMat : enemyMat;
        if (existing != null) return existing;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (sh == null) return null;

        var m = new Material(sh);
        Color c = side == Side.Player ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.25f, 0.1f, 0.08f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);

        if (side == Side.Player) playerMat = m; else enemyMat = m;
        return m;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos += vel * Time.deltaTime;

        if (straight)
        {
            // Přímá dráha: jen se posouvá i svisle; pod hladinou střela zaniká.
            pos.y += velY * Time.deltaTime;
            if (pos.y < WATER_Y) { Destroy(gameObject); return; }
        }
        else
        {
            // Nepřátelská střela drží výšku FLIGHT_Y (velY je 0).
            pos.y = FLIGHT_Y;
        }
        transform.position = pos;

        life += Time.deltaTime;
        if (life >= LIFETIME) { Destroy(gameObject); return; }

        // Stavby (maják, hradby) střelu zastaví — pro hráče i nepřátele.
        if (life >= ARM_TIME && HitsStructure()) { Destroy(gameObject); return; }

        if (side == Side.Player) CheckEnemyHits();
        else                     CheckPlayerHits();
    }

    // Narazila střela do majáku nebo hradby (ve výšce budovy)?
    bool HitsStructure()
    {
        Vector3 p = transform.position;
        if (p.y > BUILDING_H) return false;

        if (gridRef == null) gridRef = FindFirstObjectByType<GridManager>();
        if (gridRef != null &&
            gridRef.GetTileType(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == TileType.Lighthouse)
            return true;

        // Hradby a věže Pirátského ostrova (střela letící dost vysoko je přeletí).
        return MegaIslandMarker.BlocksShot(p);
    }

    // Je cíl ve výšce, kam střela doletí? (Jen přímé hráčovy střely — nepřátelské letí nízko.)
    bool HeightOk(Component target)
    {
        if (!straight || target == null) return true;
        return Mathf.Abs(target.transform.position.y - transform.position.y) <= HIT_HEIGHT;
    }

    // Hráčova koule → piráti, děla nepřátelských ostrovů, příšera a strážci.
    void CheckEnemyHits()
    {
        var dir = CombatDirector.Instance;
        if (dir == null) return;

        PirateShip pirate = dir.PirateNear(transform.position, HIT_RADIUS);
        if (pirate != null && HeightOk(pirate)) { pirate.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

        HostileIslandCannon cannon = dir.CannonNear(transform.position, HIT_RADIUS);
        if (cannon != null && HeightOk(cannon)) { cannon.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

        SeaMonster monster = dir.MonsterNear(transform.position, HIT_RADIUS);
        if (monster != null && HeightOk(monster)) { monster.TakeHit(damage); SoundManager.PlayHit(); Destroy(gameObject); return; }

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
