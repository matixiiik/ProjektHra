using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    [Header("UI root (Panel)")]
    public GameObject menuRoot;

    private bool isOpen = false;
    private GridManager grid;
    private PlayerController player;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        player = FindFirstObjectByType<PlayerController>();
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UpgradeShopManager.AnyShopOpen) return;
            if (isOpen) ContinueGame();
            else OpenMenu();
        }
    }

    private void SetMenuOpen(bool open)
    {
        isOpen = open;
        if (menuRoot != null) menuRoot.SetActive(open);
    }

    public void OpenMenu()
    {
        SetMenuOpen(true);
        Time.timeScale = 0f;
    }

    public void ContinueGame()
    {
        SetMenuOpen(false);
        Time.timeScale = 1f;
    }

    public void NewGame()
    {
        Time.timeScale = 1f;
        SetMenuOpen(false);
        if (grid != null) grid.NewGameReset();
        if (player != null) player.TeleportTo(grid.gameData.playerGridX, grid.gameData.playerGridY);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
