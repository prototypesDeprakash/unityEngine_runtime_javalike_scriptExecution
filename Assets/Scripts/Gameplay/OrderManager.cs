using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the order loop: place an order -> player makes it -> player serves it
/// -> short pause -> next order.
/// </summary>
public class OrderManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private RecipeBook recipes;

    [Header("Orders")]
    [SerializeField]
    private List<ItemType> possibleOrders = new List<ItemType>
    {
        ItemType.Burger, ItemType.Fries, ItemType.Soup, ItemType.EggRice
    };

    [SerializeField] private float firstOrderDelay = 1f;
    [SerializeField] private float delayBetweenOrders = 2f;

    public Inventory Inventory => inventory;
    public RecipeBook Recipes => recipes;

    public ItemType CurrentOrder { get; private set; } = ItemType.None;
    public bool HasOrder => CurrentOrder != ItemType.None;
    public int CompletedCount { get; private set; }

    public event Action<ItemType> OrderPlaced;
    public event Action<ItemType> OrderCompleted;

    private ItemType lastOrder = ItemType.None;

    private void Start()
    {
        if (inventory == null || recipes == null)
        {
            Debug.LogError("OrderManager: assign Inventory and Recipe Book.");
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

        Debug.Log("NEW ORDER: " + CurrentOrder.DisplayName());
        OrderPlaced?.Invoke(CurrentOrder);
    }

    // --------------------------------------------------
    // COOKING - used by cook(). Makes the next missing step of the current
    // order (dependencies first). TryCraft logs why it fails.
    // --------------------------------------------------

    public bool TryCraftNextStep(StationType at)
    {
        if (!HasOrder)
        {
            Debug.LogWarning("cook(): no active order.");
            return false;
        }

        if (inventory.Has(CurrentOrder))
        {
            Debug.LogWarning($"{CurrentOrder.DisplayName()} is already made - go serve it.");
            return false;
        }

        recipes.GetPlan(CurrentOrder, out List<RecipeStep> steps, out _);

        foreach (RecipeStep s in steps)
        {
            // Already have enough of this one - move on to the next step.
            if (inventory.Has(s.recipe.output, s.times))
                continue;

            return recipes.TryCraft(inventory, s.recipe.output, at);
        }

        return false;
    }

    // --------------------------------------------------
    // SERVING - used by serve().
    // --------------------------------------------------

    // Serve a specific dish. Fails if it isn't the current order or you don't have it.
    public bool TryServe(ItemType dish)
    {
        if (!HasOrder)
        {
            Debug.LogWarning("No active order.");
            return false;
        }

        if (dish != CurrentOrder)
        {
            Debug.LogWarning($"Wrong dish: ordered {CurrentOrder.DisplayName()}, tried to serve {dish.DisplayName()}.");
            return false;
        }

        if (!inventory.TryRemove(dish, 1))
        {
            Debug.LogWarning($"You don't have a {dish.DisplayName()} to serve.");
            return false;
        }

        ItemType done = CurrentOrder;
        CurrentOrder = ItemType.None;
        CompletedCount++;

        Debug.Log("ORDER SERVED: " + done.DisplayName());
        OrderCompleted?.Invoke(done);

        StartCoroutine(PlaceOrderAfter(delayBetweenOrders));
        return true;
    }

    // Serve whatever the current order is (matches the no-argument serve()).
    public bool TryServeCurrentOrder()
    {
        return TryServe(CurrentOrder);
    }

    // --------------------------------------------------
    // TESTING: right-click the OrderManager component header in Play mode.
    // Adds the raw ingredients, crafts every step, and serves the order.
    // --------------------------------------------------

    [ContextMenu("Debug: Auto-complete current order")]
    private void DebugAutoComplete()
    {
        if (!HasOrder) return;

        recipes.GetPlan(CurrentOrder, out List<RecipeStep> steps, out Dictionary<ItemType, int> raw);

        foreach (var kv in raw)
            inventory.Add(kv.Key, kv.Value);

        foreach (RecipeStep s in steps)
            for (int i = 0; i < s.times; i++)
                recipes.TryCraft(inventory, s.recipe.output, s.recipe.station, ignoreStation: true);

        TryServeCurrentOrder();
    }
}