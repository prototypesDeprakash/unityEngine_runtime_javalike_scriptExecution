using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Base class for anything the player's code can control (Player, Drone, ...).
/// Owns grid position, facing, movement and station sensing.
/// Subclasses override the actions they are allowed to perform.
/// </summary>
public abstract class ProgrammableAgent : MonoBehaviour
{
    [Header("References")]
    public WorldGrid worldGrid;

    // Keeps the value you already set on the Player in the Inspector.
    [SerializeField, FormerlySerializedAs("Player_y_offset")]
    private float yOffset;

    protected int gridX;
    protected int gridY;

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

        Debug.LogWarning(
            $"{name}: {action}() only works on a {required} cell. " +
            $"At ({gridX}, {gridY}) on: {CurrentStation}");
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

    public virtual void Harvest() { }

    // Makes the next missing step of the current order.
    public virtual float Cook()
    {
        if (!RequireStation(StationType.Cooking, "cook")) return -1f;
        if (Orders == null || !Orders.TryCraftNextStep(StationType.Cooking)) return -1f;
        return cookDuration;
    }

    public virtual float Wash()
    {
        if (!RequireStation(StationType.Washing, "wash")) return -1f;
        return washDuration;
    }

    // Serves the current order if the finished dish is in the inventory.
    public virtual float Serve()
    {
        if (!RequireStation(StationType.Serving, "serve")) return -1f;
        if (Orders == null || !Orders.TryServeCurrentOrder()) return -1f;
        return serveDuration;
    }

    // Travel to another grid. Unsupported by default - the Player stays on
    // its own grid. Drone overrides this.
    public virtual float GoToGrid(int number) => Unsupported("goToGrid");

    protected float Unsupported(string action)
    {
        Debug.LogWarning($"{name} can't {action}().");
        return -1f;
    }
}