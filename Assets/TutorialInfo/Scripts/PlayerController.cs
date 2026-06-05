using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [HideInInspector] public int playerIndex = 0; // 0 = P1 (WASD), 1 = P2 (numpad)

    private GridManager gridManager;
    private HarborManager harborManager;
    private UpgradeShopManager upgradeShopManager;
    private QuestShopManager questShopManager;

    public GameObject headDot;
    public Transform boatModel;
    public float moveSpeed = 5f;
    public float fishingDuration = 1.5f;
    public float miningDuration = 3.0f;

    private bool isMoving = false;
    private bool isWorking = false;

    public bool IsWorking => isWorking;
    public float WorkProgress { get; private set; }

    private int lastExploredX = -999;
    private int lastExploredY = -999;

    private bool isOnFoot = false;
    private int boatGridX;
    private int boatGridY;

    // ── Pozice routuje do správného pole podle playerIndex ────────────────────
    int GridX
    {
        get => playerIndex == 0 ? gridManager.gameData.playerGridX  : gridManager.gameData.player2GridX;
        set { if (playerIndex == 0) gridManager.gameData.playerGridX  = value; else gridManager.gameData.player2GridX = value; }
    }
    int GridY
    {
        get => playerIndex == 0 ? gridManager.gameData.playerGridY  : gridManager.gameData.player2GridY;
        set { if (playerIndex == 0) gridManager.gameData.playerGridY  = value; else gridManager.gameData.player2GridY = value; }
    }

    // ── Input helpery ─────────────────────────────────────────────────────────
    bool P1 => playerIndex == 0;
    bool Key    (KeyCode k1, KeyCode k2) => P1 ? Input.GetKey(k1)     : Input.GetKey(k2);
    bool KeyDown(KeyCode k1, KeyCode k2) => P1 ? Input.GetKeyDown(k1) : Input.GetKeyDown(k2);

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        gridManager        = FindFirstObjectByType<GridManager>();
        harborManager      = FindFirstObjectByType<HarborManager>();
        upgradeShopManager = FindFirstObjectByType<UpgradeShopManager>();
        questShopManager   = FindFirstObjectByType<QuestShopManager>();

        if (playerIndex == 0)
        {
            isOnFoot  = gridManager.gameData.isOnFoot;
            boatGridX = gridManager.gameData.boatGridX;
            boatGridY = gridManager.gameData.boatGridY;
            transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);
        }
        else
        {
            // P2 začíná na pozici P1, vždy v lodi
            isOnFoot  = false;
            boatGridX = gridManager.gameData.playerGridX;
            boatGridY = gridManager.gameData.playerGridY;
            gridManager.gameData.player2GridX = gridManager.gameData.playerGridX;
            gridManager.gameData.player2GridY = gridManager.gameData.playerGridY;
            transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);
        }

        if (isOnFoot)
        {
            if (boatModel != null) boatModel.gameObject.SetActive(false);
            if (headDot   != null) headDot.SetActive(true);
        }
        else
        {
            if (boatModel != null) boatModel.gameObject.SetActive(true);
            if (headDot   != null) headDot.SetActive(false);
        }

        ExploreCurrentPosition();
    }

    void Update()
    {
        bool shopOpen = (upgradeShopManager != null && upgradeShopManager.IsOpen)
                     || (questShopManager   != null && questShopManager.IsOpen);
        if (isMoving || isWorking || shopOpen || GameConsole.IsOpen || MainMenuManager.IsVisible) return;

        // E / Numpad1 → nastoupit/vystoupit nebo otevřít obchod
        if (KeyDown(KeyCode.E, KeyCode.Keypad1))
        {
            if (!TryOpenAdjacentShop()) TryToggleBoatFoot();
            return;
        }

        int x = 0, y = 0;
        int step = (!isOnFoot && gridManager.gameData.hasSpeedUpgrade) ? 2 : 1;

        if      (Key(KeyCode.W, KeyCode.UpArrow))    y =  step;
        else if (Key(KeyCode.S, KeyCode.DownArrow))  y = -step;
        else if (Key(KeyCode.A, KeyCode.LeftArrow))  x = -step;
        else if (Key(KeyCode.D, KeyCode.RightArrow)) x =  step;

        if (x != 0 || y != 0) AttemptMove(x, y);

        // Space / Numpad0 → interakce (rybaření / těžba)
        if (KeyDown(KeyCode.Space, KeyCode.Keypad0)) TryInteract();
    }

    void AttemptMove(int x, int y)
    {
        int targetX = GridX + x;
        int targetY = GridY + y;

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

        if (!CanEnter(gridManager.GetTileType(targetX, targetY))) return;

        RotateBoatModel(x, y);
        GridX = targetX;
        GridY = targetY;

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

    bool CanEnter(TileType t)
    {
        if (!isOnFoot) return t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure || t == TileType.Pier;
        return t == TileType.Harbor || t == TileType.Pier;
    }

    void MoveToGrid(int x, int y)
    {
        gridManager.GenerateWorld(x, y);
        StartCoroutine(SmoothMovement(x, y));
    }

    IEnumerator SmoothMovement(int tx, int ty)
    {
        isMoving = true;
        Vector3 target = new Vector3(tx, transform.position.y, ty);

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
        ExploreCurrentPosition();
        isMoving = false;
    }

    void ExploreCurrentPosition()
    {
        int cx = Mathf.RoundToInt(transform.position.x);
        int cy = Mathf.RoundToInt(transform.position.z);
        if (cx == lastExploredX && cy == lastExploredY) return;
        gridManager.MarkAreaExplored(cx, cy, 2);
        lastExploredX = cx;
        lastExploredY = cy;
    }

    void RotateBoatModel(int x, int y)
    {
        if (boatModel == null || isOnFoot) return;
        Vector3 dir = new Vector3(x, 0, y).normalized;
        if (dir != Vector3.zero) boatModel.rotation = Quaternion.LookRotation(dir);
    }

    void TryToggleBoatFoot()
    {
        int px = GridX;
        int py = GridY;

        if (!isOnFoot)
        {
            if (gridManager.GetTileType(px, py) != TileType.Pier) return;
            Vector2Int? exit = FindAdjacentHarbor(px, py);
            if (exit == null) return;

            isOnFoot = true;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = true;
            if (boatModel != null) boatModel.gameObject.SetActive(false);
            if (headDot   != null) headDot.SetActive(true);

            GridX = exit.Value.x;
            GridY = exit.Value.y;
            MoveToGrid(exit.Value.x, exit.Value.y);
        }
        else
        {
            int dist = Mathf.Abs(px - boatGridX) + Mathf.Abs(py - boatGridY);
            if (dist != 1) return;
            if (gridManager.GetTileType(boatGridX, boatGridY) != TileType.Pier) return;

            isOnFoot = false;
            if (playerIndex == 0) gridManager.gameData.isOnFoot = false;
            if (boatModel != null) boatModel.gameObject.SetActive(true);
            if (headDot   != null) headDot.SetActive(false);

            GridX = boatGridX;
            GridY = boatGridY;
            MoveToGrid(boatGridX, boatGridY);
        }
    }

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

    void TryInteract()
    {
        if (isOnFoot) return;
        int cx = GridX, cy = GridY;
        TileType type = gridManager.GetTileType(cx, cy);
        if (type == TileType.Water_Fish) StartCoroutine(FishingRoutine(cx, cy));
        else if (type == TileType.Treasure) StartCoroutine(MineRoutine(cx, cy));
    }

    bool TryOpenAdjacentShop()
    {
        if (!isOnFoot) return false;
        int px = GridX, py = GridY;
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
        {
            TileType t = gridManager.GetTileType(px + d.x, py + d.y);
            if (t == TileType.UpgradeShop && upgradeShopManager != null) { upgradeShopManager.Open(); return true; }
            if (t == TileType.QuestShop   && questShopManager   != null) { questShopManager.Open();   return true; }
        }
        return false;
    }

    IEnumerator FishingRoutine(int cx, int cy)
    {
        TileStatus tile = gridManager.GetTileStatus(cx, cy);
        if (tile == null) yield break;
        if (tile.fishRemaining <= 0) tile.fishRemaining = 3;

        isWorking = true;
        WorkProgress = 0f;

        float elapsed = 0f;
        while (elapsed < fishingDuration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / fishingDuration;
            yield return null;
        }

        int catchAmount = gridManager.gameData.hasRodUpgrade ? 2 : 1;
        tile.fishRemaining -= 1;
        gridManager.gameData.fishCount += catchAmount;

        ActiveQuest q = gridManager.gameData.activeQuest;
        if (q.hasQuest && q.questType == 0)
            q.progress = Mathf.Min(q.progress + catchAmount, q.target);

        if (tile.fishRemaining <= 0)
            gridManager.SetTileType(cx, cy, TileType.Water);

        gridManager.NotifyWorldChanged();
        WorkProgress = 0f;
        isWorking = false;
    }

    IEnumerator MineRoutine(int x, int y)
    {
        isWorking = true;
        WorkProgress = 0f;

        float duration = gridManager.gameData.hasMiningUpgrade ? miningDuration * 0.5f : miningDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / duration;
            yield return null;
        }

        gridManager.gameData.treasureCount += 1;

        ActiveQuest q = gridManager.gameData.activeQuest;
        if (q.hasQuest && q.questType == 1)
            q.progress = Mathf.Min(q.progress + 1, q.target);

        gridManager.SetTileType(x, y, TileType.Water);
        WorkProgress = 0f;
        isWorking = false;
    }

    public void TeleportTo(int x, int y)
    {
        GridX = x;
        GridY = y;
        gridManager.GenerateWorld(x, y);
        transform.position = new Vector3(x, 0.5f, y);
    }

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
        isMoving = false; isWorking = false; WorkProgress = 0f;

        if (isOnFoot) { if (boatModel) boatModel.gameObject.SetActive(false); if (headDot) headDot.SetActive(true); }
        else          { if (boatModel) boatModel.gameObject.SetActive(true);  if (headDot) headDot.SetActive(false); }

        TeleportTo(GridX, GridY);
    }
}
