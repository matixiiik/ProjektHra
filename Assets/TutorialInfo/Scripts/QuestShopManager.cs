using UnityEngine;

[DefaultExecutionOrder(100)]
public class QuestShopManager : MonoBehaviour
{
    public int fishSellPrice = 10;
    public int treasureSellPrice = 30;

    private GridManager gridManager;
    private bool isOpen;

    public bool IsOpen => isOpen;

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            isOpen = false;
            UpgradeShopManager.AnyShopOpen = false;
        }
    }

    private OfferedQuest[] offeredQuests;

    private GUIStyle titleStyle, sectionStyle, rowStyle, ownedStyle;
    private GUIStyle buyStyle, closeStyle, coinsStyle, claimStyle, progressStyle;
    private bool stylesReady;

    private struct OfferedQuest
    {
        public int type;
        public string desc;
        public int target;
        public int cost;
        public int multiplier;
    }

    private static readonly (int type, int tMin, int tMax, int cMin, int cMax, int mult)[] Templates =
    {
        (0,  5, 10,  20,  40, 2),
        (0, 20, 35,  60,  90, 4),
        (0, 50, 80, 100, 150, 7),
        (1,  3,  6,  30,  50, 3),
        (1,  8, 15,  70, 100, 5),
        (1, 18, 25, 120, 180, 8),
    };

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
    }

    public void Open()
    {
        isOpen = true;
        UpgradeShopManager.AnyShopOpen = true;
        if (!gridManager.gameData.activeQuest.hasQuest)
            GenerateOffers();
    }

    private void GenerateOffers()
    {
        offeredQuests = new OfferedQuest[3];
        int[] picks = PickDistinct(3, Templates.Length);
        for (int i = 0; i < 3; i++)
        {
            var t = Templates[picks[i]];
            int target = Random.Range(t.tMin, t.tMax + 1);
            int cost = Random.Range(t.cMin, t.cMax + 1);
            offeredQuests[i] = new OfferedQuest
            {
                type = t.type,
                desc = t.type == 0 ? $"Ulov {target} ryb" : $"Vytez {target} pokladu",
                target = target,
                cost = cost,
                multiplier = t.mult
            };
        }
    }

    private int[] PickDistinct(int count, int max)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int pick; bool unique;
            do
            {
                pick = Random.Range(0, max); unique = true;
                for (int j = 0; j < i; j++) if (result[j] == pick) { unique = false; break; }
            } while (!unique);
            result[i] = pick;
        }
        return result;
    }

    private void BuyQuest(OfferedQuest q)
    {
        GameData d = gridManager.gameData;
        if (d.coins < q.cost) return;
        d.coins -= q.cost;
        d.activeQuest.hasQuest = true;
        d.activeQuest.questType = q.type;
        d.activeQuest.description = q.desc;
        d.activeQuest.target = q.target;
        d.activeQuest.progress = 0;
        d.activeQuest.cost = q.cost;
        d.activeQuest.reward = q.cost * q.multiplier;
        d.activeQuest.multiplier = q.multiplier;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    public void ClaimQuest()
    {
        GameData d = gridManager.gameData;
        if (!d.activeQuest.hasQuest || !d.activeQuest.IsComplete) return;
        d.coins += d.activeQuest.reward;
        d.activeQuest.Reset();
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }

    private void InitStyles()
    {
        if (stylesReady) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.6f, 0.85f, 1f) }
        };
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        ownedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 1f, 0.4f) }
        };
        progressStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = new Color(1f, 0.85f, 0.4f) }
        };
        buyStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.2f, 0.45f, 0.2f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.6f, 0.3f)) }
        };
        claimStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.5f, 0.05f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.9f, 0.65f, 0.1f)) }
        };
        closeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.5f, 0.1f, 0.1f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.15f, 0.15f)) }
        };
        coinsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }
        };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        // Fullscreen dark overlay
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 580, h = 520;
        float px = (Screen.width - w) / 2f;
        float py = (Screen.height - h) / 2f;

        // Panel background
        GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.6f, 0.1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 25, py + 20, w - 50, h - 40));

        GUILayout.Label("OBCHOD S QUESTY", titleStyle);
        GUILayout.Space(12);

        GameData d = gridManager.gameData;

        // --- PRODEJ ---
        GUILayout.Label("PRODEJ", sectionStyle);
        DrawSell($"Ryby  x{d.fishCount}  ( {fishSellPrice} minci / kus )", d.fishCount * fishSellPrice, d.fishCount > 0,
            () => { d.coins += d.fishCount * fishSellPrice; d.fishCount = 0; Save(); });
        GUILayout.Space(4);
        DrawSell($"Poklady  x{d.treasureCount}  ( {treasureSellPrice} minci / kus )", d.treasureCount * treasureSellPrice, d.treasureCount > 0,
            () => { d.coins += d.treasureCount * treasureSellPrice; d.treasureCount = 0; Save(); });

        GUILayout.Space(14);

        // --- QUESTY ---
        GUILayout.Label("QUESTY", sectionStyle);

        if (d.activeQuest.hasQuest)
        {
            GUILayout.Label($"Aktivni:  {d.activeQuest.description}", rowStyle);
            GUILayout.Label($"Postup:   {d.activeQuest.progress} / {d.activeQuest.target}", progressStyle);
            GUILayout.Label($"Odmena:   {d.activeQuest.reward} minci  ( {d.activeQuest.multiplier}x )", rowStyle);
            GUILayout.Space(6);
            if (d.activeQuest.IsComplete)
            {
                if (GUILayout.Button($"  VYPLATIT  {d.activeQuest.reward} minci  !", claimStyle, GUILayout.Height(38)))
                    ClaimQuest();
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label("( quest jeste neni splnen )", rowStyle);
                GUI.color = Color.white;
            }
        }
        else
        {
            GUILayout.Label("Zadny aktivni quest — vyber si:", rowStyle);
            GUILayout.Space(6);
            if (offeredQuests != null)
            {
                for (int i = 0; i < offeredQuests.Length; i++)
                {
                    var q = offeredQuests[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{q.desc}    odmena: {q.cost * q.multiplier} minci  ( {q.multiplier}x )", rowStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{q.cost} minci", rowStyle, GUILayout.Width(90));
                    GUI.enabled = d.coins >= q.cost;
                    if (GUILayout.Button("Koupit", buyStyle, GUILayout.Width(80), GUILayout.Height(26)))
                    {
                        BuyQuest(q);
                        break;
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);
                }
            }
        }

        GUILayout.Space(12);
        GUILayout.Label($"Mince: {d.coins}", coinsStyle);
        GUILayout.EndArea();
    }

    private void DrawSell(string label, int total, bool enabled, System.Action onSell)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label($"= {total} minci", rowStyle, GUILayout.Width(110));
        GUI.enabled = enabled;
        if (GUILayout.Button("Prodat vse", buyStyle, GUILayout.Width(110), GUILayout.Height(26)))
            onSell();
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void Save()
    {
        gridManager.Save();
        gridManager.NotifyWorldChanged();
    }
}
