using UnityEngine;

public class WorldGrid : MonoBehaviour
{
    [Header("Grid size")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    [Header("Gizmos")]
    public bool showGizmos = true;
    public Color gridColor = Color.green;

    // World-space position of the grid's (0,0) corner — just uses this
    // object's transform, so you position the grid by moving it in the scene.
    public Vector3 Origin => transform.position;

    // Assumes a top-down world on the XZ plane (Y is up) — swap z for y
    // below if your game is actually a straight 2D XY-plane setup instead.
    public Vector3 GridToWorld(int x, int y)
    {
        return Origin + new Vector3(x * cellSize, 0f, y * cellSize);
    }

    public Vector3 GridCellCenter(int x, int y)
    {
        return GridToWorld(x, y) + new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f);
    }

    public bool WorldToGrid(Vector3 worldPos, out int x, out int y)
    {
        Vector3 local = worldPos - Origin;
        x = Mathf.FloorToInt(local.x / cellSize);
        y = Mathf.FloorToInt(local.z / cellSize);
        return IsInBounds(x, y);
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gridColor;

        // Lines running along X, one per row boundary (0..height inclusive).
        for (int y = 0; y <= height; y++)
        {
            Gizmos.DrawLine(GridToWorld(0, y), GridToWorld(width, y));
        }

        // Lines running along Z, one per column boundary (0..width inclusive).
        for (int x = 0; x <= width; x++)
        {
            Gizmos.DrawLine(GridToWorld(x, 0), GridToWorld(x, height));
        }
    }
}