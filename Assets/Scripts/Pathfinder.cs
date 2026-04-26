using System.Collections.Generic;
using UnityEngine;

public class Pathfinder
{
    private GridManager grid;

    public Pathfinder(GridManager grid)
    {
        this.grid = grid;
    }

    public List<GridCell> FindPath(GridCell start, GridCell goal)
    {
        List<GridCell> open = new List<GridCell>();            //lista de celule care trebuie evaluate
        HashSet<GridCell> closed = new HashSet<GridCell>();    //lista de celule care au fost deja evaluate

        //pentru a putea reconstrui drumul la final, retinem de unde am venit pentru fiecare celula
        Dictionary<GridCell, GridCell> from = new Dictionary<GridCell, GridCell>(); 

        //costul de a ajunge la fiecare celula de la start
        Dictionary<GridCell, int> g = new Dictionary<GridCell, int>();

        //costul total estimat de a ajunge la tinta prin fiecare celula (g + h)
        Dictionary<GridCell, int> f = new Dictionary<GridCell, int>();  

        open.Add(start);  //adaugam celula de start in lista de open
        g[start] = 0;     //costul de a ajunge la start e 0
        f[start] = H(start, goal);   //costul total estimat de a ajunge la tinta prin start e doar h, pentru ca g e 0



        while (open.Count > 0) // cat timp mai avem celule de evaluat
        {
            GridCell current = Best(open, f);  // alegem celula cu cel mai mic cost total estimat din open

            if (current == goal)       // daca am ajuns la tinta, reconstruim drumul si il returnam
                return BuildPath(from, current);

            // mutam celula curenta din open in closed, pentru ca am evaluat-o deja
            open.Remove(current);
            closed.Add(current);

            // pentru fiecare vecin al celulei curente
            foreach (GridCell next in Neighbors(current))
            {
                if (!next.walkable || closed.Contains(next))   // daca vecinul nu e walkable sau l-am evaluat deja,
                    continue;                                  // il sarim


                // costul de a ajunge la vecin prin celula curenta e costul de a ajunge la curent + 1 (pentru ca toate mutarile au acelasi cost)
                int newG = g[current] + 1;

                // daca pentru vecin nu s-a calculat inca g sau g-ul e mai mare decat g-ul curent, actualizam
                if (!g.ContainsKey(next) || newG < g[next]) 
                {
                    from[next] = current;
                    g[next] = newG;    //actualizam cu noul cost
                    f[next] = newG + H(next, goal);    //calculam si f

                    if (!open.Contains(next))    //adaugam la open
                        open.Add(next);
                }
            }
        }

        return null;
    }

    // functia de heuristica folosita de A* pentru a estima costul de a ajunge la tinta printr-o celula data
    // //folosim distanta Manhattan pentru ca ne miscam doar pe axele x si y
    private int H(GridCell a, GridCell b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // functia care alege celula cu cel mai mic cost total estimat dintr-o lista de celule

    private GridCell Best(List<GridCell> list, Dictionary<GridCell, int> f)
    {
        GridCell best = list[0];

        foreach (GridCell cell in list)
        {
            if (f[cell] < f[best])
                best = cell;
        }

        return best;
    }


    // functia care reconstruieste drumul de la tinta la start folosind informatiile din dictionarul "from"
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

    // functia care returneaza o lista cu vecinii celulei date
    private List<GridCell> Neighbors(GridCell cell)
    {
        List<GridCell> result = new List<GridCell>();

        Add(cell.x + 1, cell.y, result);
        Add(cell.x - 1, cell.y, result);
        Add(cell.x, cell.y + 1, result);
        Add(cell.x, cell.y - 1, result);

        return result;
    }

    // functia care adauga o celula la lista de vecini daca exista in grid
    private void Add(int x, int y, List<GridCell> list)
    {
        GridCell cell = grid.Get(x, y);
        if (cell != null)
            list.Add(cell);
    }
}