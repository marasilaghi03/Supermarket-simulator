using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    public int width = 12;
    public int height = 8;
    public float cellSize = 1f;
    public Vector3 offset = Vector3.zero;

    [Header("View")]
    public bool showGrid = true;
    public bool showLabels = false;

    private GridCell[,] grid;   //matrice 2d care contine toate celulele

    private void OnEnable()
    {
        Build();
        MakeTestLayout();
    }

    public void Build()        //creeaza grid-ul
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
        Set(0, 3, CellType.Entrance, true);
        Set(0, 2, CellType.Exit, true);
        Set(10, 1, CellType.Checkout, false);

        SetShelf(4, 5, ProductType.Milk);
        SetShelf(5, 5, ProductType.Milk);

        SetShelf(6, 5, ProductType.Bread);
        SetShelf(4, 2, ProductType.Bread);

        SetShelf(5, 2, ProductType.Fruit);
        SetShelf(6, 2, ProductType.Fruit);

        SetShelf(8, 4, ProductType.Snacks);
        SetShelf(8, 5, ProductType.Snacks);

        Set(11, 1, CellType.Wall, false);
        Set(0, 4, CellType.Wall, false);
        Set(0, 1, CellType.Wall, false);

        for (int x= 0; x < width; x++)
        {
            Set(x, 7, CellType.Wall, false);
        }
    }

    

    public void SetShelf(int x, int y, ProductType product)
    {
        Set(x, y, CellType.Shelf, false);
        grid[x, y].product = product;
    }

    public void Set(int x, int y, CellType type, bool walkable)  //seteaza rolul unei celule
    {
        if (!Inside(x, y)) return;

        grid[x, y].type = type;
        grid[x, y].walkable = walkable;
    }

    public bool Inside(int x, int y)      //verifica daca coordonatele date sunt in interiorul grid-ului
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public GridCell Get(int x, int y)    //returneaza celula de la coordonatele date
    {
        if (!Inside(x, y)) return null;
        return grid[x, y];
    }

    public GridCell Get(Vector2Int pos)     //overload pentru a putea folosi un Vector2Int in loc de coordonate separate
    {
        return Get(pos.x, pos.y);
    }

    public bool Walkable(int x, int y)     //verifica daca celula de la coordonatele date e walkable
    {
        GridCell cell = Get(x, y);
        return cell != null && cell.walkable;
    }

    public bool FreeForAgent(GridCell cell, CustomerAgent agent)   //verifica daca un agent poate intra pe o celula
    {
        if (cell == null || !cell.walkable)
            return false;

        //daca tile-ul este ocupat de alt agent, nu putem intra
        if (cell.occupiedBy != null && cell.occupiedBy != agent)
            return false;

        //daca tile-ul este deja rezervat de alt agent, nu putem intra
        if (cell.reservedForMove != null && cell.reservedForMove != agent)
            return false;

        return true;
    }


    public GridCell Find(CellType type)        //returneaza prima celula de tipul dat
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

    public GridCell RandomWalkable()    //returneaza o celula aleatoare care e walkable
    {
        List<GridCell> cells = new List<GridCell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].walkable)
                    cells.Add(grid[x, y]);
            }
        }

        if (cells.Count == 0) return null;
        return cells[Random.Range(0, cells.Count)];
    }


    public List<GridCell> FindShelves(ProductType product)
    {
        List<GridCell> result = new List<GridCell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell c = grid[x, y];
                if (c.type == CellType.Shelf && c.product == product)
                    result.Add(c);
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
                GridCell c = grid[x, y];
                if (c.type == CellType.Shelf && c.product != ProductType.None && !result.Contains(c.product))
                    result.Add(c.product);
            }
        }

        return result;
    }


    public Vector3 World(int x, int y)       //returneaza pozitia in lumea 3D a celulei de la coordonatele date
    {
        return new Vector3(x * cellSize, y * cellSize, 0f) + offset;
    }

    public Vector3 World(Vector2Int pos)    //overload pentru a putea folosi un Vector2Int in loc de coordonate separate
    {
        return World(pos.x, pos.y);
    }

    public Vector2Int Grid(Vector3 worldPos)    //returneaza coordonatele in grid ale pozitiei date in lumea 3D
    {
        Vector3 p = worldPos - offset;
        int x = Mathf.RoundToInt(p.x / cellSize);
        int y = Mathf.RoundToInt(p.y / cellSize);
        return new Vector2Int(x, y);
    }


    private void OnDrawGizmos()      //deseneaza grid-ul in editor
    {
        if (!showGrid) return;

        if (grid == null || grid.GetLength(0) != width || grid.GetLength(1) != height)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 pos = World(x, y);

                    Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                    Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.95f);

                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(pos, Vector3.one * cellSize);
                }
            }

            return;
        }

    
    }


}