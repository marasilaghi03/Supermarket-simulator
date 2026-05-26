using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    public int width = 18;
    public int height = 14;
    public float cellSize = 1f;
    public Vector3 offset = Vector3.zero;

    [Header("View")]
    public bool showGrid = true;
    public bool showLabels = false;

    private GridCell[,] grid;

    private void OnEnable()
    {
        Build();
        MakeTestLayout();
    }

    public void Build()
    {
        grid = new GridCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = new GridCell(x, y);
            }
        }
    }

    private void MakeTestLayout()
    {
        ClearLayout();

        PlaceEntranceAndExit();
        PlaceCheckoutAndQueue();
        PlaceShelves();
        PlaceWalls();
    }

    private void ClearLayout()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Set(x, y, CellType.Empty, true);
                grid[x, y].product = ProductType.None;
            }
        }
    }

    private void PlaceEntranceAndExit()
    {
        Set(0, 6, CellType.Entrance, true);
        Set(14, 0, CellType.Exit, true);
    }

    private void PlaceCheckoutAndQueue()
    {

        Set(16, 1, CellType.Checkout, false);

        for (int y = 2; y <= 12; y++)
        {
            Set(16, y, CellType.QueueEntry, true);
        }

    }

    private void PlaceShelves()
    {
        SetShelf(3, 10, ProductType.Milk);
        SetShelf(4, 10, ProductType.Milk);

        SetShelf(10, 10, ProductType.Bread);
        SetShelf(9, 10, ProductType.Bread);

        SetShelf(14, 9, ProductType.Snacks);
        SetShelf(14, 10, ProductType.Snacks);

        SetShelf(4, 6, ProductType.Fruit);
        SetShelf(5, 6, ProductType.Fruit);

        SetShelf(10, 6, ProductType.Bread);
        SetShelf(9, 6, ProductType.Bread);

        SetShelf(14, 5, ProductType.Snacks);
        SetShelf(14, 6, ProductType.Snacks);

        SetShelf(3, 2, ProductType.Fruit);
        SetShelf(4, 2, ProductType.Fruit);

        SetShelf(10, 2, ProductType.Milk);
        SetShelf(9, 2, ProductType.Milk);
    }

    private void PlaceWalls()
    {
        Set(0, 7, CellType.Wall, false);
        Set(0, 5, CellType.Wall, false);

        Set(17, 1, CellType.Wall, false);
        Set(15, 0, CellType.Wall, false);
        Set(13, 0, CellType.Wall, false);

        for (int x = 0; x < width; x++)
        {
            Set(x, height - 1, CellType.Wall, false);
        }
    }

    public void SetShelf(int x, int y, ProductType product)
    {
        Set(x, y, CellType.Shelf, false);
        grid[x, y].product = product;
    }

    public void Set(int x, int y, CellType type, bool walkable)
    {
        if (!Inside(x, y))
            return;

        grid[x, y].type = type;
        grid[x, y].walkable = walkable;

        if (type != CellType.Shelf)
            grid[x, y].product = ProductType.None;
    }

    public bool Inside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public GridCell Get(int x, int y)
    {
        if (!Inside(x, y))
            return null;

        return grid[x, y];
    }

    public GridCell Get(Vector2Int pos)
    {
        return Get(pos.x, pos.y);
    }

    public bool Walkable(int x, int y)
    {
        GridCell cell = Get(x, y);
        return cell != null && cell.walkable;
    }

    public bool FreeForAgent(GridCell cell, CustomerAgent agent)
    {
        if (cell == null || !cell.walkable)
            return false;

        if (cell.occupiedBy != null && cell.occupiedBy != agent)
            return false;

        if (cell.reservedForMove != null && cell.reservedForMove != agent)
            return false;

        return true;
    }

    public GridCell Find(CellType type)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].type == type)
                    return grid[x, y];
            }
        }

        return null;
    }

    public List<GridCell> FindAll(CellType type)
    {
        List<GridCell> result = new List<GridCell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];

                if (cell.type == type)
                    result.Add(cell);
            }
        }

        return result;
    }

    public GridCell RandomWalkable()
    {
        List<GridCell> cells = new List<GridCell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];

                if (cell.walkable)
                    cells.Add(cell);
            }
        }

        if (cells.Count == 0)
            return null;

        return cells[Random.Range(0, cells.Count)];
    }

    public List<GridCell> FindShelves(ProductType product)
    {
        List<GridCell> result = new List<GridCell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];

                if (cell.type == CellType.Shelf &&
                    cell.product == product &&
                    cell.stock > 0)
                    result.Add(cell);
            }
        }

        return result;
    }

    public List<ProductType> GetProducts()
    {
        List<ProductType> result = new List<ProductType>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];

                if (cell.type == CellType.Shelf &&
                    cell.product != ProductType.None &&
                    !result.Contains(cell.product))
                {
                    result.Add(cell.product);
                }
            }
        }

        return result;
    }

    public Vector3 World(int x, int y)
    {
        return new Vector3(x * cellSize, y * cellSize, 0f) + offset;
    }

    public Vector3 World(Vector2Int pos)
    {
        return World(pos.x, pos.y);
    }

    public Vector2Int Grid(Vector3 worldPos)
    {
        Vector3 p = worldPos - offset;

        int x = Mathf.RoundToInt(p.x / cellSize);
        int y = Mathf.RoundToInt(p.y / cellSize);

        return new Vector2Int(x, y);
    }

    public void SetShelf(int x, int y, ProductType product, int stock = 5)
    {
        Set(x, y, CellType.Shelf, false);
        grid[x, y].product = product;
        grid[x, y].stock = stock;
    }


    public void ClearMoveReservationsFor(CustomerAgent agent)
    {
        if (agent == null || grid == null)
            return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];

                if (cell.reservedForMove == agent)
                    cell.reservedForMove = null;
            }
        }
    }


    

}