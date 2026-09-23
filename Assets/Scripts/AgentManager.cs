using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns every programmable agent and the code window that drives it.
/// Drone + window are created together and linked by direct reference,
/// so nothing ever has to "find" which window belongs to which drone.
/// </summary>
public class AgentManager : MonoBehaviour
{
    public const int MaxDrones = 5;

    [Header("Scene")]
    [SerializeField] private Player player;
    [SerializeField] private CodeExecutor playerExecutor;   // the existing player window
    [SerializeField] private CarriedItemsUI playerCarriedUI; // optional: shows what the Player carries

    [Header("Prefabs")]
    [SerializeField] private Drone dronePrefab;
    [SerializeField] private GameObject codeWindowPrefab;   // your window (with CodeReader + CodeExecutor inside)
    [SerializeField] private RectTransform windowParent;    // Canvas
    [SerializeField] private Vector2 windowCascade = new Vector2(30f, -30f);

    [Header("Drone spawn (always grid 1; scripts move them with goToGrid)")]
    [SerializeField] private Vector2Int droneSpawnCell = Vector2Int.zero;

    private class Slot
    {
        public Drone drone;
        public CodeExecutor executor;
        public GameObject window;
    }

    private readonly List<Slot> slots = new List<Slot>();

    public int DroneCount => slots.Count;
    public bool CanBuyDrone => slots.Count < MaxDrones;

    private void Start()
    {
        playerExecutor.Bind(player);

        if (playerCarriedUI != null)
            playerCarriedUI.Bind(player.Backpack);
    }

    // For UI Buttons - OnClick only lists void methods.
    public void BuyDrone()
    {
        TryBuyDrone();
    }

    public bool TryBuyDrone()
    {
        if (!CanBuyDrone)
            return false;

        int index = slots.Count;

        // Every drone starts on grid 1 (the first world).
        WorldGrid grid = WorldGrid.Get(WorldGrid.PlayerGridNumber);
        if (grid == null)
        {
            Debug.LogError("AgentManager: no grid with Grid Number 1 found.");
            return false;
        }

        int number = index + 1;

        Drone drone = Instantiate(dronePrefab);
        drone.name = "Drone " + number;
        drone.Init(grid, droneSpawnCell);

        GameObject window = Instantiate(codeWindowPrefab, windowParent);
        window.name = "CodeWindow - Drone " + number;
        ((RectTransform)window.transform).anchoredPosition += windowCascade * index;

        CodeExecutor[] executors = window.GetComponentsInChildren<CodeExecutor>(true);
        if (executors.Length == 0)
        {
            Debug.LogError("codeWindowPrefab has no CodeExecutor inside it.");
            Destroy(drone.gameObject);
            Destroy(window);
            return false;
        }

        // Run button and Stop button each have their own CodeExecutor - bind all of them.
        foreach (CodeExecutor e in executors)
            e.Bind(drone);

        // Show what this drone carries inside its own window (optional).
        CarriedItemsUI carried = window.GetComponentInChildren<CarriedItemsUI>(true);
        if (carried != null)
            carried.Bind(drone.Backpack);

        slots.Add(new Slot { drone = drone, executor = executors[0], window = window });
        return true;
    }

    public void RemoveDrone(Drone drone)
    {
        int i = slots.FindIndex(s => s.drone == drone);
        if (i < 0) return;

        slots[i].executor.StopExecution();
        Destroy(slots[i].window);
        Destroy(slots[i].drone.gameObject);
        slots.RemoveAt(i);
    }

    // For a global "Stop all" button.
    public void StopAll()
    {
        playerExecutor.StopExecution();
        foreach (var s in slots)
            s.executor.StopExecution();
    }
}