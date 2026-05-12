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

public enum ProductType
{
    None,
    Milk,
    Bread,
    Fruit,
    Snacks
}

[System.Serializable]
public class GridCell
{
    public int x;
    public int y;
    public bool walkable;
    public CellType type;
    public ProductType product = ProductType.None;

    //pentru rafturi: retine daca un raft este rezervat de un agent
    [System.NonSerialized] public CustomerAgent reservedBy;

    //pentru miscare: retine daca un tile este ocupat de un agent
    [System.NonSerialized] public CustomerAgent occupiedBy;

    //pentru miscare: retine daca un tile este deja rezervat ca urmator pas
    [System.NonSerialized] public CustomerAgent reservedForMove;

    public Vector2Int Pos => new Vector2Int(x, y);

    public GridCell(int x, int y, bool walkable = true, CellType type = CellType.Floor)
    {
        this.x = x;
        this.y = y;
        this.walkable = walkable;
        this.type = type;
    }
}