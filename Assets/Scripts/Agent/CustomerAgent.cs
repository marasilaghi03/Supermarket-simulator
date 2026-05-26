using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class CustomerAgent : MonoBehaviour
{
    private enum State
    {
        ToShelf,
        WaitingShelf,
        Picking,
        JoiningQueue,
        ToQueue,
        WaitingQueue,
        Paying,
        ToExit,
        Done
    }

    private enum Decision
    {
        GoToShelf,
        WaitForShelf,
        TryOtherItem,
        SkipItem,
        GoToQueue
    }

    private enum Condition
    {
        FinishedShopping,
        ProductMissing,
        HasShelfOptions,
        HasNearShelf,
        HasClosestShelf,
        WaitedTooLong,
        HasOtherAvailableItem
    }

    private enum DecisionSystem
    {
        GeneratedDecisionTree,
        UtilityAI
    }

    [Header("AI System")]
    [SerializeField] private DecisionSystem decisionSystem = DecisionSystem.GeneratedDecisionTree;

    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private CheckoutQueue queue;

    private float moveSpeed = 2f;
    private float maxWaitForTile = 1f;

    private float pickTime = 1f;
    private float payTime = 0.5f;

    [Header("Shopping List")]
    [SerializeField] private int minItems = 2;
    [SerializeField] private int maxItems = 3;

    private int nearShelfDistance = 8;
    private float rethinkTime = 0.5f;
    private float maxShelfWaitTime = 3.5f;

    private float maxStuckTime = 3f;

    private Pathfinder pathfinder;
    private List<GridCell> path;

    private Vector2Int currentPos;
    private Vector2Int targetPos;

    private GridCell currentShelf;
    private GridCell exitCell;

    private float itemSwitchCooldown = 0f;

    private float queueRepathTimer = 0f;

    private float queueRepathInterval = 0.2f;

    private readonly List<ProductType> shoppingList = new List<ProductType>();
    private int currentItemIndex = 0;
    private int shelfBlockedAttempts = 0;
    private State state;
    private Decision lastDecision;

    private string note = "";
    private float shelfWaitTimer = 0f;
    private float queueStuckTimer = 0f;

    private bool hovered = false;
    private bool moving = false;
    private bool leavingCurrentTile = false;

    private Coroutine moveRoutine;
    private Coroutine rethinkRoutine;
    private Coroutine retryQueueRoutine;
    private Coroutine retryExitRoutine;
    private Coroutine pickRoutine;
    private Coroutine payRoutine;

    private Animator animator;
    private string currentAnim = "";

    private List<DecisionTreeNode> generatedDecisionTree;

    private float spawnTime;
    private float totalShelfWaitTime = 0f;
    private float totalQueueWaitTime = 0f;

    private int itemsBought = 0;
    private int itemsSkipped = 0;
    private int itemSwitches = 0;

    private float stuckTimer = 0f;
    private Vector2Int lastStuckCheckPos;

    private class ShelfOption
    {
        public GridCell Shelf;
        public GridCell Spot;
        public List<GridCell> Path;
        public int Distance;
    }

    //initializeaza agentul si porneste prima decizie
    private void Start()
    {
        spawnTime = Time.time;

        SetupReferences();
        SetupStartingPosition();

        lastStuckCheckPos = currentPos;

        GenerateDecisionTree();
        BuildShoppingList();
        GoNextItem();
    }

    // actualizeaza cooldown-uri, timpul la coada si verifica daca agentul e blocat
    private void Update()
    {
        if (itemSwitchCooldown > 0f)
            itemSwitchCooldown -= Time.deltaTime;

        if (hovered)
            CustomerInfoUI.Instance?.Show(this);

        if (state == State.WaitingQueue)
            totalQueueWaitTime += Time.deltaTime;

        if (state == State.JoiningQueue ||
            state == State.ToQueue ||
            state == State.WaitingQueue)
        {
            queueRepathTimer += Time.deltaTime;

            if (queueRepathTimer >= queueRepathInterval)
            {
                queueRepathTimer = 0f;
                ThinkQueue();
            }
        }

        UpdateStuckRecovery();
    }

    // cauta referintele necesare si creeaza pathfinder-ul
    private void SetupReferences()
    {
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>();

        if (queue == null)
            queue = FindAnyObjectByType<CheckoutQueue>();

        animator = GetComponent<Animator>();
        pathfinder = new Pathfinder(grid);
    }


    // pune clientul la intrare si ocupa celula de start
    private void SetupStartingPosition()
    {
        GridCell startCell = grid.Find(CellType.Entrance);
        exitCell = grid.Find(CellType.Exit);

        currentPos = startCell.Pos;
        transform.position = grid.World(currentPos);

        startCell.occupiedBy = this;
    }


    // alege urmatoarea actiune folosind decision tree sau utility ai
    private void GoNextItem()
    {
        ProductType wanted;
        GridCell chosenShelf;
        GridCell chosenSpot = null;

        if (decisionSystem == DecisionSystem.UtilityAI)
            lastDecision = DecideNextActionUtility(out wanted, out chosenShelf, out chosenSpot);
        else
            lastDecision = DecideNextAction(out wanted, out chosenShelf);

        ExecuteDecision(lastDecision, chosenShelf, chosenSpot);
    }


    // executa decizia aleasa: raft, asteptare, schimbare item, skip sau coada
    private void ExecuteDecision(Decision decision, GridCell chosenShelf, GridCell chosenSpot = null)
    {
        switch (decision)
        {
            case Decision.GoToQueue:
                state = State.JoiningQueue;
                GoTo(queue.GetJoinSpot(this));
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

                if (currentShelf == null)
                {
                    StartRethink();
                    return;
                }

                currentShelf.reservedBy = this;
                state = State.ToShelf;

                GridCell spot = chosenSpot != null ? chosenSpot : FreeNear(currentShelf);
                GoTo(spot);
                break;
        }
    }


    // pune clientul in asteptare si porneste recalcularea deciziei
    private void StartRethink()
    {
        state = State.WaitingShelf;

        if (rethinkRoutine != null)
            StopCoroutine(rethinkRoutine);

        rethinkRoutine = StartCoroutine(Rethink());
    }


    // asteapta putin, creste timpul de asteptare la raft si ia o decizie noua
    private IEnumerator Rethink()
    {
        yield return new WaitForSeconds(rethinkTime);

        shelfWaitTimer += rethinkTime;
        totalShelfWaitTime += rethinkTime;

        rethinkRoutine = null;

        GoNextItem();
    }


    private IEnumerator RetryExit()
    {
        yield return new WaitForSeconds(rethinkTime);

        retryExitRoutine = null;

        GoTo(exitCell);
    }


    // simuleaza luarea produsului, scade stocul si trece la urmatorul item
    private IEnumerator Pick()
    {
        state = State.Picking;
        FaceCell(currentShelf);

        yield return new WaitForSeconds(pickTime);

        itemsBought++;
        shelfBlockedAttempts = 0; 

        // Deplete stock
        if (currentShelf != null)
        {
            currentShelf.stock--;
            if (currentShelf.stock <= 0)
            {
                currentShelf.product = ProductType.None;
            }
        }


        ReleaseShelf();
        leavingCurrentTile = true;

        if (currentItemIndex >= 0 && currentItemIndex < shoppingList.Count)
            shoppingList.RemoveAt(currentItemIndex);

        if (currentItemIndex >= shoppingList.Count)
            currentItemIndex = 0;

        shelfWaitTimer = 0f;
        pickRoutine = null;

        yield return new WaitForSeconds(0.2f);

        GoNextItem();
    }


    // gestioneaza pozitia clientului in coada si miscarea spre casa
    private void ThinkQueue()
    {
        if (queue == null)
            return;

        if (state == State.JoiningQueue)
        {
            if (queue.IsCustomerOnQueueCell(this))
            {
                queue.Join(this);
                state = State.WaitingQueue;
                ThinkQueue();
                return;
            }

            GridCell joinSpot = queue.GetJoinSpot(this);

            if (joinSpot == null)
                return;

            if (currentPos == joinSpot.Pos)
                return;

            if (!moving || targetPos != joinSpot.Pos)
                GoTo(joinSpot);

            return;
        }

        GridCell assignedSpot = queue.GetSpot(this);

        if (assignedSpot == null)
            return;

        bool isFirst = queue.IsFirst(this);

        if (isFirst)
        {
            GridCell firstSpot = queue.GetFirstSpot();

            if (firstSpot == null)
                return;

            if (currentPos == firstSpot.Pos)
            {
                if (payRoutine == null)
                    payRoutine = StartCoroutine(Pay());

                return;
            }

            if (!moving || targetPos != firstSpot.Pos)
            {
                state = State.ToQueue;
                GoTo(firstSpot);
            }

            return;
        }

        queueStuckTimer = 0f;

        if (!queue.CanCustomerAdvance(this))
            return;

        if (currentPos == assignedSpot.Pos)
        {
            if (state == State.ToQueue)
                state = State.WaitingQueue;

            return;
        }

        if (!moving || targetPos != assignedSpot.Pos)
        {
            state = State.ToQueue;
            GoTo(assignedSpot);
        }
    }

    // reincerca deplasarea in coada dupa o mica pauza
    private IEnumerator RetryQueue()
    {
        yield return new WaitForSeconds(rethinkTime);

        retryQueueRoutine = null;

        if (state == State.JoiningQueue)
        {
            queue.RefreshJoinSpot(this);
            GoTo(queue.GetJoinSpot(this));
            yield break;
        }

        if (state == State.ToQueue || state == State.WaitingQueue)
        {
            state = State.WaitingQueue;
            ThinkQueue();
        }
    }

    // simuleaza plata, scoate clientul din coada si il trimite spre iesire
    private IEnumerator Pay()
    {
        if (state == State.Paying)
            yield break;

        queueStuckTimer = 0f;

        state = State.Paying;
        FaceCell(queue.GetCheckoutCell());

        yield return new WaitForSeconds(payTime);

        if (queue != null)
            queue.Leave(this);

        leavingCurrentTile = true;
        state = State.ToExit;
        payRoutine = null;

        yield return new WaitForSeconds(0.2f);

        GoTo(exitCell);
    }


    // verifica daca agentul nu s-a mai miscat si porneste recuperarea
    private void UpdateStuckRecovery()
    {
        if (!ShouldCheckForStuck())
        {
            stuckTimer = 0f;
            lastStuckCheckPos = currentPos;
            return;
        }

        if (currentPos != lastStuckCheckPos)
        {
            stuckTimer = 0f;
            lastStuckCheckPos = currentPos;
            return;
        }

        stuckTimer += Time.deltaTime;

        if (stuckTimer >= maxStuckTime)
        {
            stuckTimer = 0f;
            RecoverFromStuck();
        }
    }


    // decide daca starea curenta trebuie verificata pentru blocaj
    private bool ShouldCheckForStuck()
    {
        if (state == State.Picking ||
            state == State.Paying ||
            state == State.WaitingShelf ||
            state == State.Done)
        {
            return false;
        }

        if (state == State.ToShelf ||
            state == State.JoiningQueue ||
            state == State.ToQueue ||
            state == State.ToExit)
        {
            return true;
        }

        if (state == State.WaitingQueue)
        {
            GridCell assignedSpot = queue.GetSpot(this);

            if (assignedSpot == null)
                return false;

            return currentPos != assignedSpot.Pos;
        }

        return false;
    }

    // opreste miscarea curenta si reincearca actiunea potrivita pentru starea actuala
    private void RecoverFromStuck()
    {

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (grid != null)
            grid.ClearMoveReservationsFor(this);

        moving = false;
        path = null;
        leavingCurrentTile = false;

        GridCell currentCell = grid.Get(currentPos);

        if (currentCell != null)
        {
            transform.position = grid.World(currentPos);

            if (currentCell.occupiedBy == null)
                currentCell.occupiedBy = this;
        }

        if (state == State.ToShelf)
        {
            shelfBlockedAttempts++;
            ReleaseShelf();
            StartRethink();
            return;
        }

        if (state == State.JoiningQueue)
        {
            queue.RefreshJoinSpot(this);

            if (retryQueueRoutine == null)
                retryQueueRoutine = StartCoroutine(RetryQueue());

            return;
        }

        if (state == State.ToQueue || state == State.WaitingQueue)
        {
            state = State.WaitingQueue;

            if (retryQueueRoutine == null)
                retryQueueRoutine = StartCoroutine(RetryQueue());

            return;
        }

        if (state == State.ToExit)
        {
            if (retryExitRoutine == null)
                retryExitRoutine = StartCoroutine(RetryExit());

            return;
        }
    }

    // trimite statisticile clientului catre sistemul de statistici
    private void RecordStats()
    {
        if (SimulationStats.Instance == null)
            return;

        float completionTime = Time.time - spawnTime;

        SimulationStats.Instance.RecordCustomer(
            completionTime,
            totalShelfWaitTime,
            totalQueueWaitTime,
            itemsBought,
            itemsSkipped,
            itemSwitches
        );
    }

    public string GetInfoText()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"State: {state}");
        sb.AppendLine();
        sb.AppendLine($"Decision: {lastDecision}");
        sb.AppendLine();
        sb.AppendLine($"Note: {note}");
        sb.AppendLine();
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


    // elibereaza celulele, raftul si coada cand clientul este distrus
    private void OnDestroy()
    {
        if (grid != null)
            grid.ClearMoveReservationsFor(this);

        ClearCurrentTile();
        ReleaseShelf();

        if (queue != null)
            queue.Leave(this);
    }

    private void OnDrawGizmos()
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