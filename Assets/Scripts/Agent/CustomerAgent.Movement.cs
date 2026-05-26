using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class CustomerAgent
{
    private void GoTo(GridCell cell)
    {
        if (moveRoutine != null)
            StopMoveRoutine();
        else if (grid != null)
            grid.ClearMoveReservationsFor(this);

        if (cell == null)
        {
            HandleFailedPath();
            return;
        }

        targetPos = cell.Pos;

        GridCell start = grid.Get(currentPos);
        GridCell goal = grid.Get(targetPos);

        List<GridCell> newPath = pathfinder.FindPath(start, goal, this);

        if (newPath == null || newPath.Count == 0)
        {
            HandleFailedPath();
            return;
        }

        path = newPath;
        moveRoutine = StartCoroutine(Follow(newPath));
    }

    private IEnumerator Follow(List<GridCell> pathToFollow)
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

            if (nextCell == null || nextCell.Pos == currentPos)
                continue;

            float waitTime = 0f;

            while (!CanMoveIntoCell(nextCell))
            {
                waitTime += Time.deltaTime;

                if (waitTime >= maxWaitForTile)
                {
                    moving = false;
                    moveRoutine = null;
                    HandleBlockedPath();
                    yield break;
                }

                yield return null;
            }

            nextCell.reservedForMove = this;
            GridCell oldCell = grid.Get(currentPos);

            leavingCurrentTile = true;

            yield return Move(grid.World(nextCell.Pos));

            if (oldCell != null && oldCell.occupiedBy == this)
                oldCell.occupiedBy = null;

            leavingCurrentTile = false;

            nextCell.reservedForMove = null;
            nextCell.occupiedBy = this;

            currentPos = nextCell.Pos;
        }

        moving = false;
        moveRoutine = null;
        path = null;

        Arrive();
    }

    private bool CanMoveIntoCell(GridCell cell)
    {
        if (!CanUseQueueCell(cell, cell))
            return false;

        return grid.FreeForAgent(cell, this);
    }

    private IEnumerator Move(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        PlayWalkAnimation(direction);

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = target;
        SetIdleFromLastDirection();
    }

    private void StopMoveRoutine()
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

    private void HandleBlockedPath()
    {
        StopMoveRoutine();

        if (state == State.ToShelf)
        {
            shelfBlockedAttempts++;
            ReleaseShelf();
            StartRethink();
            return;
        }

        if (state == State.JoiningQueue)
        {
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

    private void HandleFailedPath()
    {
        if (state == State.ToShelf)
        {
            shelfBlockedAttempts++;
            ReleaseShelf();
            StartRethink();
            return;
        }

        if (state == State.JoiningQueue)
        {
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

    private void Arrive()
    {
        switch (state)
        {
            case State.ToShelf:
                if (pickRoutine == null)
                    pickRoutine = StartCoroutine(Pick());
                break;

            case State.JoiningQueue:
                if (queue.IsCustomerOnQueueCell(this))
                {
                    queue.Join(this);
                    state = State.WaitingQueue;
                    ThinkQueue();
                }
                else
                {
                    GoTo(queue.GetJoinSpot(this));
                }
                break;

            case State.ToQueue:
                state = State.WaitingQueue;
                ThinkQueue();
                break;

            case State.ToExit:
                state = State.Done;

                RecordStats();
                ClearCurrentTile();
                Destroy(gameObject);
                break;
        }
    }

    private GridCell FreeNear(GridCell cell)
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

    private void TryAdd(int x, int y, List<GridCell> list)
    {
        GridCell cell = grid.Get(x, y);

        if (CanUseAsDestination(cell))
            list.Add(cell);
    }

    private bool CanUseAsDestination(GridCell cell)
    {
        if (cell == null || !cell.walkable)
            return false;

        if (!CanUseQueueCell(cell, cell))
            return false;

        if (grid.FreeForAgent(cell, this))
            return true;

        if (cell.occupiedBy != null &&
            cell.occupiedBy != this &&
            cell.occupiedBy.IsLeavingCurrentTile())
        {
            return true;
        }

        return false;
    }

    private void ClearCurrentTile()
    {
        GridCell cell = grid.Get(currentPos);

        if (cell != null && cell.occupiedBy == this)
            cell.occupiedBy = null;
    }

    public bool IsLeavingCurrentTile()
    {
        return leavingCurrentTile;
    }

    public bool CanUseQueueCell(GridCell cell, GridCell goal)
    {
        if (cell == null)
            return false;

        if (cell.type != CellType.QueueEntry)
            return true;

        if (state == State.JoiningQueue ||
            state == State.ToQueue ||
            state == State.WaitingQueue ||
            state == State.Paying ||
            state == State.ToExit)
        {
            if (queue == null)
                return true;

            return queue.CanCustomerUseQueueCell(this, cell, goal);
        }

        return false;
    }

    public Vector2Int GetGridPosition()
    {
        return currentPos;
    }
}