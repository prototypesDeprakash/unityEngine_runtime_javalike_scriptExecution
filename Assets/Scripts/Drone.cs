using UnityEngine;

/// <summary>
/// Drone: same abilities as the Player (move, cook, wash, serve), plus it can
/// travel to any grid with goToGrid(n), and it is the only agent that can farm
/// (plant / canHarvest / harvest) - see ProgrammableAgent for why Player can't.
/// </summary>
public class Drone : ProgrammableAgent
{
    [Header("Grid travel")]
    [SerializeField] private float travelDuration = 1f;

    [Header("Farming")]
    [SerializeField] private float plantDuration = 1f;
    [SerializeField] private float harvestDuration = 1f;

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

    // --------------------------------------------------
    // FARMING
    // --------------------------------------------------

    private Vector2Int Cell => new Vector2Int(gridX, gridY);

    // Plants on the current cell. Replaces anything already growing there.
    public override float Plant(ItemType crop)
    {
        if (!RequireStation(StationType.Farmland, "plant")) return -1f;
        if (Plants == null) { GameLog.Warn($"{name}: no PlantsManager in the scene."); return -1f; }

        if (!Plants.Plant(worldGrid, Cell, crop, name, out _))
            return -1f;

        return plantDuration;
    }

    public override bool IsPlanted()
    {
        if (!IsOnStation(StationType.Farmland)) return false;
        if (Plants == null) return false;

        return Plants.HasPlant(worldGrid, Cell);
    }

    public override bool CanHarvest()
    {
        if (!IsOnStation(StationType.Farmland)) return false;
        if (Plants == null) return false;

        return Plants.CanHarvest(worldGrid, Cell);
    }

    // Harvests the plant on the current cell straight into Storage - it
    // skips the backpack entirely, so the carry limit never applies to crops.
    public override float Harvest()
    {
        if (!RequireStation(StationType.Farmland, "harvest")) return -1f;
        if (Plants == null) { GameLog.Warn($"{name}: no PlantsManager in the scene."); return -1f; }
        if (Orders == null || Orders.Storage == null) { GameLog.Warn($"{name}: no Storage to harvest into."); return -1f; }

        if (!Plants.TryHarvest(worldGrid, Cell, name, out ItemType crop, out int amount))
            return -1f;

        int added = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!Orders.Storage.Add(crop, 1)) break;
            added++;
        }

        if (added < amount)
            GameLog.Warn($"{name}: Storage couldn't hold all of it - added {added}/{amount} {crop.DisplayName()}.");
        else
            GameLog.Info($"{name}: harvested {added} {crop.DisplayName()} into Storage.");

        return harvestDuration;
    }
}