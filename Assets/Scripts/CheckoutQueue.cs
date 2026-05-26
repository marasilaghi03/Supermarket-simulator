using System.Collections.Generic;
using UnityEngine;

public class CheckoutQueue : MonoBehaviour
{
    [SerializeField] private GridManager grid;

    private List<CustomerAgent> customers = new List<CustomerAgent>();
    private Dictionary<CustomerAgent, int> joinReservations = new Dictionary<CustomerAgent, int>();

    private GridCell checkoutCell;

    private void Start()
    {
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>();

        checkoutCell = grid.Find(CellType.Checkout);
    }

    public void Join(CustomerAgent customer)
    {
        if (customer == null)
            return;

        if (!customers.Contains(customer))
            customers.Add(customer);

        ClearJoinReservation(customer);
    }

    public void Leave(CustomerAgent customer)
    {
        if (customer == null)
            return;

        customers.Remove(customer);
        ClearJoinReservation(customer);

        if (customers.Count == 0)
            joinReservations.Clear();
    }

    public void ClearJoinReservation(CustomerAgent customer)
    {
        if (customer == null)
            return;

        joinReservations.Remove(customer);
    }

    public bool IsFirst(CustomerAgent customer)
    {
        return customers.Count > 0 && customers[0] == customer;
    }

    public int GetIndex(CustomerAgent customer)
    {
        return customers.IndexOf(customer);
    }

    public int Count()
    {
        return customers.Count;
    }

    public GridCell GetCheckoutCell()
    {
        return checkoutCell;
    }

    public GridCell GetFirstSpot()
    {
        return GetQueueSpotByIndex(0);
    }

    public GridCell GetSpot(CustomerAgent customer)
    {
        int index = customers.IndexOf(customer);

        if (index < 0)
            return null;

        return GetQueueSpotByIndex(index);
    }

    public GridCell GetJoinSpot(CustomerAgent customer)
    {
        if (customer == null || checkoutCell == null)
            return null;

        CleanupJoinReservations();

        if (customers.Count == 0 && NoOneIsPhysicallyInQueue())
            joinReservations.Clear();

        if (!joinReservations.ContainsKey(customer))
            joinReservations[customer] = FindBestJoinIndex(customer);

        GridCell reservedSpot = GetQueueSpotByIndex(joinReservations[customer]);

        if (reservedSpot == null)
        {
            joinReservations[customer] = FindBestJoinIndex(customer);
            reservedSpot = GetQueueSpotByIndex(joinReservations[customer]);
        }

        return reservedSpot;
    }

    public void RefreshJoinSpot(CustomerAgent customer)
    {
        if (customer == null)
            return;

        CleanupJoinReservations();
        joinReservations.Remove(customer);
        joinReservations[customer] = FindBestJoinIndex(customer);
    }

    public bool IsQueueCell(GridCell cell)
    {
        if (cell == null || checkoutCell == null)
            return false;

        if (cell.type != CellType.QueueEntry)
            return false;

        if (cell.x != checkoutCell.x)
            return false;

        if (cell.y <= checkoutCell.y)
            return false;

        return true;
    }

    public bool IsCustomerOnQueueCell(CustomerAgent customer)
    {
        if (customer == null || grid == null)
            return false;

        GridCell currentCell = grid.Get(customer.GetGridPosition());
        return IsQueueCell(currentCell);
    }

    public bool CanCustomerAdvance(CustomerAgent customer)
    {
        int index = customers.IndexOf(customer);

        if (index <= 0)
            return true;

        CustomerAgent directlyAhead = customers[index - 1];

        if (directlyAhead == null)
            return true;

        GridCell aheadSpot = GetQueueSpotByIndex(index - 1);

        if (aheadSpot == null)
            return false;


        if (directlyAhead.GetGridPosition() == aheadSpot.Pos)
            return true;

        if (directlyAhead.IsLeavingCurrentTile())
            return true;

        return false;
    }

