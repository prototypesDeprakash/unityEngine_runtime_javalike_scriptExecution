#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldGrid))]
public class WorldGridEditor : Editor
{
    private enum Brush
    {
        Obstacle,
        Cooking,
        Washing,
        Serving,
        Eraser
    }

    private bool paintMode = false;
    private Brush brush = Brush.Obstacle;

    // Decided on mouse-down from the first cell clicked, then kept for the
    // whole drag. Without this, dragging inside one cell toggles it on and
    // off every mouse event.
    private bool erasing = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        GUI.backgroundColor = paintMode ? Color.red : Color.white;

        string buttonText = paintMode ? "STOP PAINTING" : "PAINT CELLS";

        if (GUILayout.Button(buttonText, GUILayout.Height(35)))
        {
            paintMode = !paintMode;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;

        if (!paintMode)
            return;

        brush = (Brush)GUILayout.Toolbar(
            (int)brush,
            Enum.GetNames(typeof(Brush))
        );

        EditorGUILayout.HelpBox(
            "Click or drag over cells in the Scene view.\n" +
            "Painting a cell that already has the selected brush erases it.",
            MessageType.Info
        );
    }

    private void OnSceneGUI()
    {
        if (!paintMode)
            return;

        // Keeps clicks from changing the selection while painting.
        if (Event.current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(
                GUIUtility.GetControlID(FocusType.Passive)
            );
        }

        Event e = Event.current;

        if (e.type != EventType.MouseDown &&
            e.type != EventType.MouseDrag)
        {
            return;
        }

        if (e.button != 0)
            return;

        // Alt is Scene camera navigation.
        if (e.alt)
            return;

        WorldGrid grid = (WorldGrid)target;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane gridPlane = new Plane(Vector3.up, grid.Origin);

        if (!gridPlane.Raycast(ray, out float distance))
            return;

        if (!grid.WorldToGrid(ray.GetPoint(distance), out int x, out int y))
            return;

        Undo.RecordObject(grid, "Paint Grid Cell");

        if (e.type == EventType.MouseDown)
            erasing = brush == Brush.Eraser || CellHasBrush(grid, x, y);

        Apply(grid, x, y);

        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();

        e.Use();
    }

    private bool TryGetStation(out StationType station)
    {
        switch (brush)
        {
            case Brush.Cooking: station = StationType.Cooking; return true;
            case Brush.Washing: station = StationType.Washing; return true;
            case Brush.Serving: station = StationType.Serving; return true;
            default: station = StationType.None; return false;
        }
    }

    private bool CellHasBrush(WorldGrid grid, int x, int y)
    {
        if (brush == Brush.Obstacle)
            return grid.IsObstacle(x, y);

        return TryGetStation(out StationType station) &&
               grid.IsStation(x, y, station);
    }

    private void Apply(WorldGrid grid, int x, int y)
    {
        if (brush == Brush.Eraser)
        {
            grid.ClearCell(x, y);
        }
        else if (brush == Brush.Obstacle)
        {
            if (erasing) grid.RemoveObstacle(x, y);
            else grid.AddObstacle(x, y);
        }
        else if (TryGetStation(out StationType station))
        {
            grid.SetStation(x, y, erasing ? StationType.None : station);
        }
    }
}

#endif