using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// On-screen feed of the latest game messages ("Drone 1: grabbed 5 Tomato",
/// "Player: Backpack full", ...). Warnings are red, normal messages grey.
/// Put it on a TMP_Text (or assign one).
/// </summary>
public class MessageFeedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private int maxLines = 6;
    [SerializeField] private float lifetimeSeconds = 8f;

    private struct Entry
    {
        public string message;
        public bool warning;
        public float time;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private bool dirty;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameLog.Posted += OnPosted;
    }

    private void OnDisable()
    {
        GameLog.Posted -= OnPosted;
    }

    private void OnPosted(string message, bool warning)
    {
        entries.Add(new Entry { message = message, warning = warning, time = Time.time });

        while (entries.Count > maxLines)
            entries.RemoveAt(0);

        dirty = true;
    }

    private void Update()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (Time.time - entries[i].time > lifetimeSeconds)
            {
                entries.RemoveAt(i);
                dirty = true;
            }
        }

        if (!dirty || text == null)
            return;

        dirty = false;

        StringBuilder sb = new StringBuilder();
        foreach (Entry e in entries)
        {
            string color = e.warning ? "#FF7777" : "#DDDDDD";
            sb.AppendLine("<color=" + color + ">" + e.message + "</color>");
        }

        text.text = sb.ToString();
    }
}
