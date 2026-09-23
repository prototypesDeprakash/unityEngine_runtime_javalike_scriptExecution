using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the current order and the full plan, starting with what to grab from
/// Storage. The "needs" line shows the raw ingredients and how many are in
/// STORAGE (red = storage doesn't have enough).
/// </summary>
public class OrderUI : MonoBehaviour
{
    [SerializeField] private OrderManager orders;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text stepsText;
    [SerializeField] private TMP_Text needsText;       // optional
    [SerializeField] private TMP_Text completedText;   // optional

    private void Start()
    {
        if (orders == null)
        {
            Debug.LogError("OrderUI: assign the OrderManager.");
            return;
        }

        orders.OrderPlaced += OnOrderEvent;
        orders.OrderCompleted += OnOrderEvent;

        if (orders.Storage != null)
            orders.Storage.Changed += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (orders == null) return;

        orders.OrderPlaced -= OnOrderEvent;
        orders.OrderCompleted -= OnOrderEvent;

        if (orders.Storage != null)
            orders.Storage.Changed -= Refresh;
    }

    private void OnOrderEvent(ItemType _) => Refresh();

    private void Refresh()
    {
        if (completedText != null)
            completedText.text = "Served: " + orders.CompletedCount;

        if (!orders.HasOrder)
        {
            titleText.text = "Waiting for next order...";
            stepsText.text = "";
            if (needsText != null) needsText.text = "";
            return;
        }

        ItemType dish = orders.CurrentOrder;
        orders.Recipes.GetPlan(dish, out List<RecipeStep> steps, out Dictionary<ItemType, int> raw);

        titleText.text = "ORDER: " + dish.DisplayName().ToUpper();

        StringBuilder sb = new StringBuilder();
        int n = 1;

        // Step 1 is always: fetch the raw ingredients from Storage.
        List<string> grabParts = new List<string>();
        foreach (var kv in raw)
            grabParts.Add(Format(kv.Key, kv.Value));

        sb.AppendLine(n++ + ". Grab " + string.Join(", ", grabParts) + "  [" + StationType.Storage + "]");

        foreach (RecipeStep s in steps)
            sb.AppendLine(n++ + ". " + Describe(s));

        sb.Append(n + ". Serve " + dish.DisplayName() + "  [" + StationType.Serving + "]");
        stepsText.text = sb.ToString();

        if (needsText != null)
        {
            StringBuilder need = new StringBuilder("Raw needed: ");
            bool first = true;

            foreach (var kv in raw)
            {
                int inStorage = orders.Storage.Get(kv.Key);
                string color = inStorage >= kv.Value ? "#7CFC7C" : "#FF5555";

                if (!first) need.Append(",  ");
                first = false;

                need.Append($"<color={color}>{kv.Key.DisplayName()} x{kv.Value} (storage {inStorage})</color>");
            }

            needsText.text = need.ToString();
        }
    }

    // "Cook Meat -> Cooked Meat  [Cooking]"
    private static string Describe(RecipeStep step)
    {
        Recipe r = step.recipe;

        string verb;
        switch (r.station)
        {
            case StationType.Cooking: verb = "Cook"; break;
            case StationType.Washing: verb = "Wash"; break;
            case StationType.Serving: verb = "Serve"; break;
            default: verb = "Make"; break;
        }

        List<string> parts = new List<string>();
        foreach (ItemStack ing in r.ingredients)
            parts.Add(Format(ing.item, ing.count * step.times));

        string line = verb + " " + string.Join(" + ", parts) + " -> " + Format(r.output, step.times);

        if (r.station != StationType.None)
            line += "  [" + r.station + "]";

        return line;
    }

    private static string Format(ItemType item, int count)
    {
        return count > 1 ? item.DisplayName() + " x" + count : item.DisplayName();
    }
}