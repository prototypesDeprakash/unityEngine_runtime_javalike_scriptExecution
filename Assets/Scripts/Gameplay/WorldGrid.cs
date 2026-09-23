using System.Collections.Generic;
using UnityEngine;

public enum StationType
{
    None,
    Cooking,
    Washing,
    Serving,
    Storage,
    Farmland
}

[System.Serializable]
public struct StationCell
{
    public Vector2Int cell;
    public StationType type;
}

public class WorldGrid : MonoBehaviour
{
    [Header("Grid size")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    public const int PlayerGridNumber = 1;
    [Header("Obstacles")]
    [SerializeField]
    private List<Vector2Int> obstacleCells = new List<Vector2Int>();

    [Header("Stations")]
    [SerializeField]
    private List<StationCell> stationCells = new List<StationCell>();

    [Header("Gizmos")]
    public bool showGizmos = true;
    public Color gridColor = Color.green;

    [Header("Grid number")]
    [Tooltip("1 = the player's grid. Drones travel between grids with goToGrid(n).")]
    public int gridNumber = 1;
    public Vector3 Origin => transform.position;

    // --------------------------------------------------
    // GRID
    // --------------------------------------------------
    private static readonly List<WorldGrid> all = new List<WorldGrid>();

    public static WorldGrid Get(int number)
    {
        foreach (WorldGrid g in all)
        {
            if (g.gridNumber == number)
                return g;
        }
        return null;
    }

    private void OnEnable()
    {
        if (Get(gridNumber) != null)
            Debug.LogWarning($"Two grids share Grid Number {gridNumber}.", this);

        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }
    public Vector3 GridToWorld(int x, int y)
    {
        return Origin + new Vector3(
            x * cellSize,
            0f,
            y * cellSize
        );
    }

    public Vector3 GridCellCenter(int x, int y)
    {
        return GridToWorld(x, y) +
               new Vector3(
                   cellSize * 0.5f,
                   0f,
                   cellSize * 0.5f
               );
    }

    public bool WorldToGrid(
        Vector3 worldPos,
        out int x,
        out int y)
    {
        Vector3 local = worldPos - Origin;

        x = Mathf.FloorToInt(local.x / cellSize);
        y = Mathf.FloorToInt(local.z / cellSize);

        return IsInBounds(x, y);
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 &&
               x < width &&
               y >= 0 &&
               y < height;
    }

    // --------------------------------------------------
    // OBSTACLES
    // --------------------------------------------------

    public bool IsObstacle(int x, int y)
    {
        return obstacleCells.Contains(
            new Vector2Int(x, y)
        );
    }

    public void AddObstacle(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        // A cell is either an obstacle or a station, never both.
        RemoveStation(x, y);

        Vector2Int cell = new Vector2Int(x, y);

        if (!obstacleCells.Contains(cell))
        {
            obstacleCells.Add(cell);
        }
    }

    public void RemoveObstacle(int x, int y)
    {
        obstacleCells.Remove(
            new Vector2Int(x, y)
        );
    }

    public void ToggleObstacle(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        if (IsObstacle(x, y))
        {
            RemoveObstacle(x, y);
        }
        else
        {
            AddObstacle(x, y);
        }
    }

    // This is what the player calls when it steps on one.
    public void ConsumeObstacle(int x, int y)
    {
        if (!IsObstacle(x, y))
            return;

        RemoveObstacle(x, y);


        Debug.Log(
            "Obstacle removed at (" +
            x + ", " + y + ")"
        );
    }

    // --------------------------------------------------
    // STATIONS
    // --------------------------------------------------

    public StationType GetStation(int x, int y)
    {
        for (int i = 0; i < stationCells.Count; i++)
        {
            StationCell s = stationCells[i];

            if (s.cell.x == x && s.cell.y == y)
                return s.type;
        }

        return StationType.None;
    }

    public bool IsStation(int x, int y, StationType type)
    {
        return type != StationType.None &&
               GetStation(x, y) == type;
    }

    // Replaces any station already on the cell.
    // StationType.None just clears it.
    public void SetStation(int x, int y, StationType type)
    {
        if (!IsInBounds(x, y))
            return;

        RemoveStation(x, y);

        if (type == StationType.None)
            return;

        RemoveObstacle(x, y);

        stationCells.Add(new StationCell
        {
            cell = new Vector2Int(x, y),
            type = type
        });
    }

    public void RemoveStation(int x, int y)
    {
        stationCells.RemoveAll(s => s.cell.x == x && s.cell.y == y);
    }

    public void ClearCell(int x, int y)
    {
        RemoveObstacle(x, y);
        RemoveStation(x, y);
    }

    public static Color StationColor(StationType type)
    {
        switch (type)
        {
            case StationType.Cooking: return new Color(1f, 0.55f, 0f);
            case StationType.Washing: return Color.cyan;
            case StationType.Serving: return Color.yellow;
            case StationType.Storage: return  new Color(0.7f, 0.4f, 1f);
            case StationType.Farmland: return new Color(0.4f, 0.8f, 0.3f);
            default: return Color.white;
        }
    }

    // --------------------------------------------------
    // GIZMOS
    // --------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        // Grid
        Gizmos.color = gridColor;

        for (int y = 0; y <= height; y++)
        {
            Gizmos.DrawLine(
                GridToWorld(0, y),
                GridToWorld(width, y)
            );
        }

        for (int x = 0; x <= width; x++)
        {
            Gizmos.DrawLine(
                GridToWorld(x, 0),
                GridToWorld(x, height)
            );
        }

        // Obstacles
        Gizmos.color = Color.red;

        foreach (Vector2Int cell in obstacleCells)
        {
            Vector3 center =
                GridCellCenter(cell.x, cell.y);

            Vector3 size = new Vector3(
                cellSize * 0.8f,
                0.15f,
                cellSize * 0.8f
            );

            Gizmos.DrawCube(center, size);
        }

        // Stations
        foreach (StationCell station in stationCells)
        {
            Gizmos.color = StationColor(station.type);

            Vector3 center =
                GridCellCenter(station.cell.x, station.cell.y);

            Gizmos.DrawCube(
                center,
                new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f)
            );

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                center + Vector3.up * 0.25f,
                station.type.ToString()
            );
#endif
        }
    }
}