using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Item counts. Starting amounts are set in the Inspector; everything else
/// (add_potato(3), crafting, serving) goes through the methods below.
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("Starting items (set counts here)")]
    [SerializeField] private List<ItemStack> startingItems = new List<ItemStack>();

    private readonly Dictionary<ItemType, int> counts = new Dictionary<ItemType, int>();
    private bool initialised;

    // Fired after any change. UI listens to this.
    public event Action Changed;

    private void Reset()
    {
        // Pre-fills the list when you add the component: one entry per stocked item, all 0.
        startingItems = new List<ItemStack>();
        foreach (ItemType item in ItemLists.Stocked)
            startingItems.Add(new ItemStack(item, 0));
    }

    private void Awake()
    {
        EnsureInit();
    }

    private void EnsureInit()
    {
        if (initialised) return;
        initialised = true;

        foreach (ItemStack s in startingItems)
        {
            if (s.item == ItemType.None || s.count <= 0) continue;
            counts[s.item] = Get(s.item) + s.count;
        }
    }

    // --------------------------------------------------
    // CORE
    // --------------------------------------------------

    public int Get(ItemType item)
    {
        EnsureInit();
        return counts.TryGetValue(item, out int n) ? n : 0;
    }

    public bool Has(ItemType item, int count = 1)
    {
        return Get(item) >= count;
    }

    public void Add(ItemType item, int count = 1)
    {
        if (item == ItemType.None) return;

        if (count <= 0)
        {
            Debug.LogWarning($"Add({item}, {count}): count must be positive.");
            return;
        }

        counts[item] = Get(item) + count;
        Changed?.Invoke();
    }

    public bool TryRemove(ItemType item, int count = 1)
    {
        if (count <= 0 || !Has(item, count))
            return false;

        counts[item] = Get(item) - count;
        Changed?.Invoke();
        return true;
    }

    // --------------------------------------------------
    // NAMED ADDERS - call these from your own logic later.
    // --------------------------------------------------

    public void add_tomato(int count)        => Add(ItemType.Tomato, count);
    public void add_onion(int count)         => Add(ItemType.Onion, count);
    public void add_potato(int count)        => Add(ItemType.Potato, count);
    public void add_carrot(int count)        => Add(ItemType.Carrot, count);
    public void add_meat(int count)          => Add(ItemType.Meat, count);
    public void add_bread(int count)         => Add(ItemType.Bread, count);
    public void add_cheese(int count)        => Add(ItemType.Cheese, count);
    public void add_egg(int count)           => Add(ItemType.Egg, count);
    public void add_rice(int count)          => Add(ItemType.Rice, count);
    public void add_cooked_meat(int count)   => Add(ItemType.CookedMeat, count);
    public void add_boiled_egg(int count)    => Add(ItemType.BoiledEgg, count);
    public void add_grilled_tomato(int count) => Add(ItemType.GrilledTomato, count);
    public void add_cooked_rice(int count)   => Add(ItemType.CookedRice, count);
}
