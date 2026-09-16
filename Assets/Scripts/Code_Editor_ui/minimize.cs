using UnityEngine;
using TMPro;

public class minimize : MonoBehaviour
{
    private GameObject testArea;
    private bool isMinimized;

    private void Awake()
    {
        // Find the window that THIS minimize button belongs to.
        WindowEdgeResize window = GetComponentInParent<WindowEdgeResize>();

        if (window != null)
        {
            // Find ALL input fields inside THIS window.
            TMP_InputField[] inputFields =
                window.GetComponentsInChildren<TMP_InputField>(true);

            // Use the SECOND input field.
            if (inputFields.Length >= 2)
            {
                testArea = inputFields[0].gameObject;
            }
            else
            {
                Debug.LogWarning(
                    $"{name}: fewer than 2 TMP_InputFields found under this window."
                );
            }
        }

        if (testArea == null)
        {
            Debug.LogWarning(
                $"{name}: couldn't find the second input field under this window."
            );
        }
    }

    public void MinimizeToggle()
    {
        isMinimized = !isMinimized;

        if (testArea != null)
        {
            testArea.SetActive(!isMinimized);
        }

        Debug.Log(
            isMinimized
                ? "Second input field minimized"
                : "Second input field restored"
        );
    }
}