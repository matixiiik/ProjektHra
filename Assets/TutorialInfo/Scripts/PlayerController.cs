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

    public GameObject headDot;          // tečka nad hlavou, když je hráč pěšky
    public Transform  boatModel;        // 3D model lodě (přepíná ShipModelSwitcher)
    public float moveSpeed       = 5f;  // rychlost plynulého posunu mezi políčky
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
            // P2 startuje na pozici P1, vždy v lodi.
            isOnFoot  = false;
            boatGridX = gridManager.gameData.playerGridX;
            boatGridY = gridManager.gameData.playerGridY;
            gridManager.gameData.player2GridX = gridManager.gameData.playerGridX;
            gridManager.gameData.player2GridY = gridManager.gameData.playerGridY;
            transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);
        }

        // Zobraz správně loď / panáčka.
        ShowBoatOrFoot();

        ExploreCurrentPosition();
    }

    void Update()
    {
        // Když je otevřený obchod / konzole / menu, hráč se neovládá.
        bool shopOpen = (upgradeShopManager != null && upgradeShopManager.IsOpen)
                     || (questShopManager   != null && questShopManager.IsOpen);
        if (isMoving || isWorking || shopOpen || GameConsole.IsOpen || MainMenuManager.IsVisible) return;

        // E / Numpad1 → nastup/vystup z lodě, nebo vejdi do sousední budovy (maják).
        if (KeyDown(KeyCode.E, KeyCode.Keypad1))
        {
            if (!TryInteractAdjacentBuilding()) TryToggleBoatFoot();
            return;
        }

        // Směr pohybu. S rychlostním upgradem (a jen na lodi) se jde o 2 pole.
        int x = 0, y = 0;
        int step = (!isOnFoot && PHasSpeedUpgrade) ? 2 : 1;

        if      (Key(KeyCode.W, KeyCode.UpArrow))    y =  step;
        else if (Key(KeyCode.S, KeyCode.DownArrow))  y = -step;
        else if (Key(KeyCode.A, KeyCode.LeftArrow))  x = -step;
        else if (Key(KeyCode.D, KeyCode.RightArrow)) x =  step;

        if (x != 0 || y != 0) AttemptMove(x, y);

        // Space / Numpad0 → rybaření / těžba na aktuálním políčku.
        if (KeyDown(KeyCode.Space, KeyCode.Keypad0)) TryInteract();
    }

    // ── Pohyb ──────────────────────────────────────────────────────────────
    void AttemptMove(int x, int y)
    {
        int targetX = GridX + x;
        int targetY = GridY + y;

        // Krok o 2 pole: když je prostřední pole "zajímavé" (ryby / poklad / molo),
        // zastav se na něm místo přeskočení.
        if (Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2)
        {
            int midX = GridX + x / 2;
            int midY = GridY + y / 2;
            TileType midType = gridManager.GetTileType(midX, midY);
            if (midType == TileType.Water_Fish || midType == TileType.Treasure || midType == TileType.Pier)
            {
                targetX = midX;
                targetY = midY;
            }
        }

        // Nemůžu vplout/vejít → nedělej nic.
        if (!CanEnter(gridManager.GetTileType(targetX, targetY))) return;

        RotateBoatModel(x, y);

        GridX = targetX;
        GridY = targetY;

        // Na lodi si pamatuj i pozici lodě (kde kotví).
        if (!isOnFoot)
        {
            boatGridX = targetX;
            boatGridY = targetY;
            if (playerIndex == 0)
            {
                gridManager.gameData.boatGridX = boatGridX;
                gridManager.gameData.boatGridY = boatGridY;
            }
        }

        MoveToGrid(targetX, targetY);
    }

    // Na co smí hráč vstoupit? V lodi = voda a molo, pěšky = pevnina a molo.
    bool CanEnter(TileType t)
    {
        if (!isOnFoot)
            return t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure || t == TileType.Pier;
        return t == TileType.Harbor || t == TileType.Pier;
    }

    // Řekne světu, kde teď hráč je, a spustí plynulý přesun.
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

    // Odkryje mlhu kolem aktuální pozice (poloměr 2 políčka).
    void ExploreCurrentPosition()
    {
        int cx = Mathf.RoundToInt(transform.position.x);
        int cy = Mathf.RoundToInt(transform.position.z);
        if (cx == lastExploredX && cy == lastExploredY) return; // beze změny

        gridManager.MarkAreaExplored(cx, cy, 2);
        lastExploredX = cx;
        lastExploredY = cy;
    }

    // Otočí model lodě po směru pohybu.
    void RotateBoatModel(int x, int y)
    {
        if (boatModel == null || isOnFoot) return;
        Vector3 dir = new Vector3(x, 0, y).normalized;
        if (dir != Vector3.zero) boatModel.rotation = Quaternion.LookRotation(dir);
    }

    // ── Přesedání loď ↔ pěšky ──────────────────────────────────────────────
    void TryToggleBoatFoot()
    {
        int px = GridX;
        int py = GridY;

        if (!isOnFoot)
        {
            // Vystoupit z lodě jde jen z mola vedle pevniny.
            if (gridManager.GetTileType(px, py) != TileType.Pier) return;
            Vector2Int? exit = FindAdjacentHarbor(px, py);
            if (exit == null) return;

            isOnFoot = true;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = true;
            ShowBoatOrFoot();

            GridX = exit.Value.x;
            GridY = exit.Value.y;
            MoveToGrid(exit.Value.x, exit.Value.y);
        }
        else
        {
            // Nastoupit zpět jde jen když hráč stojí přesně vedle své lodě
            // a loď je na molu.
            int dist = Mathf.Abs(px - boatGridX) + Mathf.Abs(py - boatGridY);
            if (dist != 1) return;
            if (gridManager.GetTileType(boatGridX, boatGridY) != TileType.Pier) return;

            isOnFoot = false;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = false;
            ShowBoatOrFoot();

            GridX = boatGridX;
            GridY = boatGridY;
            MoveToGrid(boatGridX, boatGridY);
        }
    }

    // Zapne loď nebo tečku nad hlavou podle toho, jestli je hráč pěšky.
    void ShowBoatOrFoot()
    {
        if (boatModel != null) boatModel.gameObject.SetActive(!isOnFoot);
        if (headDot   != null) headDot.SetActive(isOnFoot);
    }

    // Najde políčko pevniny (Harbor) sousedící s [x,y]. Vrací null, když žádné není.
    Vector2Int? FindAdjacentHarbor(int x, int y)
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
        {
            int nx = x + d.x, ny = y + d.y;
            if (gridManager.GetTileType(nx, ny) == TileType.Harbor)
                return new Vector2Int(nx, ny);
        }
        return null;
    }

    // ── Rybaření / těžba / kopání ─────────────────────────────────────────
    void TryInteract()
    {
        if (isOnFoot) return; // pěšky se nepracuje
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

            if (t == TileType.Lighthouse && LighthouseManager.Instance != null)
            {
                LighthouseManager.Instance.Enter(playerIndex);
                return true;
            }
            if (t == TileType.Chest && ChestManager.Instance != null)
            {
                return ChestManager.Instance.TryOpen(tx, ty, playerIndex);
            }
            if (t == TileType.UpgradeShop && upgradeShopManager != null) { upgradeShopManager.Open(playerIndex); return true; }
            if (t == TileType.QuestShop   && questShopManager   != null) { questShopManager.Open(playerIndex);   return true; }
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

        // S lepším prutem hráč dostane 2 ryby, jinak 1. Z políčka ubyde 1 "hejno".
        int catchAmount = PHasRodUpgrade ? 2 : 1;
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

        // S upgradem těžby je práce 2× rychlejší.
        float duration = PHasMiningUpgrade ? miningDuration * 0.5f : miningDuration;
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

        ShowBoatOrFoot();
        TeleportTo(GridX, GridY);
    }
}
