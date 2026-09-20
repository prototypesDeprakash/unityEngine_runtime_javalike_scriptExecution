using System;
using System.Text.RegularExpressions;

// IMPORTANT: only ever ADD new items at the END with a new number.
// The Inspector saves these numbers, so reordering/renumbering breaks saved data.
public enum ItemType
{
    None = 0,

    // Raw ingredients
    Tomato = 1,
    Onion = 2,
    Potato = 3,
    Carrot = 4,
    Meat = 5,
    Bread = 6,
    Cheese = 7,
    Egg = 8,
    Rice = 9,

    // Processed ingredients
    CookedMeat = 10,
    BoiledEgg = 11,
    GrilledTomato = 12,
    CookedRice = 13,

    // Dishes (what orders ask for)
    Burger = 14,
    Fries = 15,
    Soup = 16,
    EggRice = 17,
}

[Serializable]
public struct ItemStack
{
    public ItemType item;
    public int count;

    public ItemStack(ItemType item, int count)
    {
        this.item = item;
        this.count = count;
    }
}

public static class ItemLists
{
    // The items shown in the top inventory bar, in the same order as the bar.
    public static readonly ItemType[] Stocked =
    {
        ItemType.Tomato, ItemType.Onion, ItemType.Potato, ItemType.Carrot,
        ItemType.Meat, ItemType.Bread, ItemType.Cheese, ItemType.Egg, ItemType.Rice,
        ItemType.CookedMeat, ItemType.BoiledEgg, ItemType.GrilledTomato, ItemType.CookedRice,
    };
}

public static class ItemTypeExtensions
{
    // CookedMeat -> "Cooked Meat"
    public static string DisplayName(this ItemType item)
    {
        return Regex.Replace(item.ToString(), "(?<!^)([A-Z])", " $1");
    }
}
