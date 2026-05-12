using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CustomerAgent : MonoBehaviour
{
    //starea in care e agentul
    private enum State
    {
        ToShelf,       //agentul merge spre un raft pentru a lua un produs
        WaitingShelf,  //agentul asteapta ca raftul sa fie liber
        Picking,       //agentul ia produsul de pe raft
        ToQueue,       //agentul merge spre coada
        WaitingQueue,  //agentul asteapta la coada
        Paying,        //agentul plateste la casa
        ToExit,        //agentul merge spre iesire
        Done           //agentul a terminat cumparaturile
    }

    //decizia aleasa de arborele de decizie
    private enum Decision
    {
        GoToShelf,     //agentul a ales un raft spre care sa mearga
        WaitForShelf,  //agentul asteapta pentru ca nu exista momentan un raft disponibil
        TryOtherItem,  //agentul incearca alt produs din lista
        SkipItem,      //agentul renunta la produsul curent
        GoToQueue      //agentul a terminat cumparaturile si merge la coada
    }

    [Header("References")]
    [SerializeField] private GridManager grid;       //grid-ul din scena
    [SerializeField] private CheckoutQueue queue;    //coada de la casa

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;        //viteza de miscare a agentului
    [SerializeField] private float maxWaitForTile = 1f;   //cat asteapta agentul cand un tile e blocat

    [Header("Interaction")]
    [SerializeField] private float pickTime = 1f;    //timpul petrecut la raft
    [SerializeField] private float payTime = 1f;     //timpul petrecut la casa

    [Header("Shopping List")]
    [SerializeField] private int minItems = 2;       //numarul minim de produse din lista
    [SerializeField] private int maxItems = 3;       //numarul maxim de produse din lista

    [Header("Decision Tree")]
    [SerializeField] private int nearShelfDistance = 6;       //distanta maxima pentru ca un raft sa fie considerat apropiat
    [SerializeField] private float rethinkTime = 0.5f;        //cat asteapta agentul inainte sa ia din nou o decizie
    [SerializeField] private float maxShelfWaitTime = 2f;     //dupa cat timp agentul incearca alt produs

    //pentru A*
    private Pathfinder pathfinder;      //calculeaza drumul
    private List<GridCell> path;        //lista de celule pe care agentul trebuie sa mearga

    private Vector2Int currentPos;      //unde e agentul in grid
    private Vector2Int targetPos;       //unde vrea sa ajunga

    //puncte de interes
    private GridCell currentShelf;      //raftul spre care merge agentul momentan
    private GridCell exitCell;          //iesirea din magazin

    //lista de cumparaturi a agentului
    private List<ProductType> shoppingList = new List<ProductType>();
    private int currentItemIndex = 0;

    private State state;
    private Decision lastDecision;

    private string note = "";     //motivul pentru care arborele a ales decizia curenta
    private float shelfWaitTimer = 0f;      //cat timp a asteptat agentul pentru un raft

    private bool hovered = false;       //daca mouse-ul este peste agent
    private bool moving = false;        //daca agentul este in miscare

    private bool leavingCurrentTile = false;    //daca agentul a terminat interactiunea si pleaca de pe tile-ul curent

    private Coroutine moveRoutine;      //coroutine-ul de miscare curent
    private Coroutine rethinkRoutine;   //coroutine-ul prin care agentul se razgandeste

    //pentru animatii
    private Animator animator;
    private string currentAnim = "";

    //optiune posibila pentru un raft
    private class ShelfOption
    {
        public GridCell Shelf;       //raftul ales
        public GridCell Spot;        //celula libera de langa raft
        public List<GridCell> Path;  //drumul pana la celula de langa raft
        public int Distance;         //lungimea drumului pana la raft
    }

    private void Start()
    {
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>();

        if (queue == null)
            queue = FindAnyObjectByType<CheckoutQueue>();

        animator = GetComponent<Animator>();

        pathfinder = new Pathfinder(grid);

        GridCell startCell = grid.Find(CellType.Entrance);
        exitCell = grid.Find(CellType.Exit);

        //pornim agentul de la intrare
        currentPos = startCell.Pos;
        transform.position = grid.World(currentPos);

        //marcam tile-ul de start ca ocupat de acest agent
        startCell.occupiedBy = this;

        //generam lista de cumparaturi
        BuildShoppingList();

        //incepem sa cautam primul produs
        GoNextItem();
    }

    private void Update()
    {
        if (hovered)
            CustomerInfoUI.Instance?.Show(this);

        //daca agentul este la coada si nu se misca, isi verifica din nou pozitia
        if (state == State.WaitingQueue && !moving)
        {
            ThinkQueue();
        }
    }


    // LISTA DE CUMPARATURI

    private void BuildShoppingList()
    {
        List<ProductType> products = grid.GetProducts();
        int count = Random.Range(minItems, maxItems + 1);

        for (int i = 0; i < count; i++)
        {
            shoppingList.Add(products[Random.Range(0, products.Count)]);  //alegem produse random
        }
    }


    // DECISION TREE

    private void GoNextItem()
    {
        ProductType wanted;
        GridCell chosenShelf;

        lastDecision = DecideNextAction(out wanted, out chosenShelf);

        ExecuteDecision(lastDecision, chosenShelf);
    }

    private void ExecuteDecision(Decision decision, GridCell chosenShelf)   //executa decizia aleasa de arbore
    {
        switch (decision)
        {
            case Decision.GoToQueue:
                state = State.ToQueue;
                queue.Join(this);
                GoTo(queue.GetSpot(this));
                break;

            case Decision.WaitForShelf:
                StartRethink();
                break;

            case Decision.TryOtherItem:
                GoNextItem();
                break;

            case Decision.SkipItem:
                SkipCurrentItem();
                GoNextItem();
                break;

            case Decision.GoToShelf:
                currentShelf = chosenShelf;
                currentShelf.reservedBy = this;

                state = State.ToShelf;
                GoTo(FreeNear(currentShelf));
                break;
        }
    }

    private Decision DecideNextAction(out ProductType wanted, out GridCell chosenShelf)   //arborele de decizie al agentului
    {
        wanted = ProductType.None; //produsul dorit
        chosenShelf = null;        //raftul ales pentru a lua produsul dorit

        //daca agentul nu mai are produse de luat, merge la coada
        if (HasFinishedShopping())
        {
            note = "Shopping finished";
            shelfWaitTimer = 0f;
            return Decision.GoToQueue;
        }

        //ne asiguram ca indexul este valid
        if (currentItemIndex < 0 || currentItemIndex >= shoppingList.Count)
            currentItemIndex = 0;

        wanted = shoppingList[currentItemIndex];

        //daca nu exista niciun raft cu produsul dorit, agentul renunta la produs
        if (grid.FindShelves(wanted).Count == 0)
        {
            note = "product not in shop";
            return Decision.SkipItem;
        }

        //cautam toate optiunile valide pentru produsul curent
        List<ShelfOption> options = GetShelfOptions(wanted);

        //daca nu exista raft disponibil pentru produsul curent
        if (options.Count == 0)
        {
            //daca agentul a asteptat prea mult, incearca alt produs din lista
            if (shelfWaitTimer >= maxShelfWaitTime)
            {
                int otherItemIndex = FindAvailableItemIndex();

                if (otherItemIndex != -1)
                {
                    currentItemIndex = otherItemIndex;
                    shelfWaitTimer = 0f;

                    note = "current product not available";
                    return Decision.TryOtherItem;
                }
            }

            note = "no available shelf";
            return Decision.WaitForShelf;
        }

        //incercam sa alegem un raft apropiat
        ShelfOption nearOption = GetClosestOption(options, true);

        if (nearOption != null)
        {
            chosenShelf = nearOption.Shelf;
            shelfWaitTimer = 0f;

            note = "there is a shelf near";
            return Decision.GoToShelf;
        }

        //daca nu exista raft apropiat, alegem cel mai bun raft accesibil
        ShelfOption bestOption = GetClosestOption(options, false);

        if (bestOption != null)
        {
            chosenShelf = bestOption.Shelf;
            shelfWaitTimer = 0f;

            note = "no nearby shelf";
            return Decision.GoToShelf;
        }

        note = "no available options";
        return Decision.WaitForShelf;
    }

    private void SkipCurrentItem()   //sterge produsul curent din lista pentru ca nu poate fi cumparat
    {
        if (currentItemIndex >= 0 && currentItemIndex < shoppingList.Count)
            shoppingList.RemoveAt(currentItemIndex);

        if (currentItemIndex >= shoppingList.Count)
            currentItemIndex = 0;

        shelfWaitTimer = 0f;
    }

    private bool HasFinishedShopping()   //verifica daca agentul a terminat lista de cumparaturi
    {
        return shoppingList.Count == 0;
    }

    private List<ShelfOption> GetShelfOptions(ProductType product)   //cauta rafturile disponibile si accesibile pentru produs
    {
        List<ShelfOption> options = new List<ShelfOption>();

        //cautam rafturile care contin produsul dorit
        List<GridCell> shelves = grid.FindShelves(product);

        if (shelves.Count == 0)
            return options;

        GridCell start = grid.Get(currentPos);

        foreach (GridCell shelf in shelves)
        {
            //daca raftul este rezervat de alt agent, il ignoram
            if (shelf.reservedBy != null && shelf.reservedBy != this)
                continue;

            //cautam o celula libera langa raft
            GridCell spot = FreeNear(shelf);

            if (spot == null)
                continue;

            //verificam daca exista drum pana la celula respectiva
            List<GridCell> testPath = pathfinder.FindPath(start, spot, this);

            if (testPath == null || testPath.Count == 0)
                continue;

            ShelfOption option = new ShelfOption();
            option.Shelf = shelf;
            option.Spot = spot;
            option.Path = testPath;
            option.Distance = testPath.Count;

            options.Add(option);
        }

        return options;
    }

    private ShelfOption GetClosestOption(List<ShelfOption> options, bool onlyNear)   //alege cea mai buna optiune de raft
    {
        ShelfOption best = null;
        int bestDistance = int.MaxValue;

        foreach (ShelfOption option in options)
        {
            //daca vrem doar rafturi apropiate, ignoram optiunile prea indepartate
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

    private int FindAvailableItemIndex()   //cauta alt produs din lista care poate fi luat acum
    {
        for (int i = 0; i < shoppingList.Count; i++)
        {
            //nu verificam produsul curent, pentru ca deja stim ca nu este disponibil
            if (i == currentItemIndex)
                continue;

            ProductType product = shoppingList[i];

            List<ShelfOption> options = GetShelfOptions(product);

            if (options.Count > 0)
                return i;
        }

        return -1;
    }

    private void StartRethink()   //agentul asteapta putin si apoi ia din nou decizia
    {
        state = State.WaitingShelf;

        if (rethinkRoutine != null)
            StopCoroutine(rethinkRoutine);

        rethinkRoutine = StartCoroutine(Rethink());
    }

    private IEnumerator Rethink()   //asteapta si ruleaza din nou arborele de decizie
    {
        yield return new WaitForSeconds(rethinkTime);

        shelfWaitTimer += rethinkTime;

        rethinkRoutine = null;
        GoNextItem();
    }


    // COADA

    private void ThinkQueue()   //agentul isi re-verifica pozitia in coada
    {
        GridCell spot = queue.GetSpot(this);

        if (spot == null)
            return;

        //daca pozitia lui in coada s-a schimbat, merge la noul loc
        if (spot.Pos != currentPos)
        {
            GoTo(spot);
            return;
        }

        //daca este primul in coada si este pe locul corect, poate plati
        if (queue.IsFirst(this))
        {
            StartCoroutine(Pay());
        }
    }

    private IEnumerator RetryQueue()   //asteapta putin si incearca din nou sa avanseze in coada
    {
        yield return new WaitForSeconds(rethinkTime);
        ThinkQueue();
    }


    // PATHFINDING
    

    private GridCell FreeNear(GridCell cell)    //gaseste o celula libera in jurul celei date
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

    private void StopMoveRoutine()   //opreste miscarea curenta si pune agentul inapoi pe tile-ul logic
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        moving = false;
        path = null;

        GridCell currentCell = grid.Get(currentPos);

        if (currentCell != null)
        {
            transform.position = grid.World(currentPos);

            if (currentCell.occupiedBy == null)
                currentCell.occupiedBy = this;
        }

        leavingCurrentTile = false;
        SetIdleFromLastDirection();
    }

    private bool CanUseAsDestination(GridCell cell)   //verifica daca o celula poate fi aleasa ca destinatie
    {
        if (cell == null || !cell.walkable)
            return false;

        //daca celula este libera pentru agent, o putem folosi
        if (grid.FreeForAgent(cell, this))
            return true;

        //daca celula este ocupata de un agent care pleaca de acolo, o permitem ca destinatie
        //agentul care vine va astepta in Follow pana cand celula devine libera
        if (cell.occupiedBy != null && cell.occupiedBy != this && cell.occupiedBy.IsLeavingCurrentTile())
            return true;

        return false;
    }

    private void GoTo(GridCell cell)    //seteaza o noua tinta pentru agent si calculeaza drumul pana acolo
    {
        if (cell == null)
        {
            HandleFailedPath();
            return;
        }

        targetPos = cell.Pos;

        //gaseste celulele de start si de final pentru A*
        GridCell start = grid.Get(currentPos);
        GridCell goal = grid.Get(targetPos);

        //calculeaza drumul folosind A*
        List<GridCell> newPath = pathfinder.FindPath(start, goal, this);

        if (newPath == null || newPath.Count == 0)
        {
            HandleFailedPath();
            return;
        }

        //opreste miscarea curenta corect si porneste miscarea noua
        if (moveRoutine != null)
            StopMoveRoutine();

        path = newPath;
        moveRoutine = StartCoroutine(Follow(newPath));
    }

    private IEnumerator Follow(List<GridCell> pathToFollow)   //parcurge drumul calculat de A*
    {
        if (pathToFollow == null || pathToFollow.Count == 0)
        {
            moving = false;
            moveRoutine = null;
            yield break;
        }

        moving = true;

        for (int i = 0; i < pathToFollow.Count; i++)
        {
            GridCell nextCell = pathToFollow[i];

            if (nextCell == null)
                continue;

            //sarim peste celula curenta, pentru ca agentul este deja acolo
            if (nextCell.Pos == currentPos)
                continue;

            float waitTime = 0f;

            //daca urmatorul tile este ocupat, agentul asteapta
            while (!grid.FreeForAgent(nextCell, this))
            {
                waitTime += Time.deltaTime;

                //daca asteapta prea mult, inseamna ca traseul este blocat
                if (waitTime >= maxWaitForTile)
                {
                    moving = false;
                    moveRoutine = null;
                    HandleBlockedPath();
                    yield break;
                }

                yield return null;
            }

            //rezervam tile-ul, ca sa nu intre alt agent in el in acelasi timp
            nextCell.reservedForMove = this;

            GridCell oldCell = grid.Get(currentPos);

            yield return Move(grid.World(nextCell.Pos)); //misca agentul pana acolo

            //eliberam tile-ul vechi
            if (oldCell != null && oldCell.occupiedBy == this)
                oldCell.occupiedBy = null;

            //dupa ce agentul a plecat de pe tile-ul vechi, nu mai trebuie marcat ca pleaca de acolo
            leavingCurrentTile = false;

            //ocupam noul tile
            nextCell.reservedForMove = null;
            nextCell.occupiedBy = this;

            currentPos = nextCell.Pos;
        }

        moving = false;
        moveRoutine = null;
        path = null;

        Arrive();
    }

    private IEnumerator Move(Vector3 target)   //misca agentul spre tinta pana ajunge acolo
    {
        Vector3 direction = (target - transform.position).normalized;

        PlayWalkAnimation(direction);

        while (Vector3.Distance(transform.position, target) > 0.01f)  //cat timp agentul nu e aproape de tinta, continua sa se miste spre ea
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;

        SetIdleFromLastDirection();
    }

    private void HandleBlockedPath()   //se apeleaza cand agentul este blocat pe drum
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        moving = false;
        path = null;

        GridCell currentCell = grid.Get(currentPos);

        if (currentCell != null)
        {
            transform.position = grid.World(currentPos);

            if (currentCell.occupiedBy == null)
                currentCell.occupiedBy = this;
        }

        leavingCurrentTile = false;
        SetIdleFromLastDirection();

        if (state == State.ToShelf)
        {
            ReleaseShelf();
            StartRethink();
            return;
        }

        if (state == State.ToQueue || state == State.WaitingQueue)
        {
            state = State.WaitingQueue;
            GoTo(queue.GetSpot(this));
            return;
        }

        if (state == State.ToExit)
        {
            GoTo(exitCell);
        }
    }

    private void HandleFailedPath()   //se apeleaza cand nu exista drum valid
    {
        if (state == State.ToShelf)
        {
            ReleaseShelf();
            StartRethink();
            return;
        }

        if (state == State.ToQueue || state == State.WaitingQueue)
        {
            state = State.WaitingQueue;
            StartCoroutine(RetryQueue());
            return;
        }

        if (state == State.ToExit)
        {
            StartCoroutine(RetryExit());
        }
    }

    private IEnumerator RetryExit()   //asteapta putin si incearca din nou sa iasa
    {
        yield return new WaitForSeconds(rethinkTime);
        GoTo(exitCell);
    }


    // INTERACTIUNI

    private void Arrive()    //ajunge la tinta si decide ce sa faca in continuare
    {
        switch (state)
        {
            case State.ToShelf:
                StartCoroutine(Pick());
                break;

            case State.ToQueue:
                state = State.WaitingQueue;
                ThinkQueue();
                break;

            case State.ToExit:
                state = State.Done;
                ClearCurrentTile();
                Destroy(gameObject);
                break;
        }
    }

    private IEnumerator Pick()   //simuleaza timpul petrecut de agent la raft
    {
        state = State.Picking;

        //agentul se intoarce spre raft in timp ce ia produsul
        FaceCell(currentShelf);

        yield return new WaitForSeconds(pickTime);

        //eliberam raftul dupa ce produsul a fost luat
        ReleaseShelf();

        //agentul a terminat la raft si urmeaza sa plece de pe tile-ul curent
        leavingCurrentTile = true;

        //stergem produsul cumparat din lista
        if (currentItemIndex >= 0 && currentItemIndex < shoppingList.Count)
            shoppingList.RemoveAt(currentItemIndex);

        //daca indexul a ramas in afara listei, il mutam la inceput
        if (currentItemIndex >= shoppingList.Count)
            currentItemIndex = 0;

        shelfWaitTimer = 0f;

        yield return new WaitForSeconds(0.2f);

        GoNextItem();
    }

    private IEnumerator Pay()   //simuleaza timpul petrecut de agent la casa
    {
        if (state == State.Paying)
            yield break;

        state = State.Paying;

        //agentul se intoarce spre casa de marcat in timp ce plateste
        FaceCell(queue.GetCheckoutCell());

        yield return new WaitForSeconds(payTime);

        //agentul pleaca din coada logic imediat dupa ce a platit
        queue.Leave(this);

        //agentul a terminat la coada si urmeaza sa plece de pe tile-ul curent
        leavingCurrentTile = true;

        state = State.ToExit;

        yield return new WaitForSeconds(0.2f);

        GoTo(exitCell);
    }


    // OCUPARE / ELIBERARE TILE-URI

    private void ReleaseShelf()   //elibereaza raftul rezervat de agent
    {
        if (currentShelf != null && currentShelf.reservedBy == this)
            currentShelf.reservedBy = null;

        currentShelf = null;
    }

    private void ClearCurrentTile()   //elibereaza tile-ul pe care sta agentul
    {
        GridCell cell = grid.Get(currentPos);

        if (cell != null && cell.occupiedBy == this)
            cell.occupiedBy = null;
    }

    private void TryAdd(int x, int y, List<GridCell> list)   //daca celula de la coordonatele date e libera, o adauga in lista data
    {
        GridCell cell = grid.Get(x, y);

        if (CanUseAsDestination(cell))
            list.Add(cell);
    }

    public bool IsLeavingCurrentTile()   //verifica daca agentul urmeaza sa elibereze tile-ul pe care sta
    {
        return leavingCurrentTile;
    }


    // ANIMATII

    private void PlayAnim(string animName)   //porneste o animatie daca nu ruleaza deja
    {
        if (animator == null || currentAnim == animName)
            return;

        animator.Play(animName);
        currentAnim = animName;
    }

    private void PlayWalkAnimation(Vector3 direction)   //alege animatia de mers in functie de directie
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (direction.x > 0)
                PlayAnim("walk_right");
            else
                PlayAnim("walk_left");
        }
        else
        {
            if (direction.y > 0)
                PlayAnim("walk_back");
            else
                PlayAnim("walk_front");
        }
    }

    private void SetIdleFromLastDirection()   //seteaza idle-ul corespunzator ultimei directii de mers
    {
        if (currentAnim == "walk_right")
            PlayAnim("idle_right");
        else if (currentAnim == "walk_left")
            PlayAnim("idle_left");
        else if (currentAnim == "walk_back")
            PlayAnim("idle_back");
        else if (currentAnim == "walk_front")
            PlayAnim("idle_front");
    }

    private void FaceCell(GridCell cell)   //intoarce personajul spre o celula
    {
        if (cell == null)
            return;

        Vector2Int dir = cell.Pos - currentPos;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x > 0)
                PlayAnim("idle_right");
            else
                PlayAnim("idle_left");
        }
        else
        {
            if (dir.y > 0)
                PlayAnim("idle_back");
            else
                PlayAnim("idle_front");
        }
    }


    // UI 

    public string GetInfoText()   //returneaza textul afisat in UI cand dam hover pe agent
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"State: {state}");
        sb.AppendLine(" ");
        sb.AppendLine($"Decision: {lastDecision}");
        sb.AppendLine(" ");
        sb.AppendLine($"Note: {note}");
        sb.AppendLine(" ");
        sb.AppendLine("Shopping list:");

        for (int i = 0; i < shoppingList.Count; i++)
        {
            string marker = i == currentItemIndex ? "> " : "  ";
            sb.AppendLine(marker + shoppingList[i]);
        }

        return sb.ToString();
    }

    private void OnMouseEnter()
    {
        hovered = true;
        CustomerInfoUI.Instance?.Show(this);
    }

    private void OnMouseExit()
    {
        hovered = false;
        CustomerInfoUI.Instance?.Hide();
    }

    private void OnDestroy()   //cand agentul este sters, eliberam ce ocupa/rezerva
    {
        ClearCurrentTile();
        ReleaseShelf();

        if (queue != null)
            queue.Leave(this);
    }

    private void OnDrawGizmos()   //pentru debugging
    {
        if (!hovered || grid == null || path == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(grid.World(targetPos), 0.2f);
        Gizmos.DrawLine(transform.position, grid.World(targetPos));

        Gizmos.color = Color.yellow;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = grid.World(path[i].Pos);
            Vector3 b = grid.World(path[i + 1].Pos);
            Gizmos.DrawLine(a, b);
        }
    }
}