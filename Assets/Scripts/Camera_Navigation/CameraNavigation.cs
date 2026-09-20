using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraNavigation : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Tooltip("Height of the ground plane the camera pans and zooms over.")]
    public float groundHeight = 0f;

    [Header("Pan")]
    [Tooltip("0 = left, 1 = right, 2 = middle mouse button")]
    public int panMouseButton = 2;

    [Header("Zoom")]
    [Tooltip("Orthographic size, or camera height above the ground if perspective.")]
    public float minZoom = 2f;
    public float maxZoom = 15f;

    [Tooltip("Fraction of the current zoom changed per scroll notch.")]
    public float zoomSpeed = 0.15f;
    public float zoomSmoothSpeed = 10f;

    [Header("Code Window")]
    [Tooltip("Optional: the window that is active before the first click (e.g. the player's window).")]
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

    // Each window keeps its own scale state.
    private class WindowScale
    {
        public RectTransform rect;
        public Vector3 originalScale;
        public float target = 1f;
        public float current = 1f;
    }

    private readonly List<WindowScale> windows = new List<WindowScale>();
    private WindowScale activeWindow;   // the window that was clicked last

    private static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();

    // Camera state
    private float targetZoom;
    private bool isPanning;
    private Vector3 panAnchor;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            Debug.LogWarning("CameraNavigation: no camera found.", this);
    }

    private void Start()
    {
        if (targetCamera != null)
            targetZoom = Mathf.Clamp(GetZoom(), minZoom, maxZoom);

        if (codeWindow != null)
            activeWindow = GetOrCreate(codeWindow);
    }

    private void Update()
    {
        TrackActiveWindow();
        HandleCodeWindowScaling();
        SmoothCodeWindowScales();

        if (targetCamera != null)
        {
            HandleCameraPan();
            HandleCameraZoom();
        }
    }

    // --------------------------------------------------
    // CAMERA
    // --------------------------------------------------

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private bool MouseToGround(out Vector3 point)
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

        if (ground.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private float GetZoom()
    {
        return targetCamera.orthographic
            ? targetCamera.orthographicSize
            : targetCamera.transform.position.y - groundHeight;
    }

    // Drag the world: the ground point under the cursor stays under the cursor.
    private void HandleCameraPan()
    {
        if (Input.GetMouseButtonDown(panMouseButton))
        {
            // A drag that starts on the UI never pans the world.
            isPanning = !IsPointerOverUI() && MouseToGround(out panAnchor);
        }

        if (Input.GetMouseButtonUp(panMouseButton))
            isPanning = false;

        if (isPanning && Input.GetMouseButton(panMouseButton))
        {
            if (MouseToGround(out Vector3 current))
                targetCamera.transform.position += panAnchor - current;
        }
    }

    private void HandleCameraZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        // Scrolling over the UI (code window) never zooms the world.
        if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI())
        {
            targetZoom -= scroll * zoomSpeed * targetZoom;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        SmoothCameraZoom();
    }

    // Zooms toward the cursor.
    private void SmoothCameraZoom()
    {
        float current = GetZoom();

        if (Mathf.Approximately(current, targetZoom))
            return;

        float t = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime);
        float next = Mathf.Lerp(current, targetZoom, t);

        if (Mathf.Abs(next - targetZoom) < 0.001f)
            next = targetZoom;

        Transform camT = targetCamera.transform;
        bool hasAnchor = MouseToGround(out Vector3 before);

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = next;

            if (hasAnchor && MouseToGround(out Vector3 after))
                camT.position += before - after;
        }
        else
        {
            float oldHeight = camT.position.y - groundHeight;

            if (hasAnchor && oldHeight > 0.001f)
            {
                // Slide the camera along the line to the cursor's ground point.
                float k = next / oldHeight;
                camT.position = before + (camT.position - before) * k;
            }
            else
            {
                Vector3 p = camT.position;
                p.y = groundHeight + next;
                camT.position = p;
            }
        }
    }

    // --------------------------------------------------
    // CODE WINDOWS
    // --------------------------------------------------

    // Whichever window you click becomes the one the scale keys affect.
    private void TrackActiveWindow()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        WindowEdgeResize hit = FindWindowUnderPointer();
        if (hit == null)
            return;   // clicked the world or non-window UI: keep the current one

        activeWindow = GetOrCreate((RectTransform)hit.transform);
    }

    // Topmost window under the mouse (RaycastAll is sorted front to back).
    private static WindowEdgeResize FindWindowUnderPointer()
    {
        if (EventSystem.current == null)
            return null;

        PointerEventData data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        raycastHits.Clear();
        EventSystem.current.RaycastAll(data, raycastHits);

        foreach (RaycastResult r in raycastHits)
        {
            WindowEdgeResize w = r.gameObject.GetComponentInParent<WindowEdgeResize>();
            if (w != null)
                return w;
        }

        return null;
    }

    private WindowScale GetOrCreate(RectTransform rect)
    {
        foreach (WindowScale w in windows)
        {
            if (w.rect == rect)
                return w;
        }

        WindowScale created = new WindowScale
        {
            rect = rect,
            originalScale = rect.localScale
        };

        windows.Add(created);
        return created;
    }

    private void HandleCodeWindowScaling()
    {
        // Explicit null check: the active window may have been destroyed.
        if (activeWindow == null || activeWindow.rect == null)
            return;

        if (Input.GetKeyDown(scaleUpKey))
            activeWindow.target += codeWindowZoomAmount;

        if (Input.GetKeyDown(scaleDownKey))
            activeWindow.target -= codeWindowZoomAmount;

        activeWindow.target = Mathf.Clamp(
            activeWindow.target, minCodeWindowScale, maxCodeWindowScale);
    }

    private void SmoothCodeWindowScales()
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            WindowScale w = windows[i];

            // Window was destroyed (e.g. drone removed).
            if (w.rect == null)
            {
                if (w == activeWindow)
                    activeWindow = null;

                windows.RemoveAt(i);
                continue;
            }

            w.current = Mathf.Lerp(
                w.current, w.target, Time.deltaTime * codeWindowSmoothSpeed);

            w.rect.localScale = w.originalScale * w.current;
        }
    }
}