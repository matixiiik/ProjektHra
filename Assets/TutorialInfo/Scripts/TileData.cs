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
    public bool isExplored;
    public int fishRemaining;

    public TileStatus(int type)
    {
        this.type = type;
    }
}
