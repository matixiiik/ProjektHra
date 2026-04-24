using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
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

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        harborManager = FindFirstObjectByType<HarborManager>();
        upgradeShopManager = FindFirstObjectByType<UpgradeShopManager>();
        questShopManager = FindFirstObjectByType<QuestShopManager>();

        // Obnov stav z ulozenych dat
        isOnFoot = gridManager.gameData.isOnFoot;
        boatGridX = gridManager.gameData.boatGridX;
        boatGridY = gridManager.gameData.boatGridY;

        transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);

        if (isOnFoot)
        {
            if (boatModel != null) boatModel.gameObject.SetActive(false);
            if (headDot != null) headDot.SetActive(true);
        }
        else
        {
            if (boatModel != null) boatModel.gameObject.SetActive(true);
            if (headDot != null) headDot.SetActive(false);
        }

        ExploreCurrentPosition();
    }

    void Update()
    {
        bool shopOpen = (upgradeShopManager != null && upgradeShopManager.IsOpen)
                     || (questShopManager != null && questShopManager.IsOpen);
        if (isMoving || isWorking || shopOpen) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!TryOpenAdjacentShop())
                TryToggleBoatFoot();
            return;
        }

        int x = 0, y = 0;
        int step = (!isOnFoot && gridManager.gameData.hasSpeedUpgrade) ? 2 : 1;

        if (Input.GetKey(KeyCode.W)) y = step;
        else if (Input.GetKey(KeyCode.S)) y = -step;
        else if (Input.GetKey(KeyCode.A)) x = -step;
        else if (Input.GetKey(KeyCode.D)) x = step;

        if (x != 0 || y != 0) AttemptMove(x, y);

        if (Input.GetKeyDown(KeyCode.Space)) TryInteract();
    }

    void AttemptMove(int x, int y)
    {
        int targetX = gridManager.gameData.playerGridX + x;
        int targetY = gridManager.gameData.playerGridY + y;

        // Kdyz se pohybujeme o 2 pole, zkontroluj prostredni pole
        // Pokud je tam ryba, poklad nebo pier, zastav se tam misto skoku pres nej
        if (Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2)
        {
            int midX = gridManager.gameData.playerGridX + x / 2;
            int midY = gridManager.gameData.playerGridY + y / 2;
            TileType midType = gridManager.GetTileType(midX, midY);
            if (midType == TileType.Water_Fish || midType == TileType.Treasure || midType == TileType.Pier)
            {
                targetX = midX;
                targetY = midY;
            }
        }

        if (!CanEnter(gridManager.GetTileType(targetX, targetY))) return;

        RotateBoatModel(x, y);

        gridManager.gameData.playerGridX = targetX;
        gridManager.gameData.playerGridY = targetY;

        if (!isOnFoot)
        {
            boatGridX = targetX;
            boatGridY = targetY;
            gridManager.gameData.boatGridX = boatGridX;
            gridManager.gameData.boatGridY = boatGridY;
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
        int px = gridManager.gameData.playerGridX;
        int py = gridManager.gameData.playerGridY;

        if (!isOnFoot)
        {
            if (gridManager.GetTileType(px, py) != TileType.Pier) return;
            Vector2Int? exit = FindAdjacentHarbor(px, py);
            if (exit == null) return;

            isOnFoot = true;
            gridManager.gameData.isOnFoot = true;
            boatModel.gameObject.SetActive(false);
            if (headDot != null) headDot.SetActive(true);

            gridManager.gameData.playerGridX = exit.Value.x;
            gridManager.gameData.playerGridY = exit.Value.y;
            MoveToGrid(exit.Value.x, exit.Value.y);
        }
        else
        {
            int dist = Mathf.Abs(px - boatGridX) + Mathf.Abs(py - boatGridY);
            if (dist != 1) return;
            if (gridManager.GetTileType(boatGridX, boatGridY) != TileType.Pier) return;

            isOnFoot = false;
            gridManager.gameData.isOnFoot = false;
            boatModel.gameObject.SetActive(true);
            if (headDot != null) headDot.SetActive(false);

            gridManager.gameData.playerGridX = boatGridX;
            gridManager.gameData.playerGridY = boatGridY;
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
        int cx = gridManager.gameData.playerGridX;
        int cy = gridManager.gameData.playerGridY;
        TileType type = gridManager.GetTileType(cx, cy);
        if (type == TileType.Water_Fish) StartCoroutine(FishingRoutine(cx, cy));
        else if (type == TileType.Treasure) StartCoroutine(MineRoutine(cx, cy));
    }

    bool TryOpenAdjacentShop()
    {
        if (!isOnFoot) return false;
        int px = gridManager.gameData.playerGridX;
        int py = gridManager.gameData.playerGridY;
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
        {
            TileType t = gridManager.GetTileType(px + d.x, py + d.y);
            if (t == TileType.UpgradeShop && upgradeShopManager != null) { upgradeShopManager.Open(); return true; }
            if (t == TileType.QuestShop && questShopManager != null) { questShopManager.Open(); return true; }
        }
        return false;
    }

    IEnumerator FishingRoutine(int cx, int cy)
    {
        TileStatus tile = gridManager.GetTileStatus(cx, cy);
        if (tile == null) yield break;

        // handle tiles created before fishRemaining was introduced
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
        int actual = Mathf.Min(catchAmount, tile.fishRemaining);
        tile.fishRemaining -= actual;
        gridManager.gameData.fishCount += actual;

        ActiveQuest q = gridManager.gameData.activeQuest;
        if (q.hasQuest && q.questType == 0)
            q.progress = Mathf.Min(q.progress + actual, q.target);

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
        gridManager.gameData.playerGridX = x;
        gridManager.gameData.playerGridY = y;
        gridManager.GenerateWorld(x, y);
        transform.position = new Vector3(x, 0.5f, y);
    }
}
