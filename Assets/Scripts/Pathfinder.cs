using System.Collections.Generic;
using UnityEngine;

public class Pathfinder
{
    private GridManager grid;

    public Pathfinder(GridManager grid)
    {
        this.grid = grid;
    }

    public List<GridCell> FindPath(GridCell start, GridCell goal, CustomerAgent agent = null)
    {
        if (start == null || goal == null)
            return null;

        List<GridCell> open = new List<GridCell>();
        HashSet<GridCell> closed = new HashSet<GridCell>();
        Dictionary<GridCell, GridCell> from = new Dictionary<GridCell, GridCell>();
        Dictionary<GridCell, int> g = new Dictionary<GridCell, int>();
        Dictionary<GridCell, int> f = new Dictionary<GridCell, int>();

        open.Add(start);
        g[start] = 0;
        f[start] = H(start, goal);

        while (open.Count > 0)
        {
            GridCell current = Best(open, f);

            if (current == goal)
                return BuildPath(from, current);

            open.Remove(current);
            closed.Add(current);

            foreach (GridCell next in Neighbors(current))
            {
                if (closed.Contains(next))
                    continue;

                if (!CanUse(next, goal, agent))
                    continue;

                int newG = g[current] + 1;

                if (!g.ContainsKey(next) || newG < g[next])
                {
                    from[next] = current;
                    g[next] = newG;
                    f[next] = newG + H(next, goal);

                    if (!open.Contains(next))
                        open.Add(next);
                }
            }
        }

        return null;
    }

    private bool CanUse(GridCell cell, GridCell goal, CustomerAgent agent)
    {
        if (cell == null || !cell.walkable)
            return false;

        if (agent != null && !agent.CanUseQueueCell(cell, goal))
            return false;

        if (agent == null)
            return true;

        // A cell is passable if whoever occupies it is in the process of leaving.
        // This applies both to intermediate steps and the goal itself so that
        // customers moving forward don't deadlock behind slow-walkers.
        if (cell.occupiedBy != null && cell.occupiedBy != agent)
        {
            if (cell.occupiedBy.IsLeavingCurrentTile())
                return true;

            return false;
        }

        // Allow passing through a move-reservation only if the reserving agent
        // is actively leaving that cell (their reservation is about to clear).
        if (cell.reservedForMove != null && cell.reservedForMove != agent)
        {
            if (cell.reservedForMove.IsLeavingCurrentTile())
                return true;

            return false;
        }

        return true;
    }

    private int H(GridCell a, GridCell b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private GridCell Best(List<GridCell> list, Dictionary<GridCell, int> f)
    {
        GridCell best = list[0];

        foreach (GridCell cell in list)
        {
            if (f.TryGetValue(cell, out int cellF) && f.TryGetValue(best, out int bestF) && cellF < bestF)
                best = cell;
        }

        return best;
    }

    private List<GridCell> BuildPath(Dictionary<GridCell, GridCell> from, GridCell current)
    {
        List<GridCell> path = new List<GridCell>();
        path.Add(current);

        while (from.ContainsKey(current))
        {
            current = from[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private List<GridCell> Neighbors(GridCell cell)
    {
        List<GridCell> result = new List<GridCell>();
        Add(cell.x + 1, cell.y, result);
        Add(cell.x - 1, cell.y, result);
        Add(cell.x, cell.y + 1, result);
        Add(cell.x, cell.y - 1, result);
        return result;
    }

    private void Add(int x, int y, List<GridCell> list)
    {
        GridCell cell = grid.Get(x, y);
        if (cell != null)
            list.Add(cell);
    }
}