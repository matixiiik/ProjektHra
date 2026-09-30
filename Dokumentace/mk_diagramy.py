# Generuje diagramy pro ročníkovou práci do složky obrazky/ (matplotlib).
# Spuštění:  python mk_diagramy.py
import os
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Ellipse, FancyArrowPatch, Rectangle

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "obrazky")
os.makedirs(OUT, exist_ok=True)
NAVY, LIGHT, GREY = "#1F4E79", "#D9E2F3", "#555555"


def box(ax, x, y, w, h, title, lines=(), fc=LIGHT, ec=NAVY, tfs=10, lfs=8, align="center"):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.02,rounding_size=0.08",
                                fc=fc, ec=ec, lw=1.4))
    ax.text(x + w / 2, y + h - 0.16, title, ha="center", va="top", fontsize=tfs,
            fontweight="bold", color=NAVY)
    for i, l in enumerate(lines):
        ax.text(x + w / 2 if align == "center" else x + 0.12, y + h - 0.5 - i * 0.28, l,
                ha=align, va="top", fontsize=lfs, color="#222222")


def arrow(ax, p, q, text=None, color=GREY, both=False, rad=0.0, fs=8, dy=0.12):
    a = FancyArrowPatch(p, q, arrowstyle="<|-|>" if both else "-|>", mutation_scale=13,
                        lw=1.3, color=color, connectionstyle="arc3,rad=%s" % rad)
    ax.add_patch(a)
    if text:
        ax.text((p[0] + q[0]) / 2, (p[1] + q[1]) / 2 + dy, text, ha="center", fontsize=fs,
                color=color, style="italic")


def architektura():
    fig, ax = plt.subplots(figsize=(10, 7.2))
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 7.2)
    ax.axis("off")
    ax.text(5, 7.0, "Architektura hry Poslední maják", ha="center", fontsize=14,
            fontweight="bold", color=NAVY)
    box(ax, 0.3, 5.3, 9.4, 1.35, "PREZENTACE (UI)",
        ["HUDCounter · MinimapUIRenderer · MainMenuManager · PauseMenu · ShopUI",
         "MapScreen · DeathScreen · dialogy (StoryNpc, RivalNpc) · VaultMechanism"], fc="#EAF1FB")
    box(ax, 0.3, 3.2, 4.5, 1.8, "HERNÍ LOGIKA – SampleScene",
        ["GridManager (svět, ostrovy) · PlayerController",
         "CombatDirector · PirateShip · HostileIslandCannon",
         "StoryNpc · RivalNpc · MegaIslandMarker"], fc="#EAF1FB")
    box(ax, 5.2, 3.2, 4.5, 1.8, "HERNÍ LOGIKA – LighthouseInterior",
        ["LighthouseManager · LighthouseInterior",
         "UpgradeShopManager · QuestShopManager",
         "InteriorPlayer · InteriorInteractable"], fc="#EAF1FB")
    box(ax, 0.3, 1.55, 9.4, 1.25, "STAV HRY (jediný zdroj pravdy)",
        ["GameSession (DontDestroyOnLoad, přežije přechod scén)  →  GameData: players[2], tileData, příběh, souboj",
         "EconomyConfig (všechna čísla ekonomiky) · TileType / TileStatus"], fc="#FFF4D6", ec="#B8860B")
    box(ax, 0.3, 0.15, 5.0, 1.15, "PERZISTENCE",
        ["SaveManager: JsonUtility ↔ save_0/1/2.json",
         "zápis na pozadí (.tmp + File.Replace), migrace verzí"], fc="#E8F5E9", ec="#2E7D32")
    box(ax, 5.7, 0.15, 4.0, 1.15, "PODPORA",
        ["Loc (CZ/EN) · SoundManager", "GameConsole · MultiplayerManager"], fc="#F3E5F5", ec="#6A1B9A")
    arrow(ax, (2.5, 5.3), (2.5, 5.0), both=True)
    arrow(ax, (7.5, 5.3), (7.5, 5.0), both=True)
    arrow(ax, (2.5, 3.2), (2.5, 2.8), both=True)
    arrow(ax, (7.5, 3.2), (7.5, 2.8), both=True)
    arrow(ax, (2.8, 1.55), (2.8, 1.3), both=True)
    ax.text(3.0, 1.42, "Save() / LoadGame()", fontsize=7.5, color=GREY, style="italic")
    fig.savefig(os.path.join(OUT, "architektura.png"), dpi=170, bbox_inches="tight")
    plt.close(fig)


