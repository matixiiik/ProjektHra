using System;

public enum TileType
{
    Empty = 0,
    Water = 1,
    Water_Fish = 2,
    Treasure = 3,
    Harbor = 4,
    Pier = 5
}

[Serializable]
public class TileStatus
{
    public int type;
    public bool isExplored = false; // Nové: pro minimapu a mlhu

    public TileStatus(int type)
    {
        this.type = type;
        this.isExplored = false;
    }
}