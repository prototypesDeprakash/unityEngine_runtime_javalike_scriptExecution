using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CodeReader : MonoBehaviour
{
    private TMP_InputField inputField;

    private void Awake()
    {
        // Find the window this CodeReader belongs to
        WindowEdgeResize window = GetComponentInParent<WindowEdgeResize>();

        if (window != null)
        {
            // Find ALL TMP_InputFields inside THIS window
            TMP_InputField[] inputFields =
                window.GetComponentsInChildren<TMP_InputField>(true);

            // Use the SECOND InputField
            if (inputFields.Length >= 2)
            {
                inputField = inputFields[0];

                Debug.Log($"{name}: Using second InputField: {inputField.name}");
            }
            else
            {
                Debug.LogWarning(
                    $"{name}: Less than 2 TMP_InputFields found in this window."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                $"{name}: WindowEdgeResize not found."
            );
        }
    }

    public List<string> GetLines()
    {
        List<string> lines = new List<string>();

        if (inputField == null)
            return lines;

        string[] rawLines = inputField.text.Split('\n');

        foreach (string line in rawLines)
        {
            // Remove spaces, tabs and carriage returns
            string cleanedLine = line
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace("\r", "");

            // Ignore empty lines
            if (!string.IsNullOrEmpty(cleanedLine))
            {
                lines.Add(cleanedLine);
            }
        }

        return lines;
    }

    public void PrintLines()
    {
        Debug.Log("called");

        List<string> lines = GetLines();


        Debug.Log(string.Join(", ", lines));
        foreach (string line in lines)
        {
            Debug.Log(line);
        }
    }
    public string GetCode()
    {
        if (inputField == null)
            return string.Empty;

        return inputField.text;
    }
}