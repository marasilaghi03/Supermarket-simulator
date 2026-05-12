using UnityEngine;
using System.Collections;

public class Spawner : MonoBehaviour
{
    [SerializeField] private CustomerAgent prefab;
    [SerializeField] private int count = 8;

    private GridManager grid;

    private IEnumerator Start()
    {
        grid = FindAnyObjectByType<GridManager>();

        for (int i = 0; i < count; i++)   //spawnam un agent la fiecare 3 secunde
        {
            Spawn();
            yield return new WaitForSeconds(3f);
        }
    }
    private void Spawn()
    {
        GridCell entrance = grid.Find(CellType.Entrance);

        if (entrance == null) return;

        Vector3 pos = grid.World(entrance.Pos);

        CustomerAgent agent = Instantiate(prefab, pos, Quaternion.identity);
        agent.name = "Customer";
    }
}