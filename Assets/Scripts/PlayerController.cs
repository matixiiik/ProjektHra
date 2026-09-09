using UnityEngine;
using System.Collections;

// ─────────────────────────────────────────────────────────────────────────────
//  PlayerController.cs
//  Ovládání hráče: pohyb po mřížce (políčko po políčku), přesedání loď ↔ pěšky,
//  rybaření, těžba pokladů a otevírání obchodů.
//
//  Jeden a ten samý skript ovládá oba hráče. Rozlišuje je playerIndex:
//     0 = Hráč 1 (WASD, Space, E)
//     1 = Hráč 2 (šipky, Numpad0, Numpad1)
//  Podle playerIndex se čte a zapisuje buď do polí P1, nebo P2 v GameData —
//  o to se starají "routovací" property (GridX, PCoins, PQuest, ...).
// ─────────────────────────────────────────────────────────────────────────────

public class PlayerController : MonoBehaviour
{
    [HideInInspector] public int playerIndex = 0; // 0 = P1, 1 = P2 (nastavuje MultiplayerManager)

    private GridManager        gridManager;
    private UpgradeShopManager upgradeShopManager;
    private QuestShopManager   questShopManager;
    private StoryNpc           storyNpc;      // příběhové NPC (děda) na startovním ostrově, může být null
    private ShipModelSwitcher  shipSwitcher;  // přepíná / posazuje 3D model lodě

    public GameObject headDot;          // tečka nad hlavou, když je hráč pěšky
    public Transform  boatModel;        // 3D model lodě (přepíná ShipModelSwitcher)

    // Když hráč vystoupí na ostrov, loď nezmizí — nechá se plavat na svém místě
    // jako tenhle samostatný objekt (kopie modelu lodě). Zase se zničí při nasednutí.
    private GameObject parkedBoatGO;
    private const int  REPAIR_COST_PER_HP = EconomyConfig.RepairCostPerHp; // mince za opravu 1 bodu zdraví lodě

    // Kamera, podle které se tenhle hráč hýbe (W = "kam kouká kamera").
    // P1 = hlavní kamera (necháme null → Camera.main), P2 ji dostane od MultiplayerManageru.
    [HideInInspector] public Transform viewCamera;
    public float moveSpeed       = 5f;  // rychlost jízdy (jednotky/s) — volný pohyb podle kamery
    public float turnSpeed       = 6f;  // jak rychle se loď/postavička natáčí do směru jízdy
    public float fishingDuration = 1.5f;// jak dlouho trvá jeden zátah
    public float miningDuration  = 3.0f;// jak dlouho trvá vytěžit poklad

    private bool isMoving = false;  // právě se přesouvá mezi políčky
    private bool isWorking = false; // právě rybaří / těží

    public bool  IsWorking    => isWorking;
    public float WorkProgress { get; private set; } // 0..1, pro kroužek WorkIndicator

    // Poslední políčko, kde se odkrývala mlha (aby se to nedělalo pořád dokola).
    private int lastExploredX = -999;
    private int lastExploredY = -999;

    private bool isOnFoot = false; // true = hráč vystoupil a chodí po ostrově
    private int  boatGridX;        // kde má zaparkovanou loď
    private int  boatGridY;

    public bool IsOnFoot => isOnFoot;

    // ── Routování pozice do správných polí GameData (P1 vs P2) ───────────────
    int GridX
    {
        get => playerIndex == 0 ? gridManager.gameData.playerGridX : gridManager.gameData.player2GridX;
        set { if (playerIndex == 0) gridManager.gameData.playerGridX = value; else gridManager.gameData.player2GridX = value; }
    }
    int GridY
    {
        get => playerIndex == 0 ? gridManager.gameData.playerGridY : gridManager.gameData.player2GridY;
        set { if (playerIndex == 0) gridManager.gameData.playerGridY = value; else gridManager.gameData.player2GridY = value; }
    }

    // ── Routování ekonomiky a upgradů (P1 vs P2) ────────────────────────────
    int PCoins
    {
        get => playerIndex == 0 ? gridManager.gameData.coins : gridManager.gameData.player2Coins;
        set { if (playerIndex == 0) gridManager.gameData.coins = value; else gridManager.gameData.player2Coins = value; }
    }
    int PFishCount
    {
        get => playerIndex == 0 ? gridManager.gameData.fishCount : gridManager.gameData.player2FishCount;
        set { if (playerIndex == 0) gridManager.gameData.fishCount = value; else gridManager.gameData.player2FishCount = value; }
    }
    int PTreasureCount
    {
        get => playerIndex == 0 ? gridManager.gameData.treasureCount : gridManager.gameData.player2TreasureCount;
        set { if (playerIndex == 0) gridManager.gameData.treasureCount = value; else gridManager.gameData.player2TreasureCount = value; }
    }
    bool PHasRodUpgrade    => playerIndex == 0 ? gridManager.gameData.hasRodUpgrade    : gridManager.gameData.player2HasRodUpgrade;
    bool PHasMiningUpgrade => playerIndex == 0 ? gridManager.gameData.hasMiningUpgrade : gridManager.gameData.player2HasMiningUpgrade;
    bool PHasSpeedUpgrade  => playerIndex == 0 ? gridManager.gameData.hasSpeedUpgrade  : gridManager.gameData.player2HasSpeedUpgrade;
    int  PShipLevel        => playerIndex == 0 ? gridManager.gameData.shipLevel        : gridManager.gameData.player2ShipLevel;

