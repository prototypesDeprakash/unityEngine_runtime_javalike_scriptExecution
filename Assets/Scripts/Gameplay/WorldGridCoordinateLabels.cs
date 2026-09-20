using TMPro;
using UnityEngine;

/// <summary>
/// Shows "(x,y)" in every cell of a WorldGrid using TextMeshPro (visible in Game view).
/// Also controls the WorldGrid's gizmos with a single bool.
/// Put this on any object; assign the grid (or put it on the same object as WorldGrid).
/// </summary>
public class WorldGridCoordinateLabels : MonoBehaviour
{
    [Header("Target")]
    public WorldGrid grid;

    [Header("Toggles")]
    public bool showGizmos = true;
    public bool showTmpText = true;

    [Header("Text")]
    [Tooltip("Multiplied by grid.cellSize, so text scales with the cells.")]
    public float fontSize = 3f;
    public Color textColor = Color.white;
    [Tooltip("Lift above the floor to avoid z-fighting.")]
    public float heightOffset = 0.02f;

    [Header("Gizmo Text (Scene view)")]
    public Color gizmoTextColor = Color.yellow;

    private GameObject container;

    // Used to detect when labels need to be rebuilt.
    private (int, int, float, Vector3, float, Color, float) lastState;

    private void Awake()
    {
        if (grid == null)
            grid = GetComponent<WorldGrid>();

        if (grid == null)
            Debug.LogWarning("WorldGridCoordinateLabels: no WorldGrid assigned.", this);
    }

    private void Start()
    {
        Rebuild();
        ApplyToggles();
    }

    private void Update()
    {
        if (grid == null)
            return;

        if (lastState != GetState())
            Rebuild();

        ApplyToggles();
    }

    private void OnValidate()
    {
        // Makes the gizmo bool work in edit mode too.
        if (grid != null)
            grid.showGizmos = showGizmos;
    }

    private void OnDestroy()
    {
        if (container != null)
            Destroy(container);
    }

    private (int, int, float, Vector3, float, Color, float) GetState()
    {
        return (grid.width, grid.height, grid.cellSize, grid.Origin,
                fontSize, textColor, heightOffset);
    }

    private void ApplyToggles()
    {
        grid.showGizmos = showGizmos;

        if (container != null && container.activeSelf != showTmpText)
            container.SetActive(showTmpText);
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!showGizmos)
            return;

        // Awake hasn't run in edit mode, so resolve the grid here.
        WorldGrid g = grid != null ? grid : GetComponent<WorldGrid>();

        if (g == null)
            return;

        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
        style.normal.textColor = gizmoTextColor;
        style.alignment = TextAnchor.MiddleCenter;

        for (int y = 0; y < g.height; y++)
        {
            for (int x = 0; x < g.width; x++)
            {
                UnityEditor.Handles.Label(
                    g.GridCellCenter(x, y) + Vector3.up * heightOffset,
                    "(" + x + "," + y + ")",
                    style
                );
            }
        }
#endif
    }

    [ContextMenu("Rebuild Labels")]
    public void Rebuild()
    {
        if (grid == null)
            return;

        if (container != null)
            Destroy(container);

        // Not parented on purpose: positions are world-space, so this object's
        // transform can't offset or scale the labels.
        container = new GameObject("GridCoordinateLabels");

        Quaternion flat = Quaternion.Euler(90f, 0f, 0f); // lies flat, readable from above

        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                GameObject go = new GameObject("Label_" + x + "_" + y);
                go.transform.SetParent(container.transform, false);
                go.transform.position =
                    grid.GridCellCenter(x, y) + Vector3.up * heightOffset;
                go.transform.rotation = flat;

                TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                tmp.text = "(" + x + "," + y + ")";
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = fontSize * grid.cellSize;
                tmp.color = textColor;

                // Wide rect so the label never wraps.
                tmp.rectTransform.sizeDelta =
                    new Vector2(grid.cellSize * 3f, grid.cellSize);
            }
        }

        lastState = GetState();
        container.SetActive(showTmpText);
    }
}