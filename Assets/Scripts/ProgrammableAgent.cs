using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Base class for anything the player's code can control (Player, Drone, ...).
/// Owns grid position, facing, movement and station sensing.
/// Subclasses override the actions they are allowed to perform.
/// </summary>
[RequireComponent(typeof(Inventory))]
public abstract class ProgrammableAgent : MonoBehaviour
{
    [Header("References")]
    public WorldGrid worldGrid;

    // Keeps the value you already set on the Player in the Inspector.
    [SerializeField, FormerlySerializedAs("Player_y_offset")]
    private float yOffset;

    protected int gridX;
    protected int gridY;

    // Every agent currently active, so RouteTo() can skip a station cell
    // someone else is already standing on. Kept up to date via OnEnable/OnDisable.
    private static readonly List<ProgrammableAgent> activeAgents = new List<ProgrammableAgent>();

    protected virtual void OnEnable()
    {
        activeAgents.Add(this);
    }

    protected virtual void OnDisable()
    {
        activeAgents.Remove(this);
    }

    // True if some OTHER active agent is standing on this cell of this grid.
    public static bool IsCellOccupied(WorldGrid grid, int x, int y, ProgrammableAgent exclude)
    {
        foreach (ProgrammableAgent a in activeAgents)
        {
            if (a == exclude || a == null) continue;

            if (a.worldGrid == grid && a.gridX == x && a.gridY == y)
                return true;
        }

        return false;
    }

    // Found once, on first use (works for drones spawned from a prefab too).
    private OrderManager orders;
    protected OrderManager Orders
    {
        get
        {
            if (orders == null)
                orders = FindFirstObjectByType<OrderManager>();
            return orders;
        }
    }

    // Same lazy lookup, for the farming system.
    private PlantsManager plants;
    protected PlantsManager Plants
    {
        get
        {
            if (plants == null)
                plants = FindFirstObjectByType<PlantsManager>();

            return plants;
        }
    }

    // N, E, S, W - clockwise, so index + 1 = turn right.
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private int facing = 0;

    // --------------------------------------------------
    // PLACEMENT
    // --------------------------------------------------

    public void Place(int x, int y)
    {
        gridX = x;
        gridY = y;
        facing = 0;
        UpdateWorldPosition();
    }

    public void clear()
    {
        gridX = 0;
        gridY = 0;
        UpdateWorldPosition();
    }

    private void UpdateWorldPosition()
    {
        transform.position =
            worldGrid.GridCellCenter(gridX, gridY) + new Vector3(0f, yOffset, 0f);
    }

    // --------------------------------------------------
    // TURNING / MOVEMENT (same behaviour as the old Player code)
    // --------------------------------------------------

    public void TurnLeft() => facing = (facing + 3) % 4;
    public void TurnRight() => facing = (facing + 1) % 4;

    public void Move() { var f = Dirs[facing]; Step(f.x, f.y); }
    public void MoveRight() { var f = Dirs[facing]; Step(f.y, -f.x); }
    public void MoveLeft() { var f = Dirs[facing]; Step(-f.y, f.x); }
    public void MoveDown() { var f = Dirs[facing]; Step(-f.x, -f.y); }

    // One step in an absolute direction ((0,1) = North, (1,0) = East, ...),
    // whatever the agent is facing. Used by the automatic navigation.
    public void StepDirection(int dx, int dy)
    {
        Step(dx, dy);
    }

    // Shortest route to the nearest FREE cell of a station on this agent's grid
    // (a cell another agent is standing on is skipped). null = none free/reachable,
    // empty = already on one.
    public List<Vector2Int> RouteTo(StationType station)
    {
        return GridPathfinder.FindRoute(worldGrid, new Vector2Int(gridX, gridY), station, this);
    }

    private void Step(int dx, int dy)
    {
        int w = worldGrid.width;
        int h = worldGrid.height;

        // Wrap around the edges.
        gridX = ((gridX + dx) % w + w) % w;
        gridY = ((gridY + dy) % h + h) % h;

        UpdateWorldPosition();

        if (worldGrid.IsObstacle(gridX, gridY))
            StopScript();

        Debug.Log($"{name} moved to cell ({gridX}, {gridY})");
    }

    // --------------------------------------------------
    // STOPPING
    // --------------------------------------------------

    // Every agent has its OWN ExecutionRunner (added to its GameObject by
    // GameInterpreter), so stopping one agent never touches another.
    public void StopScript()
    {
        var runner = GetComponent<GameInterpreter.ExecutionRunner>();
        if (runner != null)
            runner.Clear();
    }

    // --------------------------------------------------
    // BACKPACK - what THIS agent carries. Separate from the kitchen's Storage.
    // Ingredients must be grabbed from Storage before cooking, and dishes are
    // served from the backpack.
    // --------------------------------------------------

    [Header("Carrying")]
    [Tooltip("Most of any one item this agent can carry.")]
    [SerializeField] private int carryLimitPerItem = 5;
    [Tooltip("Most items of all kinds together. 0 = no total limit.")]
    [SerializeField] private int carryLimitTotal = 0;

    public Inventory Backpack { get; private set; }

    protected virtual void Awake()
    {
        Backpack = GetComponent<Inventory>();

        if (Backpack == null)
            Backpack = gameObject.AddComponent<Inventory>();

        Backpack.SetLimits(carryLimitPerItem, carryLimitTotal);
    }

    // Name of the current order for scripts: "burger", "fries", "soup", "egg rice",
    // or "none" while waiting for the next order.
    public string CurrentOrderName
    {
        get
        {
            if (Orders == null || !Orders.HasOrder)
                return "none";

            return Orders.CurrentOrder.DisplayName().ToLowerInvariant();
        }
    }

