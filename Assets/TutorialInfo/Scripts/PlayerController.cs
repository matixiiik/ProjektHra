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

    private int lastExploredX = -999;
    private int lastExploredY = -999;

    // ✅ NOVÉ: režimy + pozice lodě
    private bool isOnFoot = false; // false = loď, true = panáček na ostrově
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

        // ✅ E: vystoupit / nastoupit
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryToggleBoatFoot();
            return;
        }

        int x = 0, y = 0;

        // rychlost: na noze vždy jen 1, v lodi může být 2
        int step = (!isOnFoot && gridManager.gameData.hasSpeedUpgrade) ? 2 : 1;

        if (Input.GetKey(KeyCode.W)) y = step;
        else if (Input.GetKey(KeyCode.S)) y = -step;
        else if (Input.GetKey(KeyCode.A)) x = -step;
        else if (Input.GetKey(KeyCode.D)) x = step;

        if (x != 0 || y != 0) AttemptMove(x, y);

        // Space interakce jen v lodi
        if (!isOnFoot && Input.GetKeyDown(KeyCode.Space)) TryInteract();
    }

    void AttemptMove(int x, int y)
    {
        int targetX = gridManager.gameData.playerGridX + x;
        int targetY = gridManager.gameData.playerGridY + y;
        TileType targetType = gridManager.GetTileType(targetX, targetY);

        if (!CanEnter(targetType))
            return;

        RotateBoatModel(x, y);

        gridManager.gameData.playerGridX = targetX;
        gridManager.gameData.playerGridY = targetY;

        // ✅ když jsi v lodi, aktualizuj pozici lodě
        if (!isOnFoot)
        {
            boatGridX = targetX;
            boatGridY = targetY;
        }

        gridManager.GenerateWorld(targetX, targetY);
        StartCoroutine(SmoothMovement(targetX, targetY));
    }

    bool CanEnter(TileType t)
    {
        if (!isOnFoot)
        {
            // ✅ LOĎ: nesmí přejet přes ostrov (Harbor)
            // smí: Water, Fish, Treasure, Pier
            if (t == TileType.Harbor) return false;
            return true;
        }
        else
        {
            // ✅ PANÁČEK: smí jen po ostrově (Harbor) a po Pier, do vody nesmí
            return (t == TileType.Harbor || t == TileType.Pier);
        }
    }

    IEnumerator SmoothMovement(int tx, int ty)
    {
        isMoving = true;
        Vector3 target = new Vector3(tx, transform.position.y, ty);

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            ExploreCurrentPosition();
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

        if (cx != lastExploredX || cy != lastExploredY)
        {
            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                    gridManager.MarkTileExplored(cx + x, cy + y);

            lastExploredX = cx;
            lastExploredY = cy;
        }
    }

    void RotateBoatModel(int x, int y)
    {
        if (boatModel == null) return;

        // když jsi panáček, loď se netočí podle pohybu panáčka
        if (isOnFoot) return;

        Vector3 dir = new Vector3(x, 0, y).normalized;
        if (dir != Vector3.zero) boatModel.rotation = Quaternion.LookRotation(dir);
    }

    // ✅ NOVÉ: E – vystoupit/nastoupit
    void TryToggleBoatFoot()
    {
        int px = gridManager.gameData.playerGridX;
        int py = gridManager.gameData.playerGridY;

        if (!isOnFoot)
        {
            // VYSTOUPIT: musíš být lodí na Pier
            if (gridManager.GetTileType(px, py) != TileType.Pier) return;

            Vector2Int? exit = FindAdjacentHarbor(px, py);
            if (exit == null) return;

            isOnFoot = true;
            boatModel.gameObject.SetActive(false);
            if (headDot != null) headDot.SetActive(true);

            gridManager.gameData.playerGridX = exit.Value.x;
            gridManager.gameData.playerGridY = exit.Value.y;

            gridManager.GenerateWorld(exit.Value.x, exit.Value.y);
            StartCoroutine(SmoothMovement(exit.Value.x, exit.Value.y));
        }
        else
        {
            // NASTOUPIT: musíš stát vedle lodě (1 tile) a loď musí být na Pier
            int dist = Mathf.Abs(px - boatGridX) + Mathf.Abs(py - boatGridY);
            if (dist != 1) return;

            if (gridManager.GetTileType(boatGridX, boatGridY) != TileType.Pier) return;

            isOnFoot = false;
            boatModel.gameObject.SetActive(true);
            if (headDot != null) headDot.SetActive(false);

            gridManager.gameData.playerGridX = boatGridX;
            gridManager.gameData.playerGridY = boatGridY;

            gridManager.GenerateWorld(boatGridX, boatGridY);
            StartCoroutine(SmoothMovement(boatGridX, boatGridY));
        }
    }

    Vector2Int? FindAdjacentHarbor(int x, int y)
    {
        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;
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

        if (type == TileType.Water_Fish) StartCoroutine(FishingRoutine());
        else if (type == TileType.Treasure) StartCoroutine(MineRoutine(cx, cy));
        else if (type == TileType.Harbor) harborManager.DisplayShopOptions();
    }

    IEnumerator FishingRoutine()
    {
        isWorking = true;
        yield return new WaitForSeconds(fishingDuration);
        int fish = gridManager.gameData.hasRodUpgrade ? 2 : 1;
        gridManager.gameData.coins += fish;
        isWorking = false;
    }

    IEnumerator MineRoutine(int x, int y)
    {
        isWorking = true;
        yield return new WaitForSeconds(miningDuration);
        gridManager.gameData.coins += 100;
        gridManager.SetTileType(x, y, TileType.Water);
        isWorking = false;
    }
    public void TeleportTo(int x, int y)
    {
        gridManager.gameData.playerGridX = x;
        gridManager.gameData.playerGridY = y;

        // okolo se znovu vygeneruje
        gridManager.GenerateWorld(x, y);

        // fyzicky posuň objekt
        transform.position = new Vector3(x, 0.5f, y);

        // reset trail pomocných proměnných
        // (aby se ti to hned hezky odkrylo)
        // pokud ty proměnné nemáš public, tak je to OK i bez toho
    }   
}