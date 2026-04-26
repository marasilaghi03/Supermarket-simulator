using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerAgent : MonoBehaviour
{
    //starea in care se afla agentul
    private enum State
    {
        ToShelf,
        Picking,
        ToCheckout,
        Paying,
        ToExit,
        Done
    }

    [SerializeField] private GridManager grid;    //grid-ul din scena
    [SerializeField] private float moveSpeed = 3f;   //viteza de miscare a agentului
    [SerializeField] private float pickTime = 1f;
    [SerializeField] private float payTime = 1f;

    //pentru A*
    private Pathfinder pathfinder;  //calculeaza drumul
    private List<GridCell> path;    //lista de celule pe care agentul trebuie sa mearga

    private Vector2Int currentPos;  //unde e agentul in grid
    private Vector2Int targetPos;   //unde vrea sa ajunga

    //puncte de interes
    private GridCell shelfCell;
    private GridCell checkoutCell;
    private GridCell exitCell;

    private State state;

    private void Start()
    {
        //pregatim harta si pathfinder-ul
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>();

        pathfinder = new Pathfinder(grid);


        //gasim toate punctele de interes
        GridCell startCell = grid.Find(CellType.Entrance);
        shelfCell = RandomShelf();
        checkoutCell = grid.Find(CellType.Checkout);
        exitCell = grid.Find(CellType.Exit);


        //pornim agentul de la intrare
        currentPos = startCell.Pos;
        transform.position = grid.World(currentPos);

        //incepem sa mergem spre raft
        state = State.ToShelf;
        GoTo(FreeNear(shelfCell));
    }

    private GridCell RandomShelf()     //alege un raft random din magazin
    {
        List<GridCell> shelves = new List<GridCell>();

        for (int x = 0; x < 12; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                GridCell c = grid.Get(x, y);
                if (c != null && c.type == CellType.Shelf)
                    shelves.Add(c);
            }
        }

        if (shelves.Count == 0) return null;

        return shelves[Random.Range(0, shelves.Count)];
    }


    private GridCell FreeNear(GridCell cell)    //gaseste o celula walkable in jurul celei date, pentru a evita sa ne blocam in raft
    {
        List<GridCell> options = new List<GridCell>();

        TryAdd(cell.x + 1, cell.y, options);
        TryAdd(cell.x - 1, cell.y, options);
        TryAdd(cell.x, cell.y + 1, options);
        TryAdd(cell.x, cell.y - 1, options);

        GridCell best = null;
        int bestDist = int.MaxValue;

        foreach (GridCell option in options)
        {
            int dist = Mathf.Abs(option.x - currentPos.x) + Mathf.Abs(option.y - currentPos.y);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = option;
            }
        }

        return best;
    }



    private void GoTo(GridCell cell)    //seteaza o noua tinta pentru agent si calculeaza drumul pana acolo
    {

        targetPos = cell.Pos;

        //gaseste celulele de start si de final pentru A*
        GridCell start = grid.Get(currentPos);
        GridCell goal = grid.Get(targetPos);

        //calculeaza drumul folosind A*
        path = pathfinder.FindPath(start, goal);

        //opreste ce face agentul in momentul asta si incepe sa mearga pe noul drum
        StopAllCoroutines();
        StartCoroutine(Follow());
    }




    private IEnumerator Follow()    //parcurge drumul calculat de A*
    {
        foreach (GridCell cell in path)   //pentru fiecare celula din drum
        {
            currentPos = cell.Pos;
            yield return Move(grid.World(cell.Pos)); // misca agentul pana acolo
        }

        Arrive();
    }

    private IEnumerator Move(Vector3 target)   //misca agentul spre tinta pana ajunge acolo
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)  //cat timp agentul nu e aproape de tinta, continua sa se miste spre ea
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
    }

    private void Arrive()    //ajunge la tinta si decide ce sa faca in continuare, in functie de starea in care se afla
    {
        switch (state)
        {
            case State.ToShelf:
                StartCoroutine(Pick());
                break;

            case State.ToCheckout:
                StartCoroutine(Pay());
                break;

            case State.ToExit:
                state = State.Done;
                Debug.Log("Customer finished shopping.");
                break;
        }
    }

    private IEnumerator Pick()   //simuleaza timpul petrecut de agent la raft
    {
        state = State.Picking;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        yield return new WaitForSeconds(pickTime);
        sr.color = Color.green;

        state = State.ToCheckout;
        GoTo(checkoutCell);
    }

    private IEnumerator Pay()   //simuleaza timpul petrecut de agent la casa
    {
        state = State.Paying;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        yield return new WaitForSeconds(payTime);
        sr.color = Color.blue;

        state = State.ToExit;
        GoTo(exitCell);
    }


    private void TryAdd(int x, int y, List<GridCell> list)   //daca celula de la coordonatele date e walkable, o adauga in lista data
    {
        GridCell cell = grid.Get(x, y);

        if (cell != null && cell.walkable)
            list.Add(cell);
    }

    private void OnDrawGizmos()   //deseneaza tinta si drumul agentului in editor, pentru debugging
    {

        if (grid == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(grid.World(targetPos), 0.2f);
        Gizmos.DrawLine(transform.position, grid.World(targetPos));

        if (path == null) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < path.Count - 1; i++)  //pentru fiecare pereche de celule consecutive din drum, deseneaza o linie intre ele
        {
            Vector3 a = grid.World(path[i].Pos);
            Vector3 b = grid.World(path[i + 1].Pos);
            Gizmos.DrawLine(a, b);
        }
    }
}