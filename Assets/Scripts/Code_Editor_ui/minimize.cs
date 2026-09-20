using UnityEngine;

public class minimize : MonoBehaviour
{
    private WindowEdgeResize window;
    private bool isMinimized;

    private GameObject mainBackground;
    private GameObject lineHighlight;
    private GameObject linesBackground;
    private GameObject inputField;
    private GameObject scrollbar;

    private void Awake()
    {
        // Get the WindowEdgeResize that owns this minimize button
        window = GetComponentInParent<WindowEdgeResize>();

        if (window == null)
        {
            Debug.LogError($"{name}: WindowEdgeResize parent not found!");
            return;
        }

        // Find objects inside THIS window
        mainBackground = FindChild("MainBackground");
        lineHighlight = FindChild("LineHighlight");
        linesBackground = FindChild("LinesBackground");
        inputField = FindChild("InputField");
        scrollbar = FindChild("Scrollbar");
    }

    private GameObject FindChild(string objectName)
    {
        Transform child = window.transform.Find(objectName);

        if (child != null)
            return child.gameObject;

        Debug.LogWarning($"{name}: Could not find {objectName}");
        return null;
    }

    public void MinimizeToggle()
    {
        isMinimized = !isMinimized;

        SetActive(mainBackground, !isMinimized);
        SetActive(lineHighlight, !isMinimized);
        SetActive(linesBackground, !isMinimized);
        SetActive(inputField, !isMinimized);
        SetActive(scrollbar, !isMinimized);

        Debug.Log(isMinimized ? "Window minimized" : "Window restored");
    }

    private void SetActive(GameObject obj, bool state)
    {
        if (obj != null)
            obj.SetActive(state);
    }
}