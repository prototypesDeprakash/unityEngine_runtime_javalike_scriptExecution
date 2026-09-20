using UnityEngine;

/// <summary>
/// Drone: same abilities as the Player (move, cook, wash, serve), plus it can
/// travel to any grid with goToGrid(n).
/// </summary>
public class Drone : ProgrammableAgent
{
    [Header("Grid travel")]
    [SerializeField] private float travelDuration = 1f;

    // Called by AgentManager right after Instantiate. A prefab can't hold a
    // reference to a scene object, so the grid has to be injected here.
    public void Init(WorldGrid grid, Vector2Int spawnCell)
    {
        worldGrid = grid;
        Place(spawnCell.x, spawnCell.y);
    }

    // Script: goToGrid(2);  -> lands on cell (0, 0) of grid 2 (any grid, 1 included).
    public override float GoToGrid(int number)
    {
        WorldGrid target = WorldGrid.Get(number);

        if (target == null)
        {
            Debug.LogWarning($"{name}: there is no grid {number}.");
            return -1f;
        }

        if (target == worldGrid)
        {
            Debug.Log($"{name}: already on grid {number}.");
            return -1f;
        }

        worldGrid = target;
        Place(0, 0);

        Debug.Log($"{name} -> GRID {number}");
        return travelDuration;
    }
}