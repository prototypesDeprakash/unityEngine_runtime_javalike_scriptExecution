using UnityEngine;
using UnityEngine.EventSystems;

public class CameraNavigation : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Header("Zoom")]
    public float zoomSpeed = 10f;
    public float minZoom = 3f;
    public float maxZoom = 30f;

    [Header("Pan")]
    public float panSpeed = 1f;

    private Vector3 lastMousePosition;
    private bool isPanning;
    private float zoomLevel;

    private void Awake()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }
        zoomLevel = cam.orthographic ? cam.orthographicSize : Mathf.Abs(cam.transform.position.z);
    }

    private void Update()
    {
        HandleZoom();
        HandlePan();
    }

    private bool IsMouseOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleZoom()
    {
        if (IsMouseOverUI()) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        zoomLevel = Mathf.Clamp(zoomLevel - scroll * zoomSpeed, minZoom, maxZoom);

        if (cam.orthographic)
        {
            cam.orthographicSize = zoomLevel;
        }
        else
        {
            cam.transform.position += cam.transform.forward * scroll * zoomSpeed;
        }
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        if (!isPanning) return;

        Vector3 delta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;

        float speed = panSpeed * 0.01f;

        // Move strictly along the camera's own screen-space axes: its right
        // vector for left/right, its actual up vector (not flattened onto
        // the ground plane) for up/down. This keeps panning purely lateral
        // — no forward/backward drift into the scene, regardless of tilt.
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;

        Vector3 move = (-right * delta.x - up * delta.y) * speed;
        cam.transform.position += move;
    }
}