    int PBoatHealth
    {
        get => playerIndex == 0 ? gridManager.gameData.boatHealth : gridManager.gameData.player2BoatHealth;
        set { int v = Mathf.Clamp(value, 0, BoatStats.MaxHealth);
              if (playerIndex == 0) gridManager.gameData.boatHealth = v; else gridManager.gameData.player2BoatHealth = v; }
    }
    int PPlayerHealth
    {
        get => playerIndex == 0 ? gridManager.gameData.playerHealth : gridManager.gameData.player2PlayerHealth;
        set { int v = Mathf.Clamp(value, 0, BoatStats.MaxHealth);
              if (playerIndex == 0) gridManager.gameData.playerHealth = v; else gridManager.gameData.player2PlayerHealth = v; }
    }
    int PAmmo
    {
        get => playerIndex == 0 ? gridManager.gameData.ammo : gridManager.gameData.player2Ammo;
        set { int v = Mathf.Max(0, value);
              if (playerIndex == 0) gridManager.gameData.ammo = v; else gridManager.gameData.player2Ammo = v; }
    }
    bool PBoatWrecked
    {
        get => playerIndex == 0 ? gridManager.gameData.boatWrecked : gridManager.gameData.player2BoatWrecked;
        set { if (playerIndex == 0) gridManager.gameData.boatWrecked = value; else gridManager.gameData.player2BoatWrecked = value; }
    }
    bool PBoatNeedsRehome
    {
        get => playerIndex == 0 ? gridManager.gameData.boatNeedsRehome : gridManager.gameData.player2BoatNeedsRehome;
        set { if (playerIndex == 0) gridManager.gameData.boatNeedsRehome = value; else gridManager.gameData.player2BoatNeedsRehome = value; }
    }
    bool PHasMap => playerIndex == 0 ? gridManager.gameData.hasMap : gridManager.gameData.player2HasMap;

    /// <summary>Je hráč zrovna v lodi na vodě? (pro soubojový systém)</summary>
    public bool IsSailing  => !isOnFoot && !PBoatWrecked && enabled && gameObject.activeInHierarchy;

    /// <summary>Plave hráč ve vodě (rozbitá loď)? Pořád je terč pro děla.</summary>
    public bool IsSwimming => !isOnFoot && PBoatWrecked && enabled && gameObject.activeInHierarchy;

    /// <summary>Kam je natočený model hráče (loď / panáček) ve stupních (0 = sever).
    /// Používá minimapa pro šipku, podle které se dá řídit.</summary>
    public float HeadingDegrees
    {
        get
        {
            Transform m = (isOnFoot || IsSwimming) ? (headDot != null ? headDot.transform : transform)
                                                   : (boatModel != null ? boatModel : transform);
            return m.eulerAngles.y;
        }
    }

    private float nextShotTime;
    private const float SHOOT_COOLDOWN = 0.55f;
    private float damageGraceUntil; // krátká nezranitelnost po "potopení" lodě
    ActiveQuest PQuest     => playerIndex == 0 ? gridManager.gameData.activeQuest      : gridManager.gameData.player2ActiveQuest;

    // ── Pomocníci na klávesy (P1 dostane k1, P2 dostane k2) ─────────────────
    bool P1 => playerIndex == 0;
    bool Key    (KeyCode k1, KeyCode k2) => P1 ? Input.GetKey(k1)     : Input.GetKey(k2);
    bool KeyDown(KeyCode k1, KeyCode k2) => P1 ? Input.GetKeyDown(k1) : Input.GetKeyDown(k2);

    // ───────────────────────────────────────────────────────────────────────

    void Start()
    {
        gridManager        = FindFirstObjectByType<GridManager>();
        upgradeShopManager = FindFirstObjectByType<UpgradeShopManager>();
        questShopManager   = FindFirstObjectByType<QuestShopManager>();
        storyNpc           = FindFirstObjectByType<StoryNpc>();
        shipSwitcher       = GetComponent<ShipModelSwitcher>();

        if (playerIndex == 0)
        {
            // P1 obnoví svůj stav z uložených dat.
            isOnFoot  = gridManager.gameData.isOnFoot;
            boatGridX = gridManager.gameData.boatGridX;
            boatGridY = gridManager.gameData.boatGridY;
            transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);
        }
        else
        {
            // Hráč 2 startuje hned vedle hráče 1 a ve stejném režimu (pěšky / loď) —
            // takže při nové hře se oba probudí jako panáčci na ostrově.
            isOnFoot = gridManager.gameData.isOnFoot;

            int p1x = gridManager.gameData.playerGridX;
            int p1y = gridManager.gameData.playerGridY;

            // Najdi políčko hned vedle P1, na které P2 smí vstoupit.
            int sx = p1x, sy = p1y;
            Vector2Int[] around = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var d in around)
            {
                TileType t = gridManager.GetTileType(p1x + d.x, p1y + d.y);
                bool ok = isOnFoot
                    ? (t == TileType.Harbor || t == TileType.Pier)
                    : (t == TileType.Water || t == TileType.Water_Fish || t == TileType.Pier);
                if (ok) { sx = p1x + d.x; sy = p1y + d.y; break; }
            }

            gridManager.gameData.player2GridX = sx;
            gridManager.gameData.player2GridY = sy;

            // Loď P2 — druhé molo (piery jsou vždy dva vedle sebe), jinak stejné jako P1.
            boatGridX = gridManager.gameData.boatGridX;
            boatGridY = gridManager.gameData.boatGridY;
            foreach (var d in around)
                if (gridManager.GetTileType(boatGridX + d.x, boatGridY + d.y) == TileType.Pier)
                { boatGridX += d.x; boatGridY += d.y; break; }

            transform.position = new Vector3(sx, 0.5f, sy);
        }

        // Zobraz správně loď / panáčka.
        ShowBoatOrFoot();

        // Pěna za lodí, když pluje (vlastní objekt, jede za lodí sám).
        var wakeGo = new GameObject("BoatWake_P" + (playerIndex + 1));
        wakeGo.AddComponent<BoatWake>().Bind(this);

        ExploreCurrentPosition();
    }

    void Update()
    {
        // Loď zůstává plavat na svém místě, dokud je hráč pěšky (a zase zmizí,
        // až nasedne) — řeší i načtení save uprostřed vylodění.
        SyncParkedBoat();

        // Velká mapa — v lodi i pěšky (ne když plaveš s rozbitou lodí) a jen
        // s koupenou mapou. M (P1) / Numpad 2 (P2). Řeší otevření i zavření
        // (Toggle), proto je to nad "zámkem" ovládání níž.
        if (KeyDown(KeyCode.M, KeyCode.Keypad2) && !PBoatWrecked && PHasMap
            && !GameConsole.IsOpen && !MainMenuManager.IsVisible && !DeathScreen.IsOpen)
        {
            MapScreen.Toggle(playerIndex, gridManager);
            return;
        }

        // Když je otevřený MŮJ obchod / konzole / menu / mapa, hráč se neovládá.
        // (Ve split screenu obchod/mapa druhého hráče tohohle hráče nemrazí.)
        bool myShopOpen = (upgradeShopManager != null && upgradeShopManager.IsOpenForBuyer(playerIndex))
                       || (questShopManager   != null && questShopManager.IsOpenForBuyer(playerIndex));
        bool myTalkOpen = storyNpc != null && storyNpc.IsTalkingWith(playerIndex);
        if (isMoving || isWorking || myShopOpen || myTalkOpen || MapScreen.IsOpenFor(playerIndex)
            || GameConsole.IsOpen || MainMenuManager.IsVisible || DeathScreen.IsOpen) return;

        // E / Numpad1 → nastup/vystup z lodě, nebo vejdi do sousední budovy (maják).
        if (KeyDown(KeyCode.E, KeyCode.Keypad1))
        {
            if (!TryInteractAdjacentBuilding()) TryToggleBoatFoot();
            return;
        }

        // Volný pohyb podle kamery: dopředu = tam, kam se kamera dívá, do stran
        // podle jejího natočení. Jde jet i diagonálně (např. W+D) — žádné
        // skákání po políčkách, ale plynulá "freestyle" jízda.
        float h = 0f, v = 0f;
        if (Key(KeyCode.W, KeyCode.UpArrow))    v += 1f;
        if (Key(KeyCode.S, KeyCode.DownArrow))  v -= 1f;
        if (Key(KeyCode.D, KeyCode.RightArrow)) h += 1f;
        if (Key(KeyCode.A, KeyCode.LeftArrow))  h -= 1f;

        if (h != 0f || v != 0f) Move(h, v);

        // Space / Numpad0 → rybaření / těžba na aktuálním políčku.
        if (KeyDown(KeyCode.Space, KeyCode.Keypad0)) TryInteract();

        // R / Numpad-děleno → oprava lodě, když pěšky stojíš u svého člunu na molu.
        if (isOnFoot && CanRepairHere() && KeyDown(KeyCode.R, KeyCode.KeypadDivide)) TryRepairBoat();

        // Levé tlačítko myši (P1) / Numpad * (P2) → výstřel z lodního děla (jen z celé lodě).
        bool shoot = P1 ? Input.GetMouseButtonDown(0) : Input.GetKeyDown(KeyCode.KeypadMultiply);
        if (shoot && !isOnFoot && !PBoatWrecked) TryShoot();
    }

    // ── Střelba z lodního děla ─────────────────────────────────────────────
    void TryShoot()
    {
        if (!BoatStats.HasCannon(PShipLevel)) return; // veslice dělo nemá
        if (PAmmo <= 0) return;
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + SHOOT_COOLDOWN;

        PAmmo -= 1;

        Vector3 dir  = boatModel != null ? boatModel.forward : transform.forward;
        Vector3 from = transform.position;
        CombatDirector.Ensure();
        CannonBall.Fire(from, dir, BoatStats.CannonDamage(PShipLevel), CannonBall.Side.Player);

        SoundManager.PlayCannon();
        gridManager.NotifyWorldChanged(); // překresli munici v HUD
    }

    /// <summary>Zásah do lodě (volá soubojový systém). Když loď plave → ubírá jí
    /// zdraví (a s 25% šancí trefí i panáčka). Při 0 se loď ROZBIJE — panáček
    /// vypadne do vody a musí doplavat k ostrovu a opravit ji v obchodě.
    /// Když hráč zrovna plave (rozbitá loď), zásah jde přímo do panáčka.</summary>
    public void DamageBoat(int dmg)
    {
        if (isOnFoot || dmg <= 0 || DeathScreen.IsOpen) return;
        if (Time.time < damageGraceUntil) return;

        // Rozbitá loď → hráč plave → koule trefí přímo jeho.
        if (PBoatWrecked) { DamagePlayer(dmg); return; }

        PBoatHealth -= dmg;
        SoundManager.PlaySplash();

        // 25 % — kus střepin / vlna trefí i panáčka.
        if (Random.value < BoatStats.CannonSplashChance)
        {
            int splash = Mathf.Max(3, dmg / 2);
            PPlayerHealth -= splash;
            if (PPlayerHealth <= 0) { PPlayerHealth = 0; gridManager.Save(); DeathScreen.Show(playerIndex); return; }
        }

        gridManager.NotifyWorldChanged();

        if (PBoatHealth <= 0) WreckBoat();
    }

    // Loď se rozbije: panáček je najednou ve vodě (plave), loď zmizí.
    void WreckBoat()
    {
        PBoatWrecked     = true;
        PBoatHealth      = 0;
        isOnFoot         = false;
        if (playerIndex == 0) gridManager.gameData.isOnFoot = false;
        damageGraceUntil = Time.time + 2f; // chvilka na nadechnutí

        DespawnParkedBoat();

        // Malý odraz od nejbližšího nebezpečí (ať hráč nezačíná plavat pirátovi pod dělem).
        if (CombatDirector.Instance != null)
        {
            Vector3 away = CombatDirector.Instance.AwayFromNearestThreat(transform.position);
            Vector3 escape = transform.position + away * 3f;
            int ex = Mathf.RoundToInt(escape.x), ey = Mathf.RoundToInt(escape.z);
            if (IsBoatWater(gridManager.GetTileType(ex, ey))) TeleportTo(ex, ey);
            CombatDirector.Instance.Toast("Lod se rozbila! Doplav k ostrovu a oprav ji v obchode.");
        }

        ShowBoatOrFoot();
        SoundManager.PlaySink();
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    /// <summary>Ubere hráči (panáčkovi) zdraví přímo. Při 0 → obrazovka smrti.</summary>
    public void DamagePlayer(int dmg)
    {
        if (dmg <= 0 || DeathScreen.IsOpen) return;
        PPlayerHealth -= dmg;
        gridManager.NotifyWorldChanged();
        if (PPlayerHealth <= 0) { PPlayerHealth = 0; gridManager.Save(); DeathScreen.Show(playerIndex); }
    }

    /// <summary>Přidá hráči mince (odměna za potopení piráta / zničení děla).</summary>
    public void RewardCoins(int amount)
    {
        if (amount <= 0) return;
        PCoins += amount;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    // Loď (plovoucí kopie) má existovat právě když je hráč pěšky s celou lodí.
    void SyncParkedBoat()
    {
        if (isOnFoot && !PBoatWrecked)
        {
            // Po opravě rozbité lodě v obchodě ji přemísti k nejbližšímu molu.
            if (PBoatNeedsRehome)
            {
                RehomeBoatToNearestPier();
                PBoatNeedsRehome = false;
                gridManager.Save();
            }

            if (parkedBoatGO == null && boatModel != null
                && IsBoatWater(gridManager.GetTileType(boatGridX, boatGridY)))
                SpawnParkedBoat();
        }
        else if (parkedBoatGO != null)
        {
            DespawnParkedBoat();
        }
    }

    // Přesune (zaparkuje) loď do vodního políčka hned vedle nejbližšího mola.
    void RehomeBoatToNearestPier()
    {
        Vector2Int? pier = gridManager.NearestPierTile(GridX, GridY);
        if (pier == null) return;

        Vector2Int? water = FindAdjacentWater(pier.Value.x, pier.Value.y);
        if (water == null) return;

        boatGridX = water.Value.x;
        boatGridY = water.Value.y;
        if (playerIndex == 0)
        {
            gridManager.gameData.boatGridX = boatGridX;
            gridManager.gameData.boatGridY = boatGridY;
        }
    }

    // ── Oprava lodě v přístavu ─────────────────────────────────────────────
    // Jde, když hráč stojí pěšky na molu (nebo hned vedle) a jeho loď plave
    // vedle. Platí se mincemi (REPAIR_COST_PER_HP za bod), opraví se tolik,
    // na kolik má hráč mince.
    bool CanRepairHere()
    {
        if (!isOnFoot || parkedBoatGO == null) return false;
        if (PBoatHealth >= BoatStats.MaxHealth) return false;

        int d = Mathf.Max(Mathf.Abs(GridX - boatGridX), Mathf.Abs(GridY - boatGridY));
        bool onOrNextToPier = gridManager.GetTileType(GridX, GridY) == TileType.Pier
                           || FindAdjacent(GridX, GridY, TileType.Pier) != null;
        return d <= 1 && onOrNextToPier;
    }

    void TryRepairBoat()
    {
        int missing   = BoatStats.MaxHealth - PBoatHealth;
        int canAfford = PCoins / REPAIR_COST_PER_HP;
        int heal      = Mathf.Min(missing, canAfford);
        if (heal <= 0) return;

        PBoatHealth += heal;
        PCoins      -= heal * REPAIR_COST_PER_HP;
        SoundManager.PlayCoin();
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    // ── Pohyb ──────────────────────────────────────────────────────────────

    // Posune hráče podle vstupu (h = doleva/doprava, v = dopředu/dozadu) ve
    // směru, kam se dívá kamera — ne podle pevných světových os X/Z. Díky
    // tomu jízda kopíruje natočení kamery (otoč kameru, W jede "tam kam koukáš").
    void Move(float h, float v)
    {
        Transform cam = viewCamera != null ? viewCamera
                      : (Camera.main != null ? Camera.main.transform : transform);

        Vector3 forward = cam.forward; forward.y = 0f;
        Vector3 right   = cam.right;   right.y   = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        right   = right.sqrMagnitude   > 0.0001f ? right.normalized   : Vector3.right;

        Vector3 dir = forward * v + right * h;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // Rychlost: pěšky pořád stejně, plavání (rozbitá loď) hodně pomalu,
        // na lodi ji škáluje úroveň lodě + rychlostní upgrade (2×).
        float speed = moveSpeed;
        if (PBoatWrecked && !isOnFoot)
        {
            speed *= BoatStats.SwimSpeedMultiplier;
        }
        else if (!isOnFoot)
        {
            speed *= BoatStats.SpeedMultiplier(PShipLevel);
            if (PHasSpeedUpgrade) speed *= 2f;
        }

        TryMoveBy(dir * speed * Time.deltaTime);
        RotateTowards(dir);
    }

    // Zkusí posunout hráče o "delta". Když by narazil na nesjízdné políčko
    // (pevnina pro loď, voda pro pěšího), zkusí sklouznout jen po jedné ose,
    // aby šlo "otřít" se o pobřeží místo úplného zaseknutí — jízda tak
    // zůstane plynulá i těsně u břehu.
    void TryMoveBy(Vector3 delta)
    {
        Vector3 pos = transform.position;

        if (StepTo(new Vector3(pos.x + delta.x, pos.y, pos.z + delta.z))) return;
        if (StepTo(new Vector3(pos.x + delta.x, pos.y, pos.z)))           return;
        StepTo(new Vector3(pos.x, pos.y, pos.z + delta.z));
    }

    // Přesune hráče na "target", pokud je pod ním sjízdné políčko. Vrací, jestli se to povedlo.
    bool StepTo(Vector3 target)
    {
        int tx = Mathf.RoundToInt(target.x);
        int ty = Mathf.RoundToInt(target.z);
        if (!CanEnter(gridManager.GetTileType(tx, ty))) return false;

        transform.position = target;
        OnEnteredTile(tx, ty);
        return true;
    }

    // Na co smí hráč vstoupit?
    //  • v lodi   = jen voda (na molo/ostrov se lodí nevjede)
    //  • plave    = voda + může vylézt na molo / pevninu
    //  • pěšky    = pevnina a molo
    bool CanEnter(TileType t)
    {
        if (PBoatWrecked && !isOnFoot)
            return IsBoatWater(t) || t == TileType.Harbor || t == TileType.Pier || t == TileType.MegaIsland;
        if (!isOnFoot)
            return IsBoatWater(t);
        return t == TileType.Harbor || t == TileType.Pier || t == TileType.MegaIsland;
    }

    // Vodní políčko, na které smí loď (obyčejná voda, ryby i vrak pokladu).
    static bool IsBoatWater(TileType t)
        => t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure;

    // Zavolá se, kdykoli plynulá jízda přenese hráče na jiné políčko, než na
    // kterém byl naposled — zapíše novou pozici do GameData, přegeneruje svět
    // kolem a odkryje mlhu (stejné věci, které dřív dělal jeden krok po mřížce).
    void OnEnteredTile(int tx, int ty)
    {
        if (tx == GridX && ty == GridY) return;

        GridX = tx;
        GridY = ty;

        // Plave a doplaval k molu / pevnině → vyleze z vody.
        if (PBoatWrecked && !isOnFoot)
        {
            TileType here = gridManager.GetTileType(tx, ty);
            if (here == TileType.Pier || here == TileType.Harbor)
            {
                isOnFoot = true;
                if (playerIndex == 0) gridManager.gameData.isOnFoot = true;
                ShowBoatOrFoot();
            }
        }
        // Na lodi (ne plave) si pamatuj i pozici lodě (kde kotví).
        else if (!isOnFoot)
        {
            boatGridX = tx;
            boatGridY = ty;
            if (playerIndex == 0)
            {
                gridManager.gameData.boatGridX = boatGridX;
                gridManager.gameData.boatGridY = boatGridY;
            }
        }

        // Doplul jsi k cíli z mapy (waypoint) → zruš ho.
        ClearWaypointIfReached(tx, ty);

        // Doplul jsi k příběhovému mega ostrovu (krok 2) → posuň příběh.
        var d0 = gridManager.gameData;
        if (d0.storyStep == 2 && d0.storyIslandActive
            && Mathf.Max(Mathf.Abs(tx - d0.storyIslandX), Mathf.Abs(ty - d0.storyIslandY)) <= 16)
            StoryNpc.OnReachedStoryIsland();

        gridManager.GenerateWorld(tx, ty);
        ExploreCurrentPosition();
    }

    // Když je hráč u svého waypointu (±1 políčko), cíl se splní a zmizí.
    void ClearWaypointIfReached(int tx, int ty)
    {
        var d = gridManager.gameData;
        bool has = playerIndex == 0 ? d.hasWaypoint : d.player2HasWaypoint;
        if (!has) return;

        int wx = playerIndex == 0 ? d.waypointX : d.player2WaypointX;
        int wy = playerIndex == 0 ? d.waypointY : d.player2WaypointY;
        if (Mathf.Abs(tx - wx) > 1 || Mathf.Abs(ty - wy) > 1) return;

        if (playerIndex == 0) d.hasWaypoint = false; else d.player2HasWaypoint = false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Dorazil jsi k cíli z mapy.");
        gridManager.Save();
    }

    // Řekne světu, kde teď hráč je, a spustí krátký plynulý přesun — používá
    // se jen pro nastupování/vystupování z lodě a teleport z konzole. Běžná
    // jízda jede přímo přes Move()/TryMoveBy() výše.
    void MoveToGrid(int x, int y)
    {
        gridManager.GenerateWorld(x, y);
        StartCoroutine(SmoothMovement(x, y));
    }

    // Plynule posune objekt hráče na cílové políčko.
    IEnumerator SmoothMovement(int tx, int ty)
    {
        isMoving = true;
        Vector3 target = new Vector3(tx, transform.position.y, ty);

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null; // počkej na další snímek
        }

        transform.position = target;
        ExploreCurrentPosition();
        isMoving = false;
    }

    // Odkryje mlhu kolem aktuální pozice (poloměr 2 políčka) a přidá na velkou
    // mapu ostrovy, které jsou blíž než 50 políček.
    void ExploreCurrentPosition()
    {
        int cx = Mathf.RoundToInt(transform.position.x);
        int cy = Mathf.RoundToInt(transform.position.z);
        if (cx == lastExploredX && cy == lastExploredY) return; // beze změny

        gridManager.MarkAreaExplored(cx, cy, 2);
        gridManager.MapNearbyIslands(cx, cy, 50);
        lastExploredX = cx;
        lastExploredY = cy;
    }

    // Plynule natočí model (loď nebo pěší postavička) do směru jízdy —
    // ne skokem, ale postupně (Slerp), aby se zatáčky jely obloukem.
    void RotateTowards(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir);

        Transform model = (isOnFoot || IsSwimming) ? (headDot != null ? headDot.transform : null) : boatModel;
        if (model == null) return;

        model.rotation = Quaternion.Slerp(model.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    // ── Přesedání loď ↔ pěšky ──────────────────────────────────────────────
    //  Lodí se na molo ani na ostrov NEvjede. Musíš dojet ve vodě až k molu,
    //  vystoupit (E) → panáček přeskočí na molo a loď zůstane plavat, kde byla.
    //  Nasedání: stojíš na molu (nebo vedle) a tvoje loď plave hned vedle.
    void TryToggleBoatFoot()
    {
        int px = GridX;
        int py = GridY;

        // Rozbitá loď: E jen vyleze z vody na sousední molo / pevninu (nasednout
        // se nedá — loď se opravuje v obchodě).
        if (PBoatWrecked)
        {
            if (isOnFoot)
            {
                // Panáček je pěšky, loď je rozbitá (a schovaná) — E ji nenasedne.
                // Řekni hráči, kam si pro opravu dojít.
                if (CombatDirector.Instance != null)
                    CombatDirector.Instance.Toast("Lod je rozbita — oprav ji v obchode s vylepsenimi (v majaku).");
                return;
            }
            Vector2Int? land = FindAdjacent(px, py, TileType.Pier) ?? FindAdjacent(px, py, TileType.Harbor);
            if (land == null) return;
            isOnFoot = true;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = true;
            ShowBoatOrFoot();
            GridX = land.Value.x;
            GridY = land.Value.y;
            MoveToGrid(land.Value.x, land.Value.y);
            return;
        }

        if (!isOnFoot)
        {
            // Vystoupit: loď musí plavat ve vodě HNED VEDLE mola.
            Vector2Int? pier = FindAdjacent(px, py, TileType.Pier);
            if (pier == null) return; // není u mola — nedá se vystoupit

            // Loď zůstane plavat přesně tady.
            boatGridX = px;
            boatGridY = py;
            if (playerIndex == 0)
            {
                gridManager.gameData.boatGridX = px;
                gridManager.gameData.boatGridY = py;
            }

            isOnFoot = true;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = true;
            SpawnParkedBoat();  // necháme plavat kopii lodě na místě
            ShowBoatOrFoot();   // a schováme loď u hráče

            GridX = pier.Value.x;
            GridY = pier.Value.y;
            MoveToGrid(pier.Value.x, pier.Value.y);
        }
        else
        {
            // Nasednout: hráč stojí na molu / vedle a jeho loď plave hned vedle.
            int dist = Mathf.Max(Mathf.Abs(px - boatGridX), Mathf.Abs(py - boatGridY));
            bool boatReachable = dist <= 1 && IsBoatWater(gridManager.GetTileType(boatGridX, boatGridY));

            if (!boatReachable)
            {
                // Loď je pryč / nedosažitelná — když hráč stojí na molu (nebo vedle),
                // "připluj" s lodí na vodní políčko hned vedle toho mola.
                Vector2Int? spot = WaterNextToPierNear(px, py);
                if (spot == null) return; // fakt není kam / čím nasednout
                boatGridX = spot.Value.x;
                boatGridY = spot.Value.y;
                if (playerIndex == 0)
                {
                    gridManager.gameData.boatGridX = boatGridX;
                    gridManager.gameData.boatGridY = boatGridY;
                }
            }

            isOnFoot = false;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = false;
            DespawnParkedBoat();
            ShowBoatOrFoot();

            GridX = boatGridX;
            GridY = boatGridY;
            MoveToGrid(boatGridX, boatGridY);
        }
    }

    // Vodní políčko hned vedle mola, na kterém (nebo vedle kterého) hráč stojí.
    Vector2Int? WaterNextToPierNear(int x, int y)
    {
        Vector2Int? pier = gridManager.GetTileType(x, y) == TileType.Pier
            ? new Vector2Int(x, y)
            : FindAdjacent(x, y, TileType.Pier);
        if (pier == null) return null;

        return FindAdjacentWater(pier.Value.x, pier.Value.y);
    }

    // První sousední (4-směr) vodní políčko. Null, když žádné není.
    Vector2Int? FindAdjacentWater(int x, int y)
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
            if (IsBoatWater(gridManager.GetTileType(x + d.x, y + d.y)))
                return new Vector2Int(x + d.x, y + d.y);
        return null;
    }

    // Postaví plovoucí kopii lodě na místo, kde hráč vystoupil.
    void SpawnParkedBoat()
    {
        DespawnParkedBoat();
        if (shipSwitcher != null) shipSwitcher.Apply(); // ať má model správnou výšku
        if (boatModel == null) return;

        // Výška hladiny lodě = lokální posazení modelu + výška objektu hráče (0.5).
        float y = boatModel.localPosition.y + 0.5f;

        parkedBoatGO = Instantiate(boatModel.gameObject);
        parkedBoatGO.name = "ParkedBoat_P" + (playerIndex + 1);
        parkedBoatGO.transform.SetParent(null, true);
        parkedBoatGO.transform.position = new Vector3(boatGridX, y, boatGridY);
        parkedBoatGO.transform.rotation = boatModel.rotation;
        parkedBoatGO.SetActive(true);
    }

    void DespawnParkedBoat()
    {
        if (parkedBoatGO != null) Destroy(parkedBoatGO);
        parkedBoatGO = null;
    }

    void OnDestroy() => DespawnParkedBoat(); // úklid při konci split-screenu / scény

    // Normální lokální výška panáčka (headDot) — schová se sem při plavání.
    private Vector3 headDotHomeLocalPos;
    private bool    headDotHomeSaved;

    // O kolik posadit panáčka pod hladinu, když plave (rozbitá loď) — tak, aby
    // z vody koukala jen hlava. Počátek modelu je u nohou, tělo (kapsle) je ~1,1
    // vysoké a hlava sedí kolem lokální výšky 0.9–1.3; hladina je cca −0.22, takže
    // aby byl trup pod vodou, musí panáček dolů skoro o celou svoji výšku.
    private const float SWIM_SINK = 1.3f;

    // Zapne loď / panáčka podle stavu (loď / pěšky / plave).
    void ShowBoatOrFoot()
    {
        // ShipModelSwitcher vybere model, posadí do hladiny a schová ho, když
        // je hráč pěšky NEBO má rozbitou loď (plave).
        if (shipSwitcher != null) shipSwitcher.Apply();
        else if (boatModel != null) boatModel.gameObject.SetActive(!isOnFoot && !PBoatWrecked);

        bool swimming    = IsSwimming;
        bool showFigure  = isOnFoot || swimming;

        if (headDot != null)
        {
            headDot.SetActive(showFigure);

            if (!headDotHomeSaved) { headDotHomeLocalPos = headDot.transform.localPosition; headDotHomeSaved = true; }

            // Při plavání posad panáčka hluboko pod hladinu — kouká jen hlava,
            // ať to vypadá, že plave (ne že stojí ve vodě).
            headDot.transform.localPosition = swimming
                ? headDotHomeLocalPos + new Vector3(0f, -SWIM_SINK, 0f)
                : headDotHomeLocalPos;
        }
    }

    /// <summary>Úplně schová / zase ukáže model hráče (loď i panáčka). Používá se,
    /// když je hráč 1 v majáku — ať jeho panáček nestrašidelně nestojí na ostrově
    /// na obrazovce hráče 2.</summary>
    public void SetVisualHidden(bool hidden)
    {
        if (hidden)
        {
            if (boatModel != null) boatModel.gameObject.SetActive(false);
            if (headDot   != null) headDot.SetActive(false);
        }
        else
        {
            ShowBoatOrFoot();
        }
    }

    // Najde sousední (4-směr) políčko daného typu. Vrací null, když žádné není.
    Vector2Int? FindAdjacent(int x, int y, TileType type)
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
            if (gridManager.GetTileType(x + d.x, y + d.y) == type)
                return new Vector2Int(x + d.x, y + d.y);
        return null;
    }

    // ── Rybaření / těžba / kopání ─────────────────────────────────────────
    void TryInteract()
    {
        if (isOnFoot || PBoatWrecked) return; // pěšky ani ve vodě se nerybaří/netěží
        int cx = GridX, cy = GridY;

        // Mega quest: hráč je v lodi na místě z mapy → vykopat poklad.
        MegaQuest mq = MyMegaQuest;
        if (mq != null && mq.active && !mq.dug && cx == mq.targetX && cy == mq.targetY)
        {
            StartCoroutine(DigRoutine());
            return;
        }

        TileType type = gridManager.GetTileType(cx, cy);
        if      (type == TileType.Water_Fish) StartCoroutine(FishingRoutine(cx, cy));
        else if (type == TileType.Treasure)   StartCoroutine(MineRoutine(cx, cy));
    }

    // Můj mega quest (P1 / P2).
    private MegaQuest MyMegaQuest =>
        playerIndex == 0 ? gridManager.gameData.megaQuest : gridManager.gameData.player2MegaQuest;

    // Kopání pokladu z mapy — po chvíli nastaví dug = true (odměna se bere v QuestShopu).
    IEnumerator DigRoutine()
    {
        isWorking = true;
        WorkProgress = 0f;

        float duration = 3f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / duration;
            yield return null;
        }

        MyMegaQuest.dug = true;
        gridManager.Save();
        gridManager.NotifyWorldChanged();

        WorkProgress = 0f;
        isWorking = false;
    }

    // Interakce s budovou / bednou, u které hráč (pěšky) stojí. Vrací true, když se povedlo.
    //  • maják  → vejít dovnitř (LighthouseManager – uvnitř jsou oba obchody)
    //  • bedna  → otevřít (ChestManager – mince + mega quest)
    //  • staré UpgradeShop / QuestShop dlaždice (jen ve starých savech) → původní IMGUI okno
    bool TryInteractAdjacentBuilding()
    {
        if (!isOnFoot) return false;
        int px = GridX, py = GridY;
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
        {
            int tx = px + d.x, ty = py + d.y;
            TileType t = gridManager.GetTileType(tx, ty);

            if (storyNpc != null && storyNpc.IsAt(tx, ty))
            {
                storyNpc.StartTalk(playerIndex);
                return true;
            }
            if (t == TileType.Lighthouse && LighthouseManager.Instance != null)
            {
                // Cenová hladina obchodů = podle pozice majáku toho ostrova.
                GameSession.ShopPriceLevel = EconomyConfig.IslandPriceLevel(tx, ty);
                LighthouseManager.Instance.Enter(playerIndex);
                return true;
            }
            if (t == TileType.Chest && ChestManager.Instance != null)
            {
                return ChestManager.Instance.TryOpen(tx, ty, playerIndex);
            }
            if (t == TileType.UpgradeShop && upgradeShopManager != null)
            {
                GameSession.ShopPriceLevel = EconomyConfig.IslandPriceLevel(tx, ty);
                upgradeShopManager.Open(playerIndex); return true;
            }
            if (t == TileType.QuestShop && questShopManager != null)
            {
                GameSession.ShopPriceLevel = EconomyConfig.IslandPriceLevel(tx, ty);
                questShopManager.Open(playerIndex); return true;
            }
        }
        return false;
    }

    // Zátah: po uplynutí času přičte ryby, posune quest a případně políčko vyčerpá.
    IEnumerator FishingRoutine(int cx, int cy)
    {
        TileStatus tile = gridManager.GetTileStatus(cx, cy);
        if (tile == null) yield break;
        if (tile.fishRemaining <= 0) tile.fishRemaining = 3; // pojistka pro staré savy

        isWorking = true;
        WorkProgress = 0f;

        float elapsed = 0f;
        while (elapsed < fishingDuration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / fishingDuration;
            yield return null;
        }

        SoundManager.PlaySplash();

        // S lepším prutem hráč dostane 2 ryby, jinak 1; velká loď přidá ještě +1.
        // Z políčka ubyde 1 "hejno".
        int catchAmount = (PHasRodUpgrade ? 2 : 1) + BoatStats.FishBonus(PShipLevel);
        tile.fishRemaining -= 1;
        PFishCount += catchAmount;

        ActiveQuest q = PQuest;
        if (q.hasQuest && q.questType == 0)
            q.progress = Mathf.Min(q.progress + catchAmount, q.target);

        // Vyčerpané políčko se změní na obyčejnou vodu.
        if (tile.fishRemaining <= 0)
            gridManager.SetTileType(cx, cy, TileType.Water);

        gridManager.NotifyWorldChanged();
        WorkProgress = 0f;
        isWorking = false;
    }

    // Těžba: po uplynutí času přičte poklad, posune quest a políčko změní na vodu.
    IEnumerator MineRoutine(int x, int y)
    {
        isWorking = true;
        WorkProgress = 0f;

        // S upgradem těžby je práce 2× rychlejší; střední a větší loď navíc zrychlí o 20 %.
        float duration = PHasMiningUpgrade ? miningDuration * 0.5f : miningDuration;
        duration *= BoatStats.MiningMultiplier(PShipLevel);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / duration;
            yield return null;
        }

        PTreasureCount += 1;

        ActiveQuest q = PQuest;
        if (q.hasQuest && q.questType == 1)
            q.progress = Mathf.Min(q.progress + 1, q.target);

        gridManager.SetTileType(x, y, TileType.Water);
        WorkProgress = 0f;
        isWorking = false;
    }

    // ── Používá konzole a načítání hry ─────────────────────────────────────

    /// <summary>Přesune hráče na dané políčko a přegeneruje kolem něj svět.</summary>
    public void TeleportTo(int x, int y)
    {
        GridX = x;
        GridY = y;
        gridManager.GenerateWorld(x, y);
        transform.position = new Vector3(x, 0.5f, y);
    }

    /// <summary>Znovu načte stav hráče z GameData (po načtení slotu / nové hře).</summary>
    public void ReloadFromData()
    {
        if (playerIndex == 0)
        {
            isOnFoot  = gridManager.gameData.isOnFoot;
            boatGridX = gridManager.gameData.boatGridX;
            boatGridY = gridManager.gameData.boatGridY;
        }
        else
        {
            isOnFoot  = false;
            boatGridX = gridManager.gameData.playerGridX;
            boatGridY = gridManager.gameData.playerGridY;
        }

        isMoving = false;
        isWorking = false;
        WorkProgress = 0f;

        DespawnParkedBoat();
        ShowBoatOrFoot();
        TeleportTo(GridX, GridY);
    }

    // ── Nápověda k opravě lodě (jednoduchý text dole na své půlce) ──────────
    private GUIStyle repairStyle;

    void OnGUI()
    {
        if (!CanRepairHere()) return;

        int missing = BoatStats.MaxHealth - PBoatHealth;
        int cost    = missing * REPAIR_COST_PER_HP;
        int afford  = Mathf.Min(missing, PCoins / REPAIR_COST_PER_HP);

        string key = P1 ? "R" : "Numpad /";
        string msg = afford > 0
            ? $"[{key}]  opravit lod  ({(afford >= missing ? cost : afford * REPAIR_COST_PER_HP)} minci)"
            : "na opravu lodě nemáš dost mincí";

        if (repairStyle == null)
            repairStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f) }
            };

        float w = Screen.width, x0 = 0f;
        if (MultiplayerManager.IsMultiplayer)
        {
            w = Screen.width * 0.5f;
            x0 = playerIndex == 1 ? w : 0f;
        }
        GUI.Label(new Rect(x0, Screen.height - 132f, w, 24f), msg, repairStyle);
    }
}
