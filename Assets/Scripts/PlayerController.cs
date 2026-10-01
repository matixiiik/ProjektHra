using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    // Statický seznam všech aktivních hráčů (P1 vždy, P2 jen v multiplayeru) —
    // spousta skriptů (souboj, minimapa, moře) potřebuje každý snímek najít
    // hráče nejblíž sobě; místo opakovaného FindObjectsByType<PlayerController>
    // (scan celé scény) se udržuje tenhle seznam sám sebou přes OnEnable/OnDisable.
    private static readonly List<PlayerController> activeInstances = new List<PlayerController>();
    public static IReadOnlyList<PlayerController> All => activeInstances;

    void OnEnable()  { activeInstances.Add(this); }
    void OnDisable() { activeInstances.Remove(this); }

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
    private DamageFeedback damageFeedback; // červený záblesk / cuknutí kamery / žbluňk při zásahu
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

    // ── Routování do GameData.players[playerIndex] (P1 = 0, P2 = 1) ─────────
    // Dřív dvojice polí "xxx"/"player2Xxx" + ternary v každé property — teď
    // jeden indexer do GameData.players (viz GameData.PlayerState). Nová
    // per-hráč hodnota tak stačí přidat jen na 2 místa (PlayerState + tahle
    // property, pokud ji volající kód potřebuje), ne na 3.
    PlayerState PS => gridManager.gameData.players[playerIndex];

    int GridX
    {
        get => PS.gridX;
        set => PS.gridX = value;
    }
    int GridY
    {
        get => PS.gridY;
        set => PS.gridY = value;
    }

    // ── Routování ekonomiky a upgradů (P1 vs P2) ────────────────────────────
    int PCoins
    {
        get => PS.coins;
        set => PS.coins = value;
    }
    int PFishCount
    {
        get => PS.fishCount;
        set => PS.fishCount = value;
    }
    int PTreasureCount
    {
        get => PS.treasureCount;
        set => PS.treasureCount = value;
    }
    bool PHasRodUpgrade    => PS.hasRodUpgrade;
    bool PHasMiningUpgrade => PS.hasMiningUpgrade;
    bool PHasSpeedUpgrade  => PS.hasSpeedUpgrade;
    int  PShipLevel        => PS.shipLevel;

    int PBoatHealth
    {
        get => PS.boatHealth;
        set => PS.boatHealth = Mathf.Clamp(value, 0, BoatStats.MaxHealth);
    }
    int PPlayerHealth
    {
        get => PS.playerHealth;
        set => PS.playerHealth = Mathf.Clamp(value, 0, BoatStats.MaxHealth);
    }
    int PAmmo
    {
        get => PS.ammo;
        set => PS.ammo = Mathf.Max(0, value);
    }
    bool PBoatWrecked
    {
        get => PS.boatWrecked;
        set => PS.boatWrecked = value;
    }
    bool PBoatNeedsRehome
    {
        get => PS.boatNeedsRehome;
        set => PS.boatNeedsRehome = value;
    }
    bool PHasMap => PS.hasMap;

    // ── Zbraň pro pěší boj + hotbar (koupí se v obchodě, viz UpgradeShopManager) ──
    bool PHasHandWeapon => PS.hasHandWeapon;
    int  PHandAmmo
    {
        get => PS.handAmmo;
        set => PS.handAmmo = Mathf.Max(0, value);
    }
    // Aktivní slot hotbaru (0=zbraň, 1=munice — jen zobrazení, 2=historický poklad).
    int  PHotbarSlot
    {
        get => PS.activeHotbarSlot;
        set => PS.activeHotbarSlot = value;
    }

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
    private float nextHandShotTime;
    private const float HAND_SHOOT_COOLDOWN = 0.45f; // trochu svižnější než lodní dělo
    // Stejná síla jako úder E (LandGuard.PLAYER_HIT) — hlavní výhoda zbraně není
    // víc poškození, ale že jde střílet na dálku (E funguje jen na políčko vedle
    // strážce, co na tebe mezitím střílí ze 7 políček).
    private const float HAND_SHOT_DAMAGE    = 2f;
    private float damageGraceUntil; // krátká nezranitelnost po "potopení" lodě
    private float lastDamageTime = -999f; // kdy hráč naposledy dostal zásah (kvůli regeneraci)
    private float healAccum;             // nasbírané zlomky HP při regeneraci pěšky

    /// <summary>
    /// Má tenhle hráč otevřené nějaké "okno" (obchod / dialog / mapu / je v majáku)?
    /// V coopu se pak nezapočítá zásah (v sólu se stejně pauzuje – viz SoloPause).
    /// </summary>
    public bool ModalOpen =>
           (upgradeShopManager != null && upgradeShopManager.IsOpenForBuyer(playerIndex))
        || (questShopManager   != null && questShopManager.IsOpenForBuyer(playerIndex))
        || MapScreen.IsOpenFor(playerIndex)
        || (storyNpc != null && storyNpc.IsTalkingWith(playerIndex))
        || (RivalNpc.Instance != null && RivalNpc.Instance.IsTalkingWith(playerIndex))
        || LetterScreen.IsOpenFor(playerIndex) // čte dopis z trezoru
        || LighthouseManager.IsInside(playerIndex);
    ActiveQuest PQuest     => PS.activeQuest;

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
            isOnFoot  = PS.isOnFoot;
            boatGridX = PS.boatGridX;
            boatGridY = PS.boatGridY;
            transform.position = new Vector3(PS.gridX, 0.5f, PS.gridY);
        }
        else
        {
            // Hráč 2 startuje hned vedle hráče 1 a ve stejném režimu (pěšky / loď) —
            // takže při nové hře se oba probudí jako panáčci na ostrově. Záměrně
            // čte P1 stav (players[0]), ne vlastní — P2 se teprve umisťuje poprvé.
            var p1 = gridManager.gameData.players[0];
            isOnFoot = p1.isOnFoot;

            int p1x = p1.gridX;
            int p1y = p1.gridY;

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

            PS.gridX = sx;
            PS.gridY = sy;

            // Loď P2 — druhé molo (piery jsou vždy dva vedle sebe), jinak stejné jako P1.
            boatGridX = p1.boatGridX;
            boatGridY = p1.boatGridY;
            foreach (var d in around)
                if (gridManager.GetTileType(boatGridX + d.x, boatGridY + d.y) == TileType.Pier)
                { boatGridX += d.x; boatGridY += d.y; break; }

            transform.position = new Vector3(sx, 0.5f, sy);
        }

        // Pěší postavička = Kenney model (fallback = původní primitivní panáček).
        BuildPlayerFigure();

        // Zobraz správně loď / panáčka.
        ShowBoatOrFoot();

        // Pěna za lodí, když pluje (vlastní objekt, jede za lodí sám).
        var wakeGo = new GameObject("BoatWake_P" + (playerIndex + 1));
        wakeGo.AddComponent<BoatWake>().Bind(this);

        // Reakce na zásah (červený záblesk / cuknutí kamery / žbluňk).
        var fbGo = new GameObject("DamageFeedback_P" + (playerIndex + 1));
        damageFeedback = fbGo.AddComponent<DamageFeedback>();
        damageFeedback.Bind(this);

        ExploreCurrentPosition();
    }

    void Update()
    {
        // Pěšák ve vodě: nejdřív se brodí (chodí), a až je hluboko, plave.
        UpdateWaterState();

        // Loď zůstává plavat na svém místě, dokud je hráč pěšky (a zase zmizí,
        // až nasedne) — řeší i načtení save uprostřed vylodění.
        SyncParkedBoat();

        // Animace pěší postavičky (idle/walk/sprint podle rychlosti).
        UpdateFigureAnim();

        // Pistole v ruce se ukáže/schová podle hotbaru (levné, jen bool porovnání).
        UpdateWeaponVisual();

        // Regenerace zdraví panáčka — když je pěšky v bezpečí (na ostrově se po
        // něm nestřílí) a aspoň 6 s nedostal zásah, pomalu se léčí (2 HP/s).
        if (isOnFoot && !PBoatWrecked && PPlayerHealth < 100
            && Time.time - lastDamageTime > 6f)
        {
            healAccum += 2f * Time.deltaTime;
            if (healAccum >= 1f)
            {
                int add = Mathf.FloorToInt(healAccum);
                healAccum -= add;
                PPlayerHealth = Mathf.Min(100, PPlayerHealth + add);
                gridManager.NotifyWorldChanged();
            }
        }

        // Velká mapa — v lodi i pěšky (ne když plaveš s rozbitou lodí) a jen
        // s koupenou mapou. M (P1) / Numpad 2 (P2). Řeší otevření i zavření
        // (Toggle), proto je to nad "zámkem" ovládání níž.
        if (KeyDown(KeyCode.M, KeyCode.Keypad2) && !PBoatWrecked && PHasMap
            && !GameConsole.IsOpen && !MainMenuManager.IsVisible && !DeathScreen.IsOpenFor(playerIndex))
        {
            MapScreen.Toggle(playerIndex, gridManager);
            return;
        }

        // Když je otevřený MŮJ obchod / konzole / menu / mapa, hráč se neovládá.
        // (Ve split screenu obchod/mapa druhého hráče tohohle hráče nemrazí.)
        bool myShopOpen = (upgradeShopManager != null && upgradeShopManager.IsOpenForBuyer(playerIndex))
                       || (questShopManager   != null && questShopManager.IsOpenForBuyer(playerIndex));
        bool myTalkOpen  = (storyNpc != null && storyNpc.IsTalkingWith(playerIndex))
                        || (RivalNpc.Instance != null && RivalNpc.Instance.IsTalkingWith(playerIndex));
        bool myVaultOpen = VaultMechanism.IsOpenFor(playerIndex); // puzzle na trezoru mega ostrova (Krok 3)
        if (isMoving || isWorking || myShopOpen || myTalkOpen || myVaultOpen || LetterScreen.IsOpenFor(playerIndex) || MapScreen.IsOpenFor(playerIndex)
            || GameConsole.IsOpen || MainMenuManager.IsVisible || DeathScreen.IsOpenFor(playerIndex)) return;

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

        // Hotbar (1/2/3 u P1, Numpad 7/8/9 u P2 — horní řada numpadu) — vybere,
        // CO je zrovna "v ruce" (0=zbraň, 1=munice, 2=historický poklad);
        // reálný účinek má jen slot 0 (zbraň, viz TryShootOnFoot). Stisk STEJNÉ
        // klávesy podruhé věc zase "položí" (-1 = nic v ruce).
        // V lodi (nebo při plavání) nemá hráč nic v ruce — slot se vynuluje a klávesy nic nedělají.
        if (!isOnFoot)
        {
            if (PHotbarSlot != -1) PHotbarSlot = -1;
        }
        else if (KeyDown(KeyCode.Alpha1, KeyCode.Keypad7)) PHotbarSlot = PHotbarSlot == 0 ? -1 : 0;
        else if (KeyDown(KeyCode.Alpha2, KeyCode.Keypad8)) PHotbarSlot = PHotbarSlot == 1 ? -1 : 1;
        else if (KeyDown(KeyCode.Alpha3, KeyCode.Keypad9)) PHotbarSlot = PHotbarSlot == 2 ? -1 : 2;

        // S vytaženou zbraní se panáček otáčí za kamerou (míří před sebe).
        UpdateAimFacing();

        // Levé tlačítko myši (P1) / Numpad * (P2) → výstřel. Na lodi z děla,
        // pěšky z koupené zbraně (jen když je vybraná v hotbaru — slot 0).
        bool shoot = P1 ? Input.GetMouseButtonDown(0) : Input.GetKeyDown(KeyCode.KeypadMultiply);
        if (shoot)
        {
            if (!isOnFoot && !PBoatWrecked) TryShoot();
            else if (isOnFoot) TryShootOnFoot();
        }
    }

    // ── Míření ─────────────────────────────────────────────────────────────
    // Kamera tohoto hráče: P1 = hlavní kamera, P2 má svou (viewCamera).
    // (Pozor: viewCamera je u P1 null — proto se dřív náměr u P1 nikdy neprojevil.)
    private Transform AimCamera
        => viewCamera != null ? viewCamera : (Camera.main != null ? Camera.main.transform : null);

    private const float AIM_NEUTRAL_PITCH = 52f; // sklon kamery, při kterém se střílí vodorovně (výchozí pohled)
    private const float MAX_AIM_ANGLE     = 30f; // nejvyšší náměr nahoru / dolů ve třetí osobě
    private const float FP_MAX_AIM_ANGLE  = 60f; // v první osobě jde mířit strměji

    // Náměr výstřelu ve stupních (kladný = nahoru, záporný = dolů) podle sklonu kamery.
    //  • první osoba: přímo tam, kam se díváš
    //  • třetí osoba: výchozí pohled (52°) = rovně; kamera výš (menší sklon) = nahoru,
    //    kamera kolmo dolů = dolů. Nahoru se tak dá trefit dělo na věži hradby.
    private float AimElevationDeg()
    {
        Transform cam = AimCamera;
        if (cam == null) return 0f;

        // Sklon kamery ve stupních (kladný = kouká dolů).
        float camPitch = Mathf.Asin(Mathf.Clamp(-cam.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        var orbit = cam.GetComponent<CameraOrbit>();
        if (orbit != null && orbit.IsFirstPerson)
            return Mathf.Clamp(-camPitch, -FP_MAX_AIM_ANGLE, FP_MAX_AIM_ANGLE);

        return Mathf.Clamp(AIM_NEUTRAL_PITCH - camPitch, -MAX_AIM_ANGLE, MAX_AIM_ANGLE);
    }

    // Vodorovný směr, kam se kamera dívá (pro pěší zbraň).
    private Vector3 AimHorizontalDir()
    {
        Transform cam = AimCamera;
        Vector3 f = cam != null ? cam.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    // Je vytažená pěší zbraň? (koupená + vybraná v hotbaru + hráč pěšky)
    private bool WeaponDrawn => isOnFoot && !footSwimming && PHasHandWeapon && PHotbarSlot == 0;

    // Když má hráč vytaženou zbraň, panáček se plynule otáčí tam, kam se dívá kamera —
    // zbraň míří "před něj" a střílí přesně tím směrem (bez toho by se otáčel jen podle chůze).
    void UpdateAimFacing()
    {
        if (!WeaponDrawn || headDot == null) return;
        Quaternion target = Quaternion.LookRotation(AimHorizontalDir());
        headDot.transform.rotation = Quaternion.Slerp(headDot.transform.rotation, target, 14f * Time.deltaTime);
    }

    // ── Střelba z lodního děla ─────────────────────────────────────────────
    void TryShoot()
    {
        if (!BoatStats.HasCannon(PShipLevel)) return; // veslice dělo nemá
        if (PAmmo <= 0) return;
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + SHOOT_COOLDOWN;

        PAmmo -= 1;

        // Koule letí vždy PŘED loď (podél přídě), nahoru / dolů podle kamery.
        Vector3 dir  = boatModel != null ? boatModel.forward : transform.forward;
        Vector3 from = transform.position + Vector3.up * 0.4f; // ústí děla nad palubou
        CombatDirector.Ensure();
        CannonBall.FireAimed(from, dir, AimElevationDeg(), BoatStats.CannonDamage(PShipLevel), CannonBall.Side.Player);

        SoundManager.PlayCannon();
        gridManager.NotifyWorldChanged(); // překresli munici v HUD
    }

    // ── Střelba z pěší zbraně (hotbar slot 0) ────────────────────────────────
    // Koupí se v obchodě (UpgradeShopManager.DrawWeaponRow) a má vlastní munici
    // (PHandAmmo), oddělenou od lodní (PAmmo). Funguje jen, když je zbraň zrovna
    // vybraná v hotbaru — vybrat munici/poklad zbraň "schová" (viz PHotbarSlot).
    void TryShootOnFoot()
    {
        if (!PHasHandWeapon || PHotbarSlot != 0) return;
        if (PHandAmmo <= 0) return;
        if (Time.time < nextHandShotTime) return;
        nextHandShotTime = Time.time + HAND_SHOOT_COOLDOWN;

        PHandAmmo -= 1;

        // Střílí se tam, kam se dívá kamera (vodorovně) a panáček se k tomu okamžitě otočí,
        // ať míří zbraní přesně směrem výstřelu. Nahoru / dolů podle sklonu kamery.
        Vector3 dir  = AimHorizontalDir();
        Vector3 from = fpWeapon != null && fpWeapon.activeSelf ? fpMuzzle.position : transform.position + Vector3.up * 0.9f;
        if (headDot != null) headDot.transform.rotation = Quaternion.LookRotation(dir);
        CombatDirector.Ensure();
        CannonBall.FireAimed(from, dir, AimElevationDeg(), HAND_SHOT_DAMAGE, CannonBall.Side.Player);

        SoundManager.PlayHandgun();
        FirstPersonShotEffect();
        gridManager.NotifyWorldChanged(); // překresli munici v hotbaru
    }

    /// <summary>Zásah do lodě (volá soubojový systém). Když loď plave → ubírá jí
    /// zdraví (a s 25% šancí trefí i panáčka). Při 0 se loď ROZBIJE — panáček
    /// vypadne do vody a musí doplavat k ostrovu a opravit ji v obchodě.
    /// Když hráč zrovna plave (rozbitá loď), zásah jde přímo do panáčka.</summary>
    public void DamageBoat(int dmg)
    {
        if (isOnFoot || dmg <= 0 || DeathScreen.IsOpenFor(playerIndex)) return;
        if (Time.time < damageGraceUntil) return;
        if (ModalOpen) return; // hráč zrovna nakupuje / mluví / je v majáku → nezraní ho to

        lastDamageTime = Time.time;

        // Rozbitá loď → hráč plave → koule trefí přímo jeho.
        if (PBoatWrecked) { DamagePlayer(dmg); return; }

        PBoatHealth -= dmg;
        SoundManager.PlaySplash();
        if (damageFeedback != null) damageFeedback.Play();

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
        PS.isOnFoot = false;
        damageGraceUntil = Time.time + 2f; // chvilka na nadechnutí

        // Půlka nákladu se vysype do vody jako vrak — chvíli plave, doplaveš k
        // němu a zachráníš ji zpět. Když ho necháš klesnout, je pryč.
        int lostFish = PFishCount     / 2;
        int lostTrea = PTreasureCount / 2;
        int lostAmmo = PAmmo          / 2;
        if (lostFish > 0 || lostTrea > 0 || lostAmmo > 0)
        {
            PFishCount     -= lostFish;
            PTreasureCount -= lostTrea;
            PAmmo          -= lostAmmo;
            WreckDebris.Spawn(transform.position, playerIndex, lostFish, lostTrea, lostAmmo);
        }

        DespawnParkedBoat();

        // Malý odraz od nejbližšího nebezpečí (ať hráč nezačíná plavat pirátovi pod dělem).
        if (CombatDirector.Instance != null)
        {
            Vector3 away = CombatDirector.Instance.AwayFromNearestThreat(transform.position);
            Vector3 escape = transform.position + away * 3f;
            int ex = Mathf.RoundToInt(escape.x), ey = Mathf.RoundToInt(escape.z);
            if (IsBoatWater(gridManager.GetTileType(ex, ey))) TeleportTo(ex, ey);
            CombatDirector.Instance.Toast(Loc.T("Loď se rozbila! Doplav k ostrovu a oprav ji v obchodě.",
                                                "Your boat is wrecked! Swim to an island and repair it in the shop."));
        }

        ShowBoatOrFoot();
        SoundManager.PlaySink();
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    /// <summary>Ubere hráči (panáčkovi) zdraví přímo. Při 0 → obrazovka smrti.</summary>
    public void DamagePlayer(int dmg)
    {
        if (dmg <= 0 || DeathScreen.IsOpenFor(playerIndex)) return;
        if (ModalOpen) return;

        lastDamageTime = Time.time;
        PPlayerHealth -= dmg;
        if (damageFeedback != null) damageFeedback.Play();
        gridManager.NotifyWorldChanged();
        if (PPlayerHealth <= 0) { PPlayerHealth = 0; gridManager.Save(); DeathScreen.Show(playerIndex); }
    }

    /// <summary>Vrátí hráči náklad zachráněný z vraku rozbité lodě.</summary>
    public void RecoverCargo(int fish, int treasure, int ammo)
    {
        PFishCount     += Mathf.Max(0, fish);
        PTreasureCount += Mathf.Max(0, treasure);
        PAmmo          += Mathf.Max(0, ammo);
        SoundManager.PlayCoin();
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    /// <summary>Přidá hráči mince (odměna za potopení piráta / zničení děla).</summary>
    public void RewardCoins(int amount)
    {
        if (amount <= 0) return;
        PCoins += amount;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    // Nahradí primitivního panáčka (děti headDotu) Kenney modelem postavy.
    // P1 = modré oblečení, P2 = červené. Když model v Resources není, nechá
    // původní primitiva.
    void BuildPlayerFigure()
    {
        if (headDot == null) return;

        // P2 vzniká jako kopie P1 → mohl by mít zděděný model (ve špatné barvě).
        var stale = headDot.transform.Find("CharModel");
        if (stale != null) DestroyImmediate(stale.gameObject);

        // Stejně tak zděděné věci v ruce z kopie P1 (starý HeldAnchor / staré rekvizity).
        foreach (string staleName in new[] { "HeldAnchor", "WeaponProp", "AmmoProp", "TreasureProp" })
        {
            var t = transform.Find(staleName);
            if (t != null) DestroyImmediate(t.gameObject);
        }

        Color tint = playerIndex == 1
            ? new Color(0.82f, 0.24f, 0.20f)  // P2 — červené oblečení
            : new Color(0.32f, 0.46f, 0.72f); // P1 — modré

        // Kde má primitivní panáček nohy (aby model stál na stejné výšce).
        float feetY = headDot.transform.position.y;
        bool  found = false;
        foreach (var mr in headDot.GetComponentsInChildren<MeshRenderer>(true))
        {
            float b = mr.bounds.min.y;
            if (!found || b < feetY) { feetY = b; found = true; }
        }

        var model = CharacterModel.TryBuild(headDot.transform, "character-male-a",
                                            CharacterModel.DEFAULT_SCALE, tint, "PlayerAnim",
                                            CharacterModel.LightSkin); // obličej světlý, ne zabarvený jako oblečení
        if (model == null) return;

        figureAnimator = CharacterModel.GetAnimator(model);
        figurePrevPos  = new Vector3(transform.position.x, 0f, transform.position.z); // ať anim nezačne "sprintem"

        // Kosti modelu, které ovládáme sami (natažená ruka s věcí, plavecké záběry).
        figureModelTf = model.transform;
        armRight = FindBone(model.transform, "arm-right");
        armLeft  = FindBone(model.transform, "arm-left");
        legRight = FindBone(model.transform, "leg-right");
        legLeft  = FindBone(model.transform, "leg-left");
        armCalibrated = false;

        BuildHeldItemProps();

        if (found)
        {
            var p = model.transform.position;
            model.transform.position = new Vector3(p.x, feetY, p.z);
        }
        figureHomePos = figureModelTf.localPosition; // kam se model vrátí po plavání
        figureHomeRot = figureModelTf.localRotation;

        // Schovej původní primitivní díly (Body / Head / Hat / Nose).
        foreach (Transform child in headDot.transform)
            if (child != model.transform) child.gameObject.SetActive(false);
    }

    // Věci "v ruce" postavičky — puška (hotbar 0), váček s náboji (1),
    // historický poklad (2). Všechny visí na jedné kotvě (heldAnchor), kterou
    // UpdateFigurePose() každý snímek staví do natažené ruky panáčka a natáčí ve
    // směru míření — takže věc opravdu drží v ruce a míří tam, kam se střílí.
    // (Kotva je potomek kořene hráče, ne modelu postavy — ten má v CharacterModel.TryBuild
    // vlastní kompenzační škálování podle rodiče, což dělalo věci v ruce
    // maličké a úplně mimo.) Věci se vytvoří rovnou, ale skryté — zobrazí/schová je
    // UpdateWeaponVisual() podle hotbaru.
    private static readonly Vector3 HELD_ITEM_ANCHOR = new Vector3(0.20f, 0.60f, 0.42f); // záloha, když model nemá kost ruky

    private Transform heldAnchor; // kam se věci v ruce připevňují (pohybuje se s rukou)

    private void BuildHeldItemProps()
    {
        var anchorGO = new GameObject("HeldAnchor");
        anchorGO.transform.SetParent(transform, false);
        anchorGO.transform.localPosition = HELD_ITEM_ANCHOR;
        heldAnchor = anchorGO.transform;

        weaponProp   = BuildRifleProp();
        ammoProp     = BuildAmmoPouchProp();
        treasureProp = BuildTreasureProp();

        weaponProp.SetActive(false);
        ammoProp.SetActive(false);
        treasureProp.SetActive(false);
    }

    // Puška — hlaveň + tělo + pažba, dost velká, ať je v ruce vidět. Hlaveň míří
    // dopředu = lokální +Z kotvy = směr míření. Kotva je v místě dlaně, takže tělo
    // pušky sedí těsně za ní a hlaveň před ní.
    private GameObject BuildRifleProp() => BuildRifleModel(heldAnchor, "WeaponProp");

    // Samotný model pušky z primitiv — používá ho věc v ruce panáčka i pohled z první osoby.
    private static GameObject BuildRifleModel(Transform parent, string name)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        Material dark  = MakeHeldMat(new Color(0.10f, 0.10f, 0.11f));
        Material metal = MakeHeldMat(new Color(0.42f, 0.44f, 0.48f));
        Material wood  = MakeHeldMat(new Color(0.32f, 0.22f, 0.14f));

        AddHeldPart(root.transform, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.03f, 0.30f),  new Vector3(0.04f, 0.26f, 0.04f), new Vector3(90f, 0f, 0f), metal);
        AddHeldPart(root.transform, PrimitiveType.Cube,     "Body",   new Vector3(0f, 0.01f, 0.00f),  new Vector3(0.07f, 0.09f, 0.28f), Vector3.zero, dark);
        AddHeldPart(root.transform, PrimitiveType.Cube,     "Stock",  new Vector3(0f, -0.03f, -0.24f), new Vector3(0.06f, 0.10f, 0.22f), new Vector3(-8f, 0f, 0f), wood);
        AddHeldPart(root.transform, PrimitiveType.Cube,     "Grip",   new Vector3(0f, -0.09f, -0.06f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(-18f, 0f, 0f), wood);
        return root;
    }

    // Váček s náboji do zbraně — jednoduchá kostička, aby bylo poznat, že hráč
    // zrovna drží munici (hotbar slot 1), ne zbraň.
    private GameObject BuildAmmoPouchProp()
    {
        var root = new GameObject("AmmoProp");
        root.transform.SetParent(heldAnchor, false);
        root.transform.localPosition = Vector3.zero;

        Material leather = MakeHeldMat(new Color(0.36f, 0.25f, 0.15f));
        AddHeldPart(root.transform, PrimitiveType.Cube, "Pouch", new Vector3(0f, 0f, 0.06f), new Vector3(0.14f, 0.12f, 0.10f), Vector3.zero, leather);
        return root;
    }

    // Historický poklad — malá zlatá truhlička (hotbar slot 2, jen když ho hráč má).
    private GameObject BuildTreasureProp()
    {
        var root = new GameObject("TreasureProp");
        root.transform.SetParent(heldAnchor, false);
        root.transform.localPosition = Vector3.zero;

        Material gold = MakeHeldMat(new Color(0.85f, 0.68f, 0.20f));
        AddHeldPart(root.transform, PrimitiveType.Cube, "Chest", new Vector3(0f, 0f, 0.08f), new Vector3(0.18f, 0.14f, 0.14f), Vector3.zero, gold);
        return root;
    }

    private static Material MakeHeldMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    private static void AddHeldPart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition    = localPos;
        go.transform.localScale       = scale;
        go.transform.localEulerAngles = euler;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // Ukaž přesně tu jednu věc v ruce, co odpovídá vybranému hotbar slotu
    // (-1 = nic v ruce, viz "zase stiskni stejnou klávesu" v Update()); na
    // lodi se nedrží nic (ruce jsou na kormidle). Levné volání (int
    // porovnání), klidně z Update() každý snímek.
    private void UpdateWeaponVisual()
    {
        if (weaponProp == null) return;
        int show = (isOnFoot && !footSwimming) ? PHotbarSlot : -1; // plavající panáček nic nedrží
        if (show == heldPropShown) return;
        heldPropShown = show;

        weaponProp.SetActive(show == 0 && PHasHandWeapon);
        ammoProp.SetActive(show == 1);
        treasureProp.SetActive(show == 2 && gridManager.gameData.hasHistoricalTreasure);
    }

    // ── Póza postavy: natažená ruka s věcí + plavání ───────────────────────
    // Model postavy (Kenney) má kosti arm-left/right, leg-left/right, torso, head.
    // Animátor umí jen stát/chodit, proto plavání a natažení ruky dělá kód
    // sám v LateUpdate (po animátoru) — žádné stažené animace.
    private Transform  figureModelTf;                            // kořen modelu postavy (CharModel)
    private Transform  armRight, armLeft, legRight, legLeft;     // kosti modelu (mohou být null)
    private Vector3    figureHomePos;                            // klidová lokální pozice modelu (po plavání se vrací)
    private Quaternion figureHomeRot;
    private bool       armCalibrated;                            // ví se, která osa kosti ruky míří dolů?
    private Vector3    armRightLocalDown = Vector3.down;         // osa kosti pravé ruky, co v klidu míří dolů
    private Quaternion armRightBase, armLeftBase, legRightBase, legLeftBase; // klidové lokální rotace kostí (základ pro záběry)
    private bool       footSwimming;                             // pěšky, ale stojí na vodním políčku → plave
    private bool       swimPoseActive;                           // model je právě v plavecké póze
    private float      swimClock;                                // čas pro střídavé záběry a houpání

    private const float SWIM_WATER_Y   = -0.28f; // výška středu těla plavce (hladina je kolem -0,22 → vyčnívá záda a hlava)
    private const float SWIM_BODY_HALF = 0.5f;   // asi polovina délky těla — o tolik se model posune, aby střed těla ležel ve vodě
    private const float ARM_LENGTH     = 0.30f;  // délka natažené ruky (kde je dlaň od ramene)
    private const float HELD_SIDE_WEAPON = 0.22f; // o kolik je zbraň v ruce posunutá do strany (doprava od hráče)
    private const float HELD_SIDE_OTHER  = 0.14f; // totéž pro náboje a poklad
    private const float HELD_DROP        = 0.04f; // a kousek dolů, ať nevisí ve výšce očí

    /// <summary>Plave pěší hráč (vešel z ostrova do vody)? Pro příšeru a další.</summary>
    public bool IsFootSwimming => footSwimming;

    /// <summary>Je hráč ve vodě — v lodi, s rozbitou lodí, nebo pěšky plavající?
    /// (Na souši / na molu ne — tam je před mořskou příšerou v bezpečí.)</summary>
    public bool IsInWater => enabled && gameObject.activeInHierarchy && (IsSailing || IsSwimming || footSwimming);

    /// <summary>Aktuální rychlost lodě hráče (j/s) včetně úrovně lodě a rychlostního
    /// upgradu — mořská příšera se jí řídí (je násobně rychlejší než loď).</summary>
    public float CurrentBoatSpeed
    {
        get
        {
            float s = moveSpeed * BoatStats.SpeedMultiplier(PShipLevel);
            if (PHasSpeedUpgrade) s *= 2f;
            return s;
        }
    }

    // Najde kost podle jména (kdekoli v hierarchii modelu), nebo null.
    static Transform FindBone(Transform root, string boneName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    // ── Brodění a plavání pěšáka ────────────────────────────────────────────
    // Vodní políčko začíná dřív, než je vidět voda: pláž (písek) se z ostrova
    // svažuje pod hladinu ještě o kus dál (IslandTerrain.BEACH) a mřížka políček
    // je vůči meshi posunutá o půl políčka. Podle samotné dlaždice by panáček
    // plaval už na písku. Proto se rozhoduje podle vzdálenosti od pevniny
    // (stejně jako terén): hladina je asi 0,4 od břehu, do 2 se brodí (chůze,
    // čím dál hlouběji ve vodě), a teprve dál se plave.
    private bool footWading;     // pěšky v mělké vodě → chůze, ne plavání
    private float wadeSink;      // o kolik je model kvůli brodění níž (j)
    private bool  wadeSinkApplied;
    private const float WADE_START_DIST = 0.40f; // od téhle vzdálenosti od pevniny je písek pod hladinou
    private const float WADE_SWIM_DIST  = 2.00f; // dál už je voda hluboká → plave
    private const float WADE_SPEED_MULT = 0.8f;
    private const float WADE_SINK_MAX   = 0.30f;
    private const float WADE_SLOPE_END  = 1.25f; // tam končí svah pláže (IslandTerrain.BEACH), dál je dno rovné

    void UpdateWaterState()
    {
        footSwimming = false;
        footWading   = false;
        wadeSink     = 0f;
        if (!isOnFoot) return;

        Vector3 pos = transform.position;
        TileType under = gridManager.GetTileType(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
        float dist = NearestLandDistance(pos.x, pos.z);

        footSwimming = IsBoatWater(under) && dist > WADE_SWIM_DIST;
        footWading   = !footSwimming && dist > WADE_START_DIST;
        if (footWading)
            wadeSink = Mathf.Clamp01((dist - WADE_START_DIST) / (WADE_SLOPE_END - WADE_START_DIST)) * WADE_SINK_MAX;
    }

    // Vzdálenost (světové jednotky) od bodu k nejbližšímu kousku pevniny. Políčko
    // [x,y] vizuálně pokrývá čtverec [x,x+1) × [y,y+1) (viz IslandTerrain). Hledá se
    // jen v okolí ±3 políčka (WADE_SWIM_DIST je 2) — dál už je to "daleko" (vrací 99).
    float NearestLandDistance(float wx, float wz)
    {
        int cx = Mathf.FloorToInt(wx);
        int cz = Mathf.FloorToInt(wz);
        float best = 99f * 99f;
        for (int x = cx - 3; x <= cx + 3; x++)
            for (int z = cz - 3; z <= cz + 3; z++)
            {
                TileType t = gridManager.GetTileType(x, z);
                if (!IsLand(t) && t != TileType.Lighthouse && t != TileType.Chest) continue;

                float dx = Mathf.Max(x - wx, 0f, wx - (x + 1f));
                float dz = Mathf.Max(z - wz, 0f, wz - (z + 1f));
                float sq = dx * dx + dz * dz;
                if (sq < best) best = sq;
            }
        return Mathf.Sqrt(best);
    }

    // Při brodění model o kousek klesne (voda mu sahá výš po nohou); jinak zpět.
    void ApplyWadeSink()
    {
        if (footWading)
        {
            figureModelTf.localPosition = figureHomePos + Vector3.down * wadeSink;
            wadeSinkApplied = true;
        }
        else if (wadeSinkApplied)
        {
            figureModelTf.localPosition = figureHomePos;
            wadeSinkApplied = false;
        }
    }

    // Volá se každý snímek PO animátoru: buď plavecká póza, nebo natažená ruka s věcí.
    void LateUpdate()
    {
        UpdateFirstPersonWeapon();

        if (headDot == null || figureModelTf == null || !headDot.activeInHierarchy) return;

        if (IsSwimming || footSwimming)
        {
            PoseSwimming();
            return;
        }

        if (swimPoseActive) EndSwimPose();
        TryCalibrateArm();
        PoseHeldItem();
        ApplyWadeSink();
    }

    // Plavání: tělo vodorovně u hladiny (hlava vpřed), ruce a nohy střídavě kývají.
    // Kosti nastavujeme ABSOLUTNĚ z klidové rotace (ne přičítáním), ať se záběry nehromadí.
    void PoseSwimming()
    {
        swimPoseActive = true;
        swimClock += Time.deltaTime;

        Quaternion yaw = Quaternion.Euler(0f, headDot.transform.eulerAngles.y, 0f);
        Quaternion lie = yaw * Quaternion.Euler(78f, 0f, 0f);   // naklonit dopředu = ležet na hladině
        float bob = Mathf.Sin(swimClock * 3f) * 0.03f;          // jemné houpání na vlnách

        figureModelTf.rotation = lie;
        // Počátek modelu je u nohou → posuň ho zpět podél těla, ať střed těla leží u hladiny.
        Vector3 center = new Vector3(transform.position.x, SWIM_WATER_Y + bob, transform.position.z);
        figureModelTf.position = center - (lie * Vector3.up) * SWIM_BODY_HALF;

        Vector3 axis = yaw * Vector3.right;
        float arm  = Mathf.Sin(swimClock * 6f);
        float kick = Mathf.Sin(swimClock * 8f);
        SwingBone(armRight, armRightBase,  arm * 55f,  axis);
        SwingBone(armLeft,  armLeftBase,  -arm * 55f,  axis);
        SwingBone(legRight, legRightBase,  kick * 25f, axis);
        SwingBone(legLeft,  legLeftBase,  -kick * 25f, axis);
    }

    // Kost = klidová rotace + otočení o "angle" kolem světové osy "axis".
    static void SwingBone(Transform bone, Quaternion baseLocal, float angle, Vector3 axis)
    {
        if (bone == null) return;
        bone.localRotation = baseLocal;
        bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }

    // Konec plavání: vrať model na místo (kosti si po chvíli převezme animátor).
    void EndSwimPose()
    {
        swimPoseActive = false;
        figureModelTf.localPosition = figureHomePos;
        figureModelTf.localRotation = figureHomeRot;
    }

    // Zjistí, která lokální osa kosti pravé ruky míří dolů (v klidové póze ruka visí).
    // Bez toho bychom nevěděli, jak ruku natáhnout dopředu. Zkouší se, dokud panáček
    // nestojí v klidu (ruka pak visí dolů); zároveň si zapamatuje klidové rotace kostí.
    void TryCalibrateArm()
    {
        if (armCalibrated || armRight == null) return;
        if (!isOnFoot || figureAnimSpeed > 0.3f) return;

        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        float best = -2f;
        Vector3 bestAxis = Vector3.down;
        foreach (var a in axes)
        {
            float d = Vector3.Dot(armRight.rotation * a, Vector3.down);
            if (d > best) { best = d; bestAxis = a; }
        }
        if (best < 0.6f) return; // ruka nevisí dolů → zkusíme později

        armRightLocalDown = bestAxis;
        armRightBase = armRight.localRotation;
        if (armLeft  != null) armLeftBase  = armLeft.localRotation;
        if (legRight != null) legRightBase = legRight.localRotation;
        if (legLeft  != null) legLeftBase  = legLeft.localRotation;
        armCalibrated = true;
    }

    // Když panáček drží věc (zbraň / náboje / poklad), natáhne před sebe pravou ruku
    // a věc mu sedí v dlani. Se zbraní míří ruka i hlaveň přesně ve směru střelby
    // (podle kamery, včetně náměru nahoru/dolů); bez zbraně ruka míří dopředu.
    void PoseHeldItem()
    {
        if (heldAnchor == null) return;
        if (!isOnFoot || footSwimming || PHotbarSlot < 0) return; // nic v ruce → ruku nechá animátor

        Vector3 dirH = WeaponDrawn ? AimHorizontalDir() : headDot.transform.forward;
        dirH.y = 0f;
        dirH = dirH.sqrMagnitude > 0.0001f ? dirH.normalized : Vector3.forward;
        float elev = WeaponDrawn ? AimElevationDeg() : 0f;

        // Směr paže / hlavně: vodorovný směr nakloněný nahoru / dolů o náměr.
        Vector3 right = Vector3.Cross(Vector3.up, dirH);
        Vector3 aim   = Quaternion.AngleAxis(-elev, right) * dirH;

        Vector3 hand;
        if (armRight != null && armCalibrated)
        {
            Vector3 down = armRight.rotation * armRightLocalDown;                          // kam ruka míří teď (animovaná)
            armRight.rotation = Quaternion.FromToRotation(down, aim) * armRight.rotation; // natáhnout ji ve směru míření
            hand = armRight.position + aim * ARM_LENGTH;                                    // dlaň = konec natažené ruky
            // Věc nemá viset uprostřed před tělem, ale v ruce po straně (zbraň víc do strany).
            hand += right * (PHotbarSlot == 0 ? HELD_SIDE_WEAPON : HELD_SIDE_OTHER) + Vector3.down * HELD_DROP;
        }
        else
        {
            // Bez kosti ruky: věc drží pevně před tělem (záloha).
            hand = headDot.transform.position + Quaternion.LookRotation(dirH) * HELD_ITEM_ANCHOR;
        }

        heldAnchor.position = hand;
        heldAnchor.rotation = Quaternion.LookRotation(aim, Vector3.up);
    }

    // ── Pohled z první osoby: zbraň v pravé ruce na obrazovce ──────────────────
    // V první osobě se vlastní model hráče schová (CameraOrbit.HideOwnRenderers),
    // takže by zbraň nebyla vidět. Proto má pušku ještě jednou jako "viewmodel"
    // připojený ke kameře: vpravo dole, míří dopředu. Při výstřelu cukne a na hlavni
    // krátce zazáří záblesk. Jen pro sólo hru (ve split-screenu by ji viděl i druhý hráč).
    private GameObject fpWeapon;      // puška připojená ke kameře
    private Transform  fpMuzzle;      // konec hlavně (odtud vylétá střela)
    private GameObject fpFlash;       // záblesk výstřelu
    private CameraOrbit fpOrbit;
    private float      fpRecoil;      // 1 = právě vystřeleno, doznívá k 0
    private float      fpFlashUntil;
    private static readonly Vector3 FP_WEAPON_POS = new Vector3(0.32f, -0.27f, 0.95f);
    private const float FP_WEAPON_SCALE = 0.8f;   // puška na obrazovce je menší než ta v ruce panáčka
    private const float FP_RECOIL_BACK = 0.10f;   // o kolik puška při výstřelu ucukne dozadu
    private const float FP_RECOIL_PITCH = 7f;     // a o kolik stupňů se zvedne hlaveň

    void UpdateFirstPersonWeapon()
    {
        Transform cam = AimCamera;   // solo: Camera.main (viewCamera dostane jen druhý hráč)
        if (MultiplayerManager.IsMultiplayer || cam == null) return;

        if (fpOrbit == null) fpOrbit = cam.GetComponent<CameraOrbit>();
        bool show = fpOrbit != null && fpOrbit.IsFirstPerson && WeaponDrawn;

        if (show && fpWeapon == null) BuildFirstPersonWeapon(cam);
        if (fpWeapon == null) return;

        if (fpWeapon.activeSelf != show) fpWeapon.SetActive(show);
        if (!show) return;

        fpRecoil = Mathf.MoveTowards(fpRecoil, 0f, 7f * Time.deltaTime);
        fpWeapon.transform.localPosition = FP_WEAPON_POS + Vector3.back * (FP_RECOIL_BACK * fpRecoil);
        fpWeapon.transform.localRotation = Quaternion.Euler(-FP_RECOIL_PITCH * fpRecoil, -3f, 0f);
        if (fpFlash != null) fpFlash.SetActive(Time.time < fpFlashUntil);
    }

    void BuildFirstPersonWeapon(Transform cam)
    {
        fpWeapon = BuildRifleModel(cam, "FirstPersonWeapon");
        fpWeapon.transform.localPosition = FP_WEAPON_POS;
        fpWeapon.transform.localScale = Vector3.one * FP_WEAPON_SCALE;
        if (fpOrbit != null) fpOrbit.keepVisibleRoot = fpWeapon.transform; // první osoba ji nesmí schovat

        // Ať puška nehází stín.
        foreach (var r in fpWeapon.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var muzzleGO = new GameObject("Muzzle");
        muzzleGO.transform.SetParent(fpWeapon.transform, false);
        muzzleGO.transform.localPosition = new Vector3(0f, 0.03f, 0.46f);
        fpMuzzle = muzzleGO.transform;

        // Záblesk: malá zářivá koule na hlavni (svítí sama, bez světla ve scéně).
        fpFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fpFlash.name = "MuzzleFlash";
        var col = fpFlash.GetComponent<Collider>();
        if (col != null) Destroy(col);
        fpFlash.transform.SetParent(fpMuzzle, false);
        fpFlash.transform.localScale = new Vector3(0.09f, 0.09f, 0.14f);
        var fr = fpFlash.GetComponent<MeshRenderer>();
        fr.sharedMaterial = MakeHeldMat(new Color(1f, 0.85f, 0.35f));
        if (fr.sharedMaterial.HasProperty("_EmissionColor"))
        {
            fr.sharedMaterial.EnableKeyword("_EMISSION");
            fr.sharedMaterial.SetColor("_EmissionColor", new Color(3f, 2.2f, 0.7f));
        }
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fpFlash.SetActive(false);
        fpWeapon.SetActive(false);
    }

    // Volá se při výstřelu z pěší zbraně: cuknutí pušky a záblesk (jen v první osobě).
    void FirstPersonShotEffect()
    {
        fpRecoil = 1f;
        fpFlashUntil = Time.time + 0.06f;
    }

    private Animator   figureAnimator;   // animátor pěší postavičky (idle/walk/sprint)
    private GameObject weaponProp, ammoProp, treasureProp; // věci "v ruce" — jen jedna vidět podle PHotbarSlot
    private int         heldPropShown = -2; // -2 = ještě nespočteno (donutí první Update() přepočítat)
    private Vector3  figurePrevPos;    // pozice z minulého snímku (na výpočet rychlosti)
    private float    figureAnimSpeed;  // vyhlazená rychlost pro blend tree

    // Podle skutečné rychlosti pohybu přepíná idle → walk → sprint.
    void UpdateFigureAnim()
    {
        if (figureAnimator == null) return;

        float raw = 0f;
        if (headDot != null && headDot.activeInHierarchy)
        {
            Vector3 p = new Vector3(transform.position.x, 0f, transform.position.z);
            raw = (p - figurePrevPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            figurePrevPos = p;
            if (footSwimming || IsSwimming) raw = 0f; // plavání řeší kód (PoseSwimming), ne animátor chůze
        }
        else
        {
            figurePrevPos = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        // Vyhlaď, ať blend tree neposkakuje.
        figureAnimSpeed = Mathf.Lerp(figureAnimSpeed, Mathf.Min(raw, 8f), 12f * Time.deltaTime);

        // Panáček je při plavbě na lodi vypnutý — neaktivnímu animátoru se parametr
        // nastavovat nedá (Unity by každý snímek vypsalo varování a zbytečně plnilo Console).
        if (figureAnimator.gameObject.activeInHierarchy)
            figureAnimator.SetFloat("Speed", figureAnimSpeed);
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
        PS.boatGridX = boatGridX;
        PS.boatGridY = boatGridY;
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
        if ((PBoatWrecked && !isOnFoot) || footSwimming)
        {
            speed *= BoatStats.SwimSpeedMultiplier;
        }
        else if (footWading)
        {
            speed *= WADE_SPEED_MULT; // v mělčině se jde o něco pomaleji
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
        TileType tile = gridManager.GetTileType(tx, ty);
        if (!CanEnter(tile)) return false;
        if (isOnFoot && MegaIslandMarker.BlocksWalking(tx, ty)) return false; // do obelisku se nechodí

        // Pěšák plave jen kousek od břehu — dál do moře jen lodí.
        if (isOnFoot && IsBoatWater(tile) && !LandWithin(tx, ty, FOOT_SWIM_RANGE)) return false;

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
            return IsBoatWater(t) || IsLand(t); // plavec smí vylézt na jakoukoli pevninu
        if (!isOnFoot)
            return IsBoatWater(t);
        // Pěšky: pevnina a molo — a navíc VODA (panáček může z ostrova rovnou plavat).
        return IsLand(t) || IsBoatWater(t);
    }

    private const int FOOT_SWIM_RANGE = 4; // kolik políček od pevniny smí pěšák doplavat

    // Je do `range` políček od bodu nějaká pevnina?
    bool LandWithin(int x, int y, int range)
    {
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
                if (IsLand(gridManager.GetTileType(x + dx, y + dy))) return true;
        return false;
    }

    // Pevnina, po které jde chodit pěšky (pevnina, molo, mega ostrov).
    static bool IsLand(TileType t)
        => t == TileType.Harbor || t == TileType.Pier || t == TileType.MegaIsland;

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
            if (IsLand(here))
            {
                isOnFoot = true;
                PS.isOnFoot = true;
                ShowBoatOrFoot();
            }
        }
        // Na lodi (ne plave) si pamatuj i pozici lodě (kde kotví).
        else if (!isOnFoot)
        {
            boatGridX = tx;
            boatGridY = ty;
            PS.boatGridX = boatGridX;
            PS.boatGridY = boatGridY;
        }

        // Doplul jsi k cíli z mapy (waypoint) → zruš ho.
        ClearWaypointIfReached(tx, ty);

        // Doplul jsi k příběhovému mega ostrovu (krok 2) → posuň příběh.
        var d0 = gridManager.gameData;
        if (d0.storyStep == 2 && d0.storyIslandActive
            && Mathf.Max(Mathf.Abs(tx - d0.storyIslandX), Mathf.Abs(ty - d0.storyIslandY)) <= 16)
            StoryNpc.OnReachedStoryIsland();

        // Plavba po trase k dalšímu mega ostrovu → může narazit na mořskou obludu (Krok 5).
        StoryEvents.CheckMonster(gridManager); // megalodon za Pirátským ostrovem
        StoryEvents.CheckGhost(gridManager);   // Bludný Holanďan při odjezdu z Hřbitova lodí

        gridManager.GenerateWorld(tx, ty);
        ExploreCurrentPosition();
    }

    // Když je hráč u svého waypointu (±1 políčko), cíl se splní a zmizí.
    void ClearWaypointIfReached(int tx, int ty)
    {
        if (!PS.hasWaypoint) return;

        int wx = PS.waypointX;
        int wy = PS.waypointY;
        if (Mathf.Abs(tx - wx) > 1 || Mathf.Abs(ty - wy) > 1) return;

        PS.hasWaypoint = false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Dorazil jsi k cíli z mapy.", "You have reached the target from the map."));
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
        if (WeaponDrawn) return; // s vytaženou zbraní panáčka otáčí míření (UpdateAimFacing), ne chůze
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
                    CombatDirector.Instance.Toast(Loc.T("Loď je rozbitá — oprav ji v obchodě s vylepšeními (v majáku).",
                                                        "Your boat is wrecked — repair it in the upgrade shop (in the lighthouse)."));
                return;
            }
            Vector2Int? land = FindAdjacent(px, py, TileType.Pier) ?? FindAdjacent(px, py, TileType.Harbor);
            if (land == null) return;
            isOnFoot = true;
            PS.isOnFoot = true;
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
            PS.boatGridX = px;
            PS.boatGridY = py;

            isOnFoot = true;
            PS.isOnFoot = true;
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
            bool boatReachable = dist <= (footSwimming ? 2 : 1) && IsBoatWater(gridManager.GetTileType(boatGridX, boatGridY));

            if (!boatReachable)
            {
                // Loď je pryč / nedosažitelná — když hráč stojí na molu (nebo vedle),
                // "připluj" s lodí na vodní políčko hned vedle toho mola.
                Vector2Int? spot = WaterNextToPierNear(px, py);
                if (spot == null) return; // fakt není kam / čím nasednout
                boatGridX = spot.Value.x;
                boatGridY = spot.Value.y;
                PS.boatGridX = boatGridX;
                PS.boatGridY = boatGridY;
            }

            isOnFoot = false;
            PS.isOnFoot = false;
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

    // Normální lokální výška panáčka (headDot).
    private Vector3 headDotHomeLocalPos;
    private bool    headDotHomeSaved;

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

            // Plavecká póza (tělo ležící u hladiny) se řeší v LateUpdate → PoseSwimming,
            // panáček se tedy už nepotápí — kotva zůstává na své normální výšce.
            headDot.transform.localPosition = headDotHomeLocalPos;
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

        // Mega ostrov 2 (Hřbitov lodí, Krok 6): kousek roztržené mapy v mělčině.
        if (MegaIslandMarker.Instance != null && MegaIslandMarker.Instance.HasDigSpot(cx, cy))
        {
            StartCoroutine(MegaDigRoutine(cx, cy));
            return;
        }

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
    private MegaQuest MyMegaQuest => PS.megaQuest;

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

    // Kopání kusu roztržené mapy na ostrově 2 (Krok 6) — stejné tempo jako
    // DigRoutine, výsledek zpracuje MegaIslandMarker.TryDig.
    IEnumerator MegaDigRoutine(int cx, int cy)
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

        MegaIslandMarker.Instance?.TryDig(cx, cy, playerIndex);

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

            // Mega ostrov (pokračování příběhu) — obelisk, později tabulky/trezor.
            if (MegaIslandMarker.Instance != null && MegaIslandMarker.Instance.TryInteract(tx, ty, playerIndex))
                return true;

            if (storyNpc != null && storyNpc.IsAt(tx, ty))
            {
                storyNpc.StartTalk(playerIndex);
                return true;
            }
            if (RivalNpc.Instance != null && RivalNpc.Instance.IsAt(tx, ty))
            {
                RivalNpc.Instance.StartTalk(playerIndex);
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
        // Dřív se P2 isOnFoot/boatGridX/Y během běžné hry vůbec neukládaly (jen
        // GridManager je jednorázově nastavil po respawnu) — sjednocením do
        // `players[]` (viz PS) se teď ukládají průběžně pro oba hráče stejně,
        // takže načtení je teď symetrické.
        isOnFoot  = PS.isOnFoot;
        boatGridX = PS.boatGridX;
        boatGridY = PS.boatGridY;

        isMoving = false;
        isWorking = false;
        WorkProgress = 0f;

        DespawnParkedBoat();
        ShowBoatOrFoot();
        TeleportTo(GridX, GridY);
    }

    // ── Kontextová nápověda mimo maják ("[E]/[Space] ...") ──────────────────
    // Stejný princip jako u dědy nebo v majáku (InteriorPlayer) — když hráč
    // stojí u něčeho interaktivního, ukaž mu, co s tím udělá E/Space. Bez tohohle
    // by první hraní nešlo poznat, že Space na rybím hejnu/pokladu vůbec něco dělá.
    private string GetContextHint()
    {
        string ekey = P1 ? "E" : "Numpad 1";
        string skey = P1 ? "Space" : "Numpad 0";

        if (isOnFoot)
        {
            int px = GridX, py = GridY;
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var d in dirs)
            {
                int tx = px + d.x, ty = py + d.y;

                if (MegaIslandMarker.Instance != null)
                {
                    string mh = MegaIslandMarker.Instance.GetHint(tx, ty);
                    if (mh != null) return $"[{ekey}]  {mh}";
                }

                TileType t = gridManager.GetTileType(tx, ty);
                if (t == TileType.Lighthouse) return $"[{ekey}]  " + Loc.T("vejít do majáku", "enter the lighthouse");
                if (t == TileType.Chest)      return $"[{ekey}]  " + Loc.T("otevřít bednu",   "open the chest");
            }

            // Nasednout do lodě — plave do 1 políčka a není rozbitá (tu se jen vyleze, viz TryToggleBoatFoot).
            if (!PBoatWrecked)
            {
                int dist = Mathf.Max(Mathf.Abs(px - boatGridX), Mathf.Abs(py - boatGridY));
                if (dist <= 1 && IsBoatWater(gridManager.GetTileType(boatGridX, boatGridY)))
                    return $"[{ekey}]  " + Loc.T("nastoupit do lodě", "board the boat");
            }
        }
        else if (!PBoatWrecked)
        {
            if (MegaIslandMarker.Instance != null && MegaIslandMarker.Instance.HasDigSpot(GridX, GridY))
                return $"[{skey}]  " + Loc.T("kopat (kousek mapy)", "dig (map piece)");

            TileType here = gridManager.GetTileType(GridX, GridY);
            if (here == TileType.Water_Fish) return $"[{skey}]  " + Loc.T("rybařit",       "fish");
            if (here == TileType.Treasure)   return $"[{skey}]  " + Loc.T("těžit poklad",  "salvage treasure");

            MegaQuest mq = MyMegaQuest;
            if (mq != null && mq.active && !mq.dug && GridX == mq.targetX && GridY == mq.targetY)
                return $"[{skey}]  " + Loc.T("vykopat poklad z mapy", "dig up the treasure from the map");

            if (FindAdjacent(GridX, GridY, TileType.Pier) != null) return $"[{ekey}]  " + Loc.T("vystoupit z lodě", "leave the boat");
        }
        return null;
    }

    // Hotbar (zbraň / boat ammo / rifle ammo / historický poklad) se kreslí v HUDCounter
    // (dole uprostřed, uGUI). Tady zůstává jen logika výběru slotu — viz Update().

    // ── Nápověda k opravě lodě + kontextová nápověda (dole na své půlce) ────
    private GUIStyle repairStyle, contextHintStyle;

    void OnGUI()
    {
        HudSkin.UseUiFont(); // čitelnější systémové písmo pro celé IMGUI (dialogy, menu, obchody)

        bool blocked = ModalOpen || isMoving || isWorking || GameConsole.IsOpen
                     || MainMenuManager.IsVisible || DeathScreen.IsOpenFor(playerIndex) || VaultMechanism.IsOpenFor(playerIndex);

        if (!blocked)
        {
            string hint = GetContextHint();
            if (hint != null)
            {
                if (contextHintStyle == null)
                    contextHintStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15, fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.85f, 0.85f, 0.7f) }
                    };
                GUI.Label(new Rect(HalfX(), Screen.height - 156f, HalfW(), 24f), hint, contextHintStyle);
            }
        }

        if (!CanRepairHere()) return;

        int missing = BoatStats.MaxHealth - PBoatHealth;
        int cost    = missing * REPAIR_COST_PER_HP;
        int afford  = Mathf.Min(missing, PCoins / REPAIR_COST_PER_HP);

        string key = P1 ? "R" : "Numpad /";
        int payNow = afford >= missing ? cost : afford * REPAIR_COST_PER_HP;
        string msg = afford > 0
            ? Loc.T($"[{key}]  opravit loď  ({payNow} {Loc.CoinsWord(payNow)})",
                    $"[{key}]  repair the boat  ({payNow} {Loc.CoinsWord(payNow)})")
            : Loc.T("na opravu lodě nemáš dost mincí", "you can't afford to repair the boat");

        if (repairStyle == null)
            repairStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f) }
            };

        GUI.Label(new Rect(HalfX(), Screen.height - 132f, HalfW(), 24f), msg, repairStyle);
    }

    // Šířka/X začátek "své" půlky obrazovky (celá obrazovka v sólu).
    private float HalfW() => MultiplayerManager.IsMultiplayer ? Screen.width * 0.5f : Screen.width;
    private float HalfX() => MultiplayerManager.IsMultiplayer && playerIndex == 1 ? Screen.width * 0.5f : 0f;
}
