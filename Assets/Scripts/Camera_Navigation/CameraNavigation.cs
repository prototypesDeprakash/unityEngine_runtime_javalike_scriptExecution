
using UnityEngine;

public class CameraNavigation : MonoBehaviour
{
    [Header("Code Window")]
    public RectTransform codeWindow;

    // How much the code window changes per key press
    public float codeWindowZoomAmount = 0.03f;

    // How smoothly the code window reaches its target scale
    public float codeWindowSmoothSpeed = 8f;

    // Limits for the code window scale
    public float minCodeWindowScale = 0.8f;
    public float maxCodeWindowScale = 1.5f;

    [Header("Code Window Scale Keys")]
    public KeyCode scaleUpKey = KeyCode.Equals;
    public KeyCode scaleDownKey = KeyCode.Minus;

    private Vector3 originalCodeWindowScale;

    private float targetCodeWindowScale = 1f;
    private float currentCodeWindowScale = 1f;

    private void Awake()
    {
        if (codeWindow != null)
        {
            originalCodeWindowScale = codeWindow.localScale;
        }
    }

    private void Update()
    {
        HandleCodeWindowScaling();
        SmoothCodeWindowScale();
    }

    private void HandleCodeWindowScaling()
    {
        if (codeWindow == null)
            return;

        // Scale UP
        if (Input.GetKeyDown(scaleUpKey))
        {
            targetCodeWindowScale += codeWindowZoomAmount;

            targetCodeWindowScale = Mathf.Clamp(
                targetCodeWindowScale,
                minCodeWindowScale,
                maxCodeWindowScale
            );
        }

        // Scale DOWN
        if (Input.GetKeyDown(scaleDownKey))
        {
            targetCodeWindowScale -= codeWindowZoomAmount;

            targetCodeWindowScale = Mathf.Clamp(
                targetCodeWindowScale,
                minCodeWindowScale,
                maxCodeWindowScale
            );
        }
    }

    private void SmoothCodeWindowScale()
    {
        if (codeWindow == null)
            return;

        currentCodeWindowScale = Mathf.Lerp(
            currentCodeWindowScale,
            targetCodeWindowScale,
            Time.deltaTime * codeWindowSmoothSpeed
        );

        codeWindow.localScale =
            originalCodeWindowScale * currentCodeWindowScale;
    }
}

