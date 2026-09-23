using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows what ONE agent is carrying, e.g.
///   CARRYING
///   Tomato 3/5
///   Cooked Meat 1/5
/// Put it on a TMP_Text inside a code window (or assign the Text field).
/// AgentManager binds it to that window's agent. Items at their limit are orange.
/// If it was never bound, it says so on screen and in the Console.
/// </summary>
public class CarriedItemsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private string title = "CARRYING";

    private Inventory inventory;
    private bool warnedNoText;

    private void Awake()
    {
        ResolveText();
    }

    private IEnumerator Start()
    {
        // Wait one frame so every Start() and Bind() has happened first.
        yield return null;

        if (inventory == null)
        {
            ResolveText();

            if (text != null)
                text.text = "<color=#FF7777>(not linked to an agent)</color>";

            Debug.LogWarning(
                "CarriedItemsUI on '" + name + "' was never bound to an agent. " +
                "For the Player's window, assign it to 'Player Carried UI' on AgentManager. " +
                "For drone windows it must be inside the window prefab.", this);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void ResolveText()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(Inventory backpack)
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        inventory = backpack;

        if (inventory != null)
            inventory.Changed += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (inventory == null)
            return;

        // Works even if this object was inactive when Bind() ran (Awake hadn't run yet).
        ResolveText();

        if (text == null)
        {
            if (!warnedNoText)
            {
                warnedNoText = true;
                Debug.LogWarning("CarriedItemsUI on '" + name + "' has no TMP_Text. Assign the Text field.", this);
            }
            return;
        }

        StringBuilder sb = new StringBuilder(title);

        if (inventory.MaxTotal > 0)
            sb.Append("  (" + inventory.Total + "/" + inventory.MaxTotal + ")");

        sb.AppendLine();

        bool any = false;

        foreach (ItemType item in Enum.GetValues(typeof(ItemType)))
        {
            if (item == ItemType.None) continue;

            int n = inventory.Get(item);
            if (n <= 0) continue;

            any = true;

            bool full = inventory.MaxPerItem > 0 && n >= inventory.MaxPerItem;
            string amount = inventory.MaxPerItem > 0 ? n + "/" + inventory.MaxPerItem : n.ToString();
            string color = full ? "#FFB347" : "#FFFFFF";

            sb.AppendLine(item.DisplayName() + " <color=" + color + ">" + amount + "</color>");
        }

        if (!any)
            sb.AppendLine("<color=#999999>(empty - grab items at a Storage cell)</color>");

        text.text = sb.ToString();
    }
}