def usecase():
    fig, ax = plt.subplots(figsize=(10, 7))
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 7)
    ax.axis("off")
    ax.text(5, 6.8, "Use Case diagram – Poslední maják", ha="center", fontsize=14,
            fontweight="bold", color=NAVY)
    ax.add_patch(Rectangle((2.2, 0.3), 5.6, 6.15, fc="#FAFAFA", ec=NAVY, lw=1.5))
    ax.text(5, 6.25, "Systém: hra", ha="center", fontsize=9, color=NAVY, fontweight="bold")
    ucs = [("UC1 Plout a objevovat", 5.55), ("UC2 Rybařit / těžit", 4.75),
           ("UC3 Prodat a nakoupit", 3.95), ("UC4 Bojovat", 3.15),
           ("UC5 Plnit úkol", 2.35), ("UC6 Postupovat v příběhu", 1.55),
           ("UC7 Uložit a pokračovat", 0.8)]
    for t, y in ucs:
        ax.add_patch(Ellipse((5, y), 4.0, 0.6, fc=LIGHT, ec=NAVY, lw=1.2))
        ax.text(5, y, t, ha="center", va="center", fontsize=9)

    def actor(x, y, label):
        ax.add_patch(plt.Circle((x, y + 0.55), 0.16, fc="white", ec="black", lw=1.4))
        ax.plot([x, x], [y + 0.39, y - 0.1], "k", lw=1.4)
        ax.plot([x - 0.3, x + 0.3], [y + 0.25, y + 0.25], "k", lw=1.4)
        ax.plot([x, x - 0.25], [y - 0.1, y - 0.55], "k", lw=1.4)
        ax.plot([x, x + 0.25], [y - 0.1, y - 0.55], "k", lw=1.4)
        ax.text(x, y - 0.75, label, ha="center", va="top", fontsize=9, fontweight="bold")

    actor(1.0, 3.6, "Hráč 1 / Hráč 2\n(lokální)")
    actor(9.0, 3.4, "Hra jako systém\n(spawn pirátů,\ndělo ostrova)")
    for _, y in ucs:
        ax.plot([1.35, 3.0], [3.6, y], color=GREY, lw=0.9)
    for y in (3.15, 1.55):
        ax.plot([8.65, 7.0], [3.4, y], color=GREY, lw=0.9, ls="--")
    ax.text(5, 0.05, "UC3 navazuje na UC2 (prodej kořisti) · UC5 a UC6 navazují na UC3",
            ha="center", fontsize=7.5, color=GREY, style="italic")
    fig.savefig(os.path.join(OUT, "usecase.png"), dpi=170, bbox_inches="tight")
    plt.close(fig)


def trida():
    fig, ax = plt.subplots(figsize=(10.5, 7.6))
    ax.set_xlim(0, 10.5)
    ax.set_ylim(0, 7.6)
    ax.axis("off")
    ax.text(5.25, 7.4, "Datový model ukládaného stavu (GameData → JSON)", ha="center",
            fontsize=14, fontweight="bold", color=NAVY)
    box(ax, 0.2, 3.7, 3.9, 3.4, "GameData",
        ["players : PlayerState[2]", "tileData : SerializableDictionary<string,TileStatus>",
         "hostileIslands / clearedIslands : List<string>", "openedChests, mappedIslands, …",
         "pirateKills : int", "storyStep, storyDone, storyEnding : int/bool",
         "megaIndex, megaTask, megaCode : int", "saveVersion : int  (dnes 2)"],
        fc="#FFF4D6", ec="#B8860B", align="left", lfs=7.6)
    box(ax, 4.9, 4.55, 2.6, 2.55, "PlayerState",
        ["gridX, gridY, isOnFoot", "coins, fishCount, treasureCount", "shipLevel, boatHealth,",
         "playerHealth, ammo, hasMap", "upgrady, hotbar, handAmmo"], align="left", lfs=7.6)
    box(ax, 8.0, 5.55, 2.3, 1.55, "ActiveQuest",
        ["hasQuest, questType", "target, progress", "cost, reward"], align="left", lfs=7.6)
    box(ax, 8.0, 3.7, 2.3, 1.55, "MegaQuest",
        ["active, targetX/Y", "dug, rewardCoins", "grantsHistoricalTreasure"], align="left", lfs=7.6)
    box(ax, 4.9, 2.2, 2.6, 1.75, "TileStatus",
        ["type : int (TileType)", "isExplored : bool", "fishRemaining : int"], align="left", lfs=7.6)
    box(ax, 0.2, 0.35, 3.3, 2.6, "enum TileType (jako int!)",
        ["Empty=0  Water=1  Water_Fish=2", "Treasure=3  Harbor=4  Pier=5", "UpgradeShop=6*  QuestShop=7*",
         "Lighthouse=8  Chest=9", "MegaIsland=10", "* zděděné, nový kód je nestaví"],
        fc="#F3E5F5", ec="#6A1B9A", align="left", lfs=7.6)
    box(ax, 5.0, 0.35, 5.3, 1.5, "SaveManager (statická třída)",
        ["SaveGame / SaveGameAsync · LoadGame → ParseAndMigrate",
         "save_0.json … save_2.json v persistentDataPath"], fc="#E8F5E9", ec="#2E7D32",
        align="left", lfs=7.6)
    arrow(ax, (4.1, 5.9), (4.9, 5.9), "2")
    arrow(ax, (7.5, 6.3), (8.0, 6.3), "1")
    arrow(ax, (7.5, 5.0), (8.0, 4.6), "1")
    arrow(ax, (4.1, 4.3), (4.9, 3.4), "0..*", rad=0.1)
    arrow(ax, (2.15, 3.7), (2.15, 2.95), "typ dlaždice", dy=0.0)
    arrow(ax, (3.8, 3.7), (5.0, 1.2), "serializuje do JSON", rad=0.15, color="#2E7D32", dy=-0.3)
    fig.savefig(os.path.join(OUT, "datovy_model.png"), dpi=170, bbox_inches="tight")
    plt.close(fig)


architektura()
usecase()
trida()
print("OK ->", OUT)