    // How many of an item the kitchen Storage holds.
    public int StorageCount(ItemType item)
    {
        return Orders != null && Orders.Storage != null ? Orders.Storage.Get(item) : 0;
    }

    // --------------------------------------------------
    // STATIONS
    // --------------------------------------------------

    // Which grid this agent is on (1 = the player's grid).
    public int GridNumber => worldGrid != null ? worldGrid.gridNumber : 0;

    public StationType CurrentStation => worldGrid.GetStation(gridX, gridY);

    public bool IsOnStation(StationType type) =>
        worldGrid.IsStation(gridX, gridY, type);

    protected bool RequireStation(StationType required, string action)
    {
        if (IsOnStation(required))
            return true;

        GameLog.Warn(
            $"{name}: {action}() only works on a {required} cell " +
            $"(standing on {CurrentStation}).");
        return false;
    }

    // --------------------------------------------------
    // ACTIONS - shared by the Player and every Drone.
    // Timed actions return their duration in seconds, -1 = failed
    // (so the script doesn't wait). Override in a subclass to restrict
    // or change one, e.g. a future drone type that can't cook.
    // --------------------------------------------------

    [Header("Action durations (seconds)")]
    [SerializeField] private float cookDuration = 3f;
    [SerializeField] private float washDuration = 3f;
    [SerializeField] private float serveDuration = 1f;
    [Tooltip("Time a grab() or store() takes.")]
    [SerializeField] private float transferDuration = 0.5f;

    // --------------------------------------------------
    // FARMING - unsupported by default. Only Drone overrides these
    // (see Drone.cs); the Player can't farm.
    // --------------------------------------------------

    // Plants 'crop' on the cell the agent is standing on. If something is
    // already planted there, it is removed and replaced.
    public virtual float Plant(ItemType crop) => Unsupported("plant");

    // True if THIS cell already has a plant growing on it (ready or not).
    public virtual bool IsPlanted() => false;

    // True if the plant on THIS cell is fully grown and ready to harvest.
    public virtual bool CanHarvest() => false;

    // Harvests the plant on this cell into the agent's backpack.
    public virtual float Harvest() => Unsupported("harvest");

    // Makes the next missing step of the current order from what THIS agent carries.
    public virtual float Cook()
    {
        if (!RequireStation(StationType.Cooking, "cook")) return -1f;
        if (Orders == null || !Orders.TryCraftNextStep(this, StationType.Cooking)) return -1f;
        return cookDuration;
    }

    public virtual float Wash()
    {
        if (!RequireStation(StationType.Washing, "wash")) return -1f;
        return washDuration;
    }

    // Serves the current order if THIS agent carries the finished dish.
    public virtual float Serve()
    {
        if (!RequireStation(StationType.Serving, "serve")) return -1f;
        if (Orders == null || !Orders.TryServeCurrentOrder(this)) return -1f;
        return serveDuration;
    }

    // Move items from Storage into the backpack. Needs a Storage cell.
    // Takes as many as fit (backpack limit) and as many as Storage has.
    public virtual float Grab(ItemType item, int count)
    {
        if (!RequireStation(StationType.Storage, "grab")) return -1f;
        if (Orders == null || Orders.Storage == null) return -1f;

        if (count <= 0)
        {
            GameLog.Warn($"{name}: grab() needs a count of 1 or more.");
            return -1f;
        }

        Inventory storage = Orders.Storage;

        int inStorage = storage.Get(item);
        if (inStorage <= 0)
        {
            GameLog.Warn($"{name}: Storage has no {item.DisplayName()}.");
            return -1f;
        }

        int space = Backpack.SpaceFor(item);
        if (space <= 0)
        {
            GameLog.Warn($"{name}: can't carry any more {item.DisplayName()} (backpack limit reached).");
            return -1f;
        }

        int amount = Mathf.Min(count, Mathf.Min(inStorage, space));

        storage.TryRemove(item, amount);
        Backpack.Add(item, amount);

        if (amount < count)
        {
            string reason = amount == space ? "backpack limit" : "Storage ran out";
            GameLog.Info($"{name}: grabbed {amount} {item.DisplayName()} (asked {count}, {reason}).");
        }
        else
        {
            GameLog.Info($"{name}: grabbed {amount} {item.DisplayName()}.");
        }

        return transferDuration;
    }

    // Put items from the backpack back into Storage. Needs a Storage cell.
    // Also works for made items (Cooked Meat, dishes), so agents can hand things over.
    public virtual float Store(ItemType item, int count)
    {
        if (!RequireStation(StationType.Storage, "store")) return -1f;
        if (Orders == null || Orders.Storage == null) return -1f;

        if (count <= 0)
        {
            GameLog.Warn($"{name}: store() needs a count of 1 or more.");
            return -1f;
        }

        int have = Backpack.Get(item);
        if (have <= 0)
        {
            GameLog.Warn($"{name}: not carrying any {item.DisplayName()}.");
            return -1f;
        }

        int amount = Mathf.Min(count, Mathf.Min(have, Orders.Storage.SpaceFor(item)));
        if (amount <= 0)
        {
            GameLog.Warn($"{name}: Storage is full for {item.DisplayName()}.");
            return -1f;
        }

        Backpack.TryRemove(item, amount);
        Orders.Storage.Add(item, amount);

        GameLog.Info($"{name}: stored {amount} {item.DisplayName()}.");
        return transferDuration;
    }

    // Travel to another grid. Unsupported by default - the Player stays on
    // its own grid. Drone overrides this.
    public virtual float GoToGrid(int number) => Unsupported("goToGrid");

    protected float Unsupported(string action)
    {
        GameLog.Warn($"{name} can't {action}().");
        return -1f;
    }
}