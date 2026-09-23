using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shortest-path search on a WorldGrid. Uses the same rules as agent movement:
/// the edges wrap around, obstacle cells are avoided (stepping on one would stop
/// the script), and a station cell someone else is standing on is skipped as a
/// destination (but still walkable, so the search keeps looking past it).
/// </summary>
public static class GridPathfinder
{
    // N, E, S, W
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    /// <summary>
    /// Route to the NEAREST FREE cell (fewest steps) that holds the given station -
    /// "free" meaning no other agent is currently standing on it.
    /// Returns the list of steps, each one a direction (0,1) = North, (1,0) = East, ...
    ///   empty list = already standing on one
    ///   null       = no such station, or every matching cell is occupied
    /// </summary>
    public static List<Vector2Int> FindRoute(WorldGrid grid, Vector2Int start, StationType station, ProgrammableAgent requester)
    {
        if (grid == null || station == StationType.None)
            return null;

        // Your own cell always counts as "free", even if it's a station.
        if (grid.IsStation(start.x, start.y, station))
            return new List<Vector2Int>();

        int w = grid.width;
        int h = grid.height;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int> { start };
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> stepUsed = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(start);

        // Breadth-first: the first FREE station cell we reach is the nearest one.
        // An occupied station cell is not a valid destination, but it is not an
        // obstacle either - the search keeps expanding through it.
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int d in Dirs)
            {
                int nx = ((current.x + d.x) % w + w) % w;
                int ny = ((current.y + d.y) % h + h) % h;
                Vector2Int next = new Vector2Int(nx, ny);

                if (seen.Contains(next))
                    continue;

                if (grid.IsObstacle(nx, ny))
                    continue;

                seen.Add(next);
                cameFrom[next] = current;
                stepUsed[next] = d;

                if (grid.IsStation(nx, ny, station) &&
                    !ProgrammableAgent.IsCellOccupied(grid, nx, ny, requester))
                {
                    return Rebuild(start, next, cameFrom, stepUsed);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    // True if the grid has at least one cell of this station type, regardless
    // of whether anyone is standing on it. Used to tell "none exist" apart
    // from "they're all occupied right now".
    public static bool AnyStationExists(WorldGrid grid, StationType station)
    {
        if (grid == null || station == StationType.None)
            return false;

        for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
                if (grid.IsStation(x, y, station))
                    return true;

        return false;
    }

    private static List<Vector2Int> Rebuild(
        Vector2Int start,
        Vector2Int end,
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Dictionary<Vector2Int, Vector2Int> stepUsed)
    {
        List<Vector2Int> steps = new List<Vector2Int>();

        Vector2Int cell = end;
        while (cell != start)
        {
            steps.Add(stepUsed[cell]);
            cell = cameFrom[cell];
        }

        steps.Reverse();
        return steps;
    }
}