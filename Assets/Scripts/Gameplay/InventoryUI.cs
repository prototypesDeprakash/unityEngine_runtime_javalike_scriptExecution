using TMPro;
using UnityEngine;

/// <summary>
/// Writes inventory counts into the top bar.
/// Assign the NUMBER text of each slot, left to right:
/// Tomato, Onion, Potato, Carrot, Meat, Bread, Cheese, Egg, Rice,
/// Cooked Meat, Boiled Egg, Grilled Tomato, Cooked Rice.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private TMP_Text[] countTexts;

    private void Start()
    {
        if (inventory == null)
        {
            Debug.LogError("InventoryUI: assign the Inventory.");
            return;
        }

        inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    private void OnValidate()
    {
        if (countTexts != null && countTexts.Length > 0 &&
            countTexts.Length != ItemLists.Stocked.Length)
        {
            Debug.LogWarning(
                "InventoryUI: expected " + ItemLists.Stocked.Length +
                " count texts, got " + countTexts.Length + ".", this);
        }
    }

    private void Refresh()
    {
        int n = Mathf.Min(countTexts.Length, ItemLists.Stocked.Length);

        for (int i = 0; i < n; i++)
        {
            if (countTexts[i] != null)
                countTexts[i].text = inventory.Get(ItemLists.Stocked[i]).ToString();
        }
    }
}
