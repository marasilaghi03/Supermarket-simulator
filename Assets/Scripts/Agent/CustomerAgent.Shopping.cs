
using System.Collections.Generic;
using UnityEngine;

public partial class CustomerAgent
{
    private void BuildShoppingList()
    {
        List<ProductType> products = grid.GetProducts();

        if (products.Count == 0)
            return;

        int count = Random.Range(minItems, maxItems + 1);

        for (int i = 0; i < count; i++)
            shoppingList.Add(products[Random.Range(0, products.Count)]);
    }

    private bool HasFinishedShopping()
    {
        return shoppingList.Count == 0;
    }

    private void SkipCurrentItem()
    {
        itemsSkipped++;
        if (currentItemIndex >= 0 && currentItemIndex < shoppingList.Count)
            shoppingList.RemoveAt(currentItemIndex);

        if (currentItemIndex >= shoppingList.Count)
            currentItemIndex = 0;

        shelfWaitTimer = 0f;
    }

    private List<ShelfOption> GetShelfOptions(ProductType product, bool lenient = false)
    {
        List<ShelfOption> options = new List<ShelfOption>();
        List<GridCell> shelves = grid.FindShelves(product);

        if (shelves.Count == 0)
            return options;

        GridCell start = grid.Get(currentPos);

        foreach (GridCell shelf in shelves)
        {
            if (shelf.reservedBy != null && shelf.reservedBy != this)
                continue;

            GridCell spot = lenient ? BestNearLenient(shelf) : FreeNear(shelf);

            if (spot == null)
                continue;

            List<GridCell> testPath = pathfinder.FindPath(start, spot, this);

            if (testPath == null || testPath.Count == 0)
                continue;

            options.Add(new ShelfOption
            {
                Shelf = shelf,
                Spot = spot,
                Path = testPath,
                Distance = testPath.Count
            });
        }

        return options;
    }

    private GridCell BestNearLenient(GridCell cell)
    {
        List<GridCell> candidates = new List<GridCell>();

        TryAddLenient(cell.x + 1, cell.y, candidates);
        TryAddLenient(cell.x - 1, cell.y, candidates);
        TryAddLenient(cell.x, cell.y + 1, candidates);
        TryAddLenient(cell.x, cell.y - 1, candidates);

        GridCell best = null;
        int bestDist = int.MaxValue;

        foreach (GridCell option in candidates)
        {
            int dist =
                Mathf.Abs(option.x - currentPos.x) +
                Mathf.Abs(option.y - currentPos.y);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = option;
            }
        }

        return best;
    }

    private void TryAddLenient(int x, int y, List<GridCell> list)
    {
        GridCell cell = grid.Get(x, y);

        if (cell == null || !cell.walkable)
            return;

        if (!CanUseQueueCell(cell, cell))
            return;

        if (cell.occupiedBy == null || cell.occupiedBy == this)
        {
            list.Add(cell);
            return;
        }

        if (cell.occupiedBy.IsLeavingCurrentTile())
            list.Add(cell);
    }
    private ShelfOption GetClosestOption(List<ShelfOption> options, bool onlyNear)
    {
        ShelfOption best = null;
        int bestDistance = int.MaxValue;

        foreach (ShelfOption option in options)
        {
            if (onlyNear && option.Distance > nearShelfDistance)
                continue;

            if (option.Distance < bestDistance)
            {
                bestDistance = option.Distance;
                best = option;
            }
        }

        return best;
    }

    private int FindAvailableItemIndex()
    {
        for (int i = 0; i < shoppingList.Count; i++)
        {
            if (i == currentItemIndex)
                continue;

            List<ShelfOption> options = GetShelfOptions(shoppingList[i], lenient: true);

            if (options.Count > 0)
                return i;
        }

        return -1;
    }

    private void ReleaseShelf()
    {
        if (currentShelf != null && currentShelf.reservedBy == this)
            currentShelf.reservedBy = null;

        currentShelf = null;
    }
}
