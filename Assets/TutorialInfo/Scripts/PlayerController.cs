using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    private GridManager gridManager;
    private HarborManager harborManager;

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

        transform.position = new Vector3(gridManager.gameData.playerGridX, 0.5f, gridManager.gameData.playerGridY);
        boatGridX = gridManager.gameData.playerGridX;
        boatGridY = gridManager.gameData.playerGridY;

        ExploreCurrentPosition();
        if (headDot != null) headDot.SetActive(false);
    }

    void Update()
    {
        if (isMoving || isWorking) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
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

        if (!isOnFoot && Input.GetKeyDown(KeyCode.Space)) TryInteract();
    }

    void AttemptMove(int x, int y)
    {
        int targetX = gridManager.gameData.playerGridX + x;
        int targetY = gridManager.gameData.playerGridY + y;

        if (!CanEnter(gridManager.GetTileType(targetX, targetY))) return;

        RotateBoatModel(x, y);

        gridManager.gameData.playerGridX = targetX;
        gridManager.gameData.playerGridY = targetY;

        if (!isOnFoot)
        {
            boatGridX = targetX;
            boatGridY = targetY;
        }

        MoveToGrid(targetX, targetY);
    }

    bool CanEnter(TileType t)
    {
        if (!isOnFoot) return t != TileType.Harbor;
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
        int cx = gridManager.gameData.playerGridX;
        int cy = gridManager.gameData.playerGridY;
        TileType type = gridManager.GetTileType(cx, cy);

        if (type == TileType.Water_Fish) StartCoroutine(FishingRoutine(cx, cy));
        else if (type == TileType.Treasure) StartCoroutine(MineRoutine(cx, cy));
        else if (type == TileType.Harbor) harborManager.DisplayShopOptions();
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

        float elapsed = 0f;
        while (elapsed < miningDuration)
        {
            elapsed += Time.deltaTime;
            WorkProgress = elapsed / miningDuration;
            yield return null;
        }

        gridManager.gameData.treasureCount += 1;
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
