using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Runs the order loop: place an order -> agents fetch ingredients from
/// STORAGE, make the dish in their BACKPACK -> serve it -> short pause -> next order.
/// </summary>
public class OrderManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The kitchen's stock (an Inventory with no limits).")]
    [SerializeField, FormerlySerializedAs("inventory")] private Inventory storage;
    [SerializeField] private RecipeBook recipes;

    [Header("Orders")]
    [SerializeField]
    private List<ItemType> possibleOrders = new List<ItemType>
    {
        ItemType.Burger, ItemType.Fries, ItemType.Soup, ItemType.EggRice
    };

    [SerializeField] private float firstOrderDelay = 1f;
    [SerializeField] private float delayBetweenOrders = 2f;

    public Inventory Storage => storage;
    public RecipeBook Recipes => recipes;

    public ItemType CurrentOrder { get; private set; } = ItemType.None;
    public bool HasOrder => CurrentOrder != ItemType.None;
    public int CompletedCount { get; private set; }

    public event Action<ItemType> OrderPlaced;
    public event Action<ItemType> OrderCompleted;

    private ItemType lastOrder = ItemType.None;

    private void Start()
    {
        if (storage == null || recipes == null)
        {
            Debug.LogError("OrderManager: assign Storage (Inventory) and Recipe Book.");
            return;
        }

        StartCoroutine(PlaceOrderAfter(firstOrderDelay));
    }

    private IEnumerator PlaceOrderAfter(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlaceNewOrder();
    }

    private void PlaceNewOrder()
    {
        List<ItemType> valid = new List<ItemType>();

        foreach (ItemType dish in possibleOrders)
        {
            if (recipes.HasRecipe(dish))
                valid.Add(dish);
            else
                Debug.LogWarning("OrderManager: no recipe for " + dish + ", skipping it.");
        }

        if (valid.Count == 0)
        {
            Debug.LogError("OrderManager: no valid dishes to order.");
            return;
        }

        // Avoid the same dish twice in a row when there is a choice.
        if (valid.Count > 1)
            valid.Remove(lastOrder);

        CurrentOrder = valid[UnityEngine.Random.Range(0, valid.Count)];
        lastOrder = CurrentOrder;

        GameLog.Info("New order: " + CurrentOrder.DisplayName());
        OrderPlaced?.Invoke(CurrentOrder);
    }

    // --------------------------------------------------
    // COOKING - used by cook(). Works on the agent's OWN backpack:
    // makes the next missing step of the current order (dependencies first).
    // --------------------------------------------------

    public bool TryCraftNextStep(ProgrammableAgent agent, StationType at)
    {
        if (!HasOrder)
        {
            GameLog.Warn($"{agent.name}: no active order.");
            return false;
        }

        Inventory carried = agent.Backpack;

        if (carried.Has(CurrentOrder))
        {
            GameLog.Warn($"{agent.name}: already carrying {CurrentOrder.DisplayName()} - go serve it.");
            return false;
        }

        recipes.GetPlan(CurrentOrder, out List<RecipeStep> steps, out _);

        foreach (RecipeStep s in steps)
        {
            // Already carrying enough of this one - move on to the next step.
            if (carried.Has(s.recipe.output, s.times))
                continue;

            return recipes.TryCraft(carried, s.recipe.output, at, false, agent.name);
        }

        return false;
    }

    // --------------------------------------------------
    // SERVING - used by serve(). The dish must be in the agent's backpack.
    // --------------------------------------------------

    public bool TryServe(ProgrammableAgent agent, ItemType dish)
    {
        if (!HasOrder)
        {
            GameLog.Warn($"{agent.name}: no active order.");
            return false;
        }

        if (dish != CurrentOrder)
        {
            GameLog.Warn($"{agent.name}: wrong dish - ordered {CurrentOrder.DisplayName()}, tried to serve {dish.DisplayName()}.");
            return false;
        }

        if (!agent.Backpack.TryRemove(dish, 1))
        {
            GameLog.Warn($"{agent.name}: not carrying a {dish.DisplayName()} to serve.");
            return false;
        }

        ItemType done = CurrentOrder;
        CurrentOrder = ItemType.None;
        CompletedCount++;

        GameLog.Info($"{agent.name} served {done.DisplayName()}. Order complete!");
        OrderCompleted?.Invoke(done);

        StartCoroutine(PlaceOrderAfter(delayBetweenOrders));
        return true;
    }

    // Serve whatever the current order is (matches the no-argument serve()).
    public bool TryServeCurrentOrder(ProgrammableAgent agent)
    {
        return TryServe(agent, CurrentOrder);
    }

    // --------------------------------------------------
    // TESTING: right-click the OrderManager component header in Play mode.
    // Puts the raw ingredients in the PLAYER's backpack, crafts every step,
    // and serves the order.
    // --------------------------------------------------

    [ContextMenu("Debug: Auto-complete current order (Player)")]
    private void DebugAutoComplete()
    {
        if (!HasOrder) return;

        Player player = FindFirstObjectByType<Player>();
        if (player == null) return;

        recipes.GetPlan(CurrentOrder, out List<RecipeStep> steps, out Dictionary<ItemType, int> raw);

        foreach (var kv in raw)
            player.Backpack.Add(kv.Key, kv.Value);

        foreach (RecipeStep s in steps)
            for (int i = 0; i < s.times; i++)
                recipes.TryCraft(player.Backpack, s.recipe.output, s.recipe.station, true, player.name);

        TryServeCurrentOrder(player);
    }
}