using UnityEngine;

public enum CellType
{
    Floor,
    Shelf,
    Checkout,
    Entrance,
    Exit,
    Wall
}

[System.Serializable]
public class GridCell
{
    public int x;
    public int y;
    public bool walkable;
    public CellType type;

    public Vector2Int Pos => new Vector2Int(x, y);

    public GridCell(int x, int y, bool walkable = true, CellType type = CellType.Floor)
    {
        this.x = x;
        this.y = y;
        this.walkable = walkable;
        this.type = type;
    }
}