    public bool CanCustomerUseQueueCell(CustomerAgent customer, GridCell cell, GridCell goal)
    {
        if (!IsQueueCell(cell))
            return true;

        if (customer == null)
            return false;

        int cellIndex = GetQueueIndex(cell);

        if (cellIndex < 0)
            return false;

        int customerIndex = customers.IndexOf(customer);

        if (customerIndex >= 0)
        {

            if (cellIndex < customerIndex)
                return false;

            return true;
        }


        if (joinReservations.TryGetValue(customer, out int reservedIndex))
        {
            if (cellIndex < customers.Count)
                return false;

            if (cellIndex < reservedIndex)
                return false;

            return true;
        }

        return false;
    }

    private int GetQueueIndex(GridCell cell)
    {
        if (!IsQueueCell(cell))
            return -1;

        return cell.y - checkoutCell.y - 1;
    }

    private int FindBestJoinIndex(CustomerAgent askingCustomer)
    {
        int maxIndex = GetMaxQueueIndex();

        if (maxIndex < 0)
            return 0;

        int reservedJoiners = 0;

        foreach (KeyValuePair<CustomerAgent, int> reservation in joinReservations)
        {
            if (reservation.Key == null || reservation.Key == askingCustomer)
                continue;

            if (customers.Contains(reservation.Key))
                continue;

            reservedJoiners++;
        }

        int wantedIndex = customers.Count + reservedJoiners;

        if (wantedIndex > maxIndex)
            wantedIndex = maxIndex;

        for (int i = wantedIndex; i <= maxIndex; i++)
        {
            if (IsQueueSpotUsableForJoining(i, askingCustomer))
                return i;
        }

        return maxIndex;
    }

    private int GetMaxQueueIndex()
    {
        if (checkoutCell == null)
            return -1;

        int index = 0;
        int max = -1;

        while (true)
        {
            GridCell spot = GetQueueSpotByIndex(index);

            if (spot == null)
                break;

            max = index;
            index++;
        }

        return max;
    }

    private bool IsQueueSpotUsableForJoining(int index, CustomerAgent askingCustomer)
    {
        GridCell spot = GetQueueSpotByIndex(index);

        if (spot == null || !spot.walkable)
            return false;

        if (spot.occupiedBy != null && spot.occupiedBy != askingCustomer)
            return false;

        if (spot.reservedForMove != null && spot.reservedForMove != askingCustomer)
            return false;

        foreach (KeyValuePair<CustomerAgent, int> reservation in joinReservations)
        {
            if (reservation.Key != askingCustomer && reservation.Value == index)
                return false;
        }

        return true;
    }

    private bool NoOneIsPhysicallyInQueue()
    {
        for (int i = 0; i <= GetMaxQueueIndex(); i++)
        {
            GridCell spot = GetQueueSpotByIndex(i);

            if (spot != null && spot.occupiedBy != null)
                return false;
        }

        return true;
    }

    private void CleanupJoinReservations()
    {
        List<CustomerAgent> toRemove = new List<CustomerAgent>();

        foreach (KeyValuePair<CustomerAgent, int> reservation in joinReservations)
        {
            CustomerAgent customer = reservation.Key;

            if (customer == null)
            {
                toRemove.Add(customer);
                continue;
            }

            if (customers.Contains(customer))
            {
                toRemove.Add(customer);
                continue;
            }

            GridCell spot = GetQueueSpotByIndex(reservation.Value);

            if (spot == null)
                toRemove.Add(customer);
        }

        foreach (CustomerAgent customer in toRemove)
        {
            if (joinReservations.ContainsKey(customer))
                joinReservations.Remove(customer);
        }
    }

    private GridCell GetQueueSpotByIndex(int index)
    {
        if (checkoutCell == null)
            return null;

        GridCell spot = grid.Get(checkoutCell.x, checkoutCell.y + index + 1);

        if (spot == null || !spot.walkable)
            return null;

        return spot;
    }
}