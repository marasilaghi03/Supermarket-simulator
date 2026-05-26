using UnityEngine;
using System.Collections;

public class Spawner : MonoBehaviour
{
    [SerializeField] private CustomerAgent prefab;
    [SerializeField] private int count = 20;
    [SerializeField] private float spawnDelay = 3f;

    private GridManager grid;

    private IEnumerator Start()
    {
        grid = FindAnyObjectByType<GridManager>();

        for (int i = 0; i < count; i++)
        {
            if (SimulationStats.Instance != null && SimulationStats.Instance.IsTestFinished())
                yield break;

            Spawn();

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void Spawn()
    {
        GridCell entrance = grid.Find(CellType.Entrance);

        if (entrance == null)
            return;

        Vector3 pos = grid.World(entrance.Pos);

        CustomerAgent agent = Instantiate(prefab, pos, Quaternion.identity);
        agent.name = "Customer";
    }
}