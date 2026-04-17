using UnityEngine;

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

        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) ContinueGame();
            else OpenMenu();
        }
    }

    public void OpenMenu()
    {
        isOpen = true;
        if (menuRoot != null) menuRoot.SetActive(true);
        Time.timeScale = 0f; // pause
    }

    public void ContinueGame()
    {
        isOpen = false;
        if (menuRoot != null) menuRoot.SetActive(false);
        Time.timeScale = 1f; // resume
    }

    public void NewGame()
    {
        // resume čas, ať se věci správně inicializují
        Time.timeScale = 1f;
        isOpen = false;
        if (menuRoot != null) menuRoot.SetActive(false);

        if (grid != null) grid.NewGameReset();
        if (player != null) player.TeleportTo(grid.gameData.playerGridX, grid.gameData.playerGridY);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;

        // Build
        Application.Quit();

        // Editor (aby to šlo testovat v Unity)
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}