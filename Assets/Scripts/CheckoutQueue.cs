using System.Collections.Generic;
using UnityEngine;

public class CheckoutQueue : MonoBehaviour
{
    [SerializeField] private GridManager grid;   //grid-ul din scena

    private List<CustomerAgent> customers = new List<CustomerAgent>();  //agentii care stau la coada
    private GridCell checkoutCell;   //celula casei de marcat

    private void Start()
    {
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>();

        checkoutCell = grid.Find(CellType.Checkout);
    }

    public void Join(CustomerAgent customer)   //adauga agentul la coada
    {
        if (!customers.Contains(customer))
            customers.Add(customer);
    }

    public void Leave(CustomerAgent customer)  //scoate agentul din coada
    {
        customers.Remove(customer);
    }

    public bool IsFirst(CustomerAgent customer)  //verifica daca agentul este primul la coada
    {
        return customers.Count > 0 && customers[0] == customer;
    }

    public GridCell GetSpot(CustomerAgent customer)   //returneaza locul agentului in coada
    {
        int index = customers.IndexOf(customer);

        if (index < 0 || checkoutCell == null)
            return null;

        //coada se formeaza pe coloana, deasupra casei de marcat
        //+1 pentru ca primul client sa stea deasupra casei, nu pe casa
        GridCell spot = grid.Get(checkoutCell.x, checkoutCell.y + index + 1);

        if (spot == null || !spot.walkable)
            return null;

        return spot;
    }

    public int Count()   //returneaza numarul de agenti din coada
    {
        return customers.Count;
    }

    public GridCell GetCheckoutCell()
    {
        return checkoutCell;
    }
}