using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Recipe
{
    public ItemType output;

    [Tooltip("Station the player must stand on. None = can be made anywhere.")]
    public StationType station = StationType.None;

    public List<ItemStack> ingredients = new List<ItemStack>();
}

// One line of a plan: "make 'recipe.output' this many times".
public class RecipeStep
{
    public Recipe recipe;
    public int times;
}

/// <summary>
/// All recipes. An ingredient that has its own recipe is an intermediate
/// (Cooked Meat needs Meat); one without a recipe is raw.
/// Edit the list in the Inspector to add or change recipes.
/// </summary>
public class RecipeBook : MonoBehaviour
{
    [SerializeField] private List<Recipe> recipes = new List<Recipe>();

    private void Reset()
    {
        recipes = DefaultRecipes();
    }

    public bool TryGetRecipe(ItemType item, out Recipe recipe)
    {
        foreach (Recipe r in recipes)
        {
            if (r.output == item)
            {
                recipe = r;
                return true;
            }
        }

        recipe = null;
        return false;
    }

    public bool HasRecipe(ItemType item)
    {
        return TryGetRecipe(item, out _);
    }

    // --------------------------------------------------
    // PLAN: expands a dish all the way down to raw ingredients.
    //   steps = what to make, in order (dependencies first)
    //   raw   = total raw ingredients needed from scratch
    // --------------------------------------------------

    public void GetPlan(
        ItemType dish,
        out List<RecipeStep> steps,
        out Dictionary<ItemType, int> raw)
    {
        steps = new List<RecipeStep>();
        raw = new Dictionary<ItemType, int>();
        Expand(dish, 1, steps, raw, 0);
    }

    private void Expand(
        ItemType item,
        int qty,
        List<RecipeStep> steps,
        Dictionary<ItemType, int> raw,
        int depth)
    {
        if (!TryGetRecipe(item, out Recipe recipe))
        {
            raw[item] = (raw.TryGetValue(item, out int have) ? have : 0) + qty;
            return;
        }

        if (depth > 16)
        {
            Debug.LogError("Recipe loop detected at " + item + ". Check the recipe list.");
            return;
        }

        foreach (ItemStack ing in recipe.ingredients)
            Expand(ing.item, ing.count * qty, steps, raw, depth + 1);

        RecipeStep existing = steps.Find(s => s.recipe == recipe);
        if (existing != null)
            existing.times += qty;
        else
            steps.Add(new RecipeStep { recipe = recipe, times = qty });
    }

    // --------------------------------------------------
    // CRAFTING: consumes the ingredients from 'inventory' (the agent's
    // BACKPACK) and adds ONE output to it. 'at' is the station the agent
    // is standing on. If the output doesn't fit, nothing is changed.
    // --------------------------------------------------

    public bool TryCraft(
        Inventory inventory,
        ItemType output,
        StationType at,
        bool ignoreStation = false,
        string who = "")
    {
        string p = string.IsNullOrEmpty(who) ? "" : who + ": ";

        if (!TryGetRecipe(output, out Recipe recipe))
        {
            GameLog.Warn(p + "no recipe for " + output.DisplayName() + ".");
            return false;
        }

        if (!ignoreStation &&
            recipe.station != StationType.None &&
            recipe.station != at)
        {
            GameLog.Warn($"{p}{output.DisplayName()} must be made on a {recipe.station} cell (standing on {at}).");
            return false;
        }

        foreach (ItemStack ing in recipe.ingredients)
        {
            if (inventory.Has(ing.item, ing.count))
                continue;

            if (inventory.MaxPerItem > 0 && ing.count > inventory.MaxPerItem)
            {
                GameLog.Warn($"{p}{output.DisplayName()} needs {ing.count} {ing.item.DisplayName()}, " +
                             $"but a backpack holds at most {inventory.MaxPerItem} of one item.");
            }
            else
            {
                GameLog.Warn($"{p}can't make {output.DisplayName()}: carrying {inventory.Get(ing.item)}/{ing.count} " +
                             $"{ing.item.DisplayName()}. Grab more from Storage.");
            }

            return false;
        }

        foreach (ItemStack ing in recipe.ingredients)
            inventory.TryRemove(ing.item, ing.count);

        if (!inventory.Add(output, 1))
        {
            // Didn't fit: put the ingredients back.
            foreach (ItemStack ing in recipe.ingredients)
                inventory.Add(ing.item, ing.count);

            GameLog.Warn($"{p}backpack is full - can't hold another {output.DisplayName()}.");
            return false;
        }

        GameLog.Info($"{p}made {output.DisplayName()}.");
        return true;
    }

    // --------------------------------------------------
    // DEFAULTS (placeholders - edit freely in the Inspector)
    // --------------------------------------------------

    private static Recipe Make(
        ItemType output,
        StationType station,
        params ItemStack[] ingredients)
    {
        return new Recipe
        {
            output = output,
            station = station,
            ingredients = new List<ItemStack>(ingredients)
        };
    }

    private static List<Recipe> DefaultRecipes()
    {
        return new List<Recipe>
        {
            // Intermediates
            Make(ItemType.CookedMeat,    StationType.Cooking, new ItemStack(ItemType.Meat, 1)),
            Make(ItemType.BoiledEgg,     StationType.Cooking, new ItemStack(ItemType.Egg, 1)),
            Make(ItemType.GrilledTomato, StationType.Cooking, new ItemStack(ItemType.Tomato, 1)),
            Make(ItemType.CookedRice,    StationType.Cooking, new ItemStack(ItemType.Rice, 1)),

            // Dishes
            Make(ItemType.Burger, StationType.None,
                new ItemStack(ItemType.CookedMeat, 1),
                new ItemStack(ItemType.Bread, 1),
                new ItemStack(ItemType.Tomato, 1)),

            Make(ItemType.Fries, StationType.Cooking,
                new ItemStack(ItemType.Potato, 2)),

            Make(ItemType.Soup, StationType.Cooking,
                new ItemStack(ItemType.GrilledTomato, 1),
                new ItemStack(ItemType.Carrot, 1),
                new ItemStack(ItemType.Onion, 1)),

            Make(ItemType.EggRice, StationType.None,
                new ItemStack(ItemType.CookedRice, 1),
                new ItemStack(ItemType.BoiledEgg, 1),
                new ItemStack(ItemType.Onion, 1)),
        };
    }
}