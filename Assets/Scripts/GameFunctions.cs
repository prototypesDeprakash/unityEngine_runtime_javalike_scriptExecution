using System;
using UnityEngine;

public class GameFunctions
{
    private readonly ProgrammableAgent player;
    private readonly Action<Action> queueGameAction;
    private readonly Action<Func<float>> queueTimedAction;

    public GameFunctions(
     ProgrammableAgent player,
     Action<Action> queueGameAction,
     Action<Func<float>> queueTimedAction)
    {
        this.player = player;
        this.queueGameAction = queueGameAction;
        this.queueTimedAction = queueTimedAction;
    }

    // "Station.Cooking", "Cooking" and "cooking" all resolve to the same station.
    private static bool TryParseStation(object value, out StationType station)
    {
        string name = value?.ToString() ?? "";

        int dot = name.LastIndexOf('.');
        if (dot >= 0)
            name = name.Substring(dot + 1);

        return Enum.TryParse(name, true, out station) &&
               Enum.IsDefined(typeof(StationType), station) &&
               station != StationType.None;
    }

    // For actions that take time: 'action' returns its duration in seconds.
    private bool QueueTimedNoArgAction(string name, object[] args, Func<float> action)
    {
        if (args.Length != 0)
        {
            Debug.LogError(name + "() does not take arguments.");
            return true;
        }

        queueTimedAction(action);
        return true;
    }

    public bool Execute(
        string functionName,
        object[] args,
        out object result)
    {
        result = null;

        switch (functionName)
        {
            // =====================================================
            // move(direction)
            // Example:
            // move(North);
            // move(South);
            // move(East);
            // move(West);
            // =====================================================
            case "move":

                if (args.Length != 1)
                {
                    Debug.LogError(
                        "move() requires exactly one direction argument."
                    );
                    return true;
                }

                string direction = args[0]?.ToString();

                queueGameAction(() =>
                {
                    switch (direction)
                    {
                        case "North":
                            Debug.Log("PLAYER -> MOVE NORTH");
                            player.Move();
                            break;

                        case "South":
                            Debug.Log("PLAYER -> MOVE SOUTH");
                            player.MoveDown();
                            // player.MoveSouth();
                            break;

                        case "East":
                            Debug.Log("PLAYER -> MOVE EAST");
                            player.MoveRight();
                            // player.MoveEast();
                            break;

                        case "West":
                            Debug.Log("PLAYER -> MOVE WEST");
                            // player.MoveWest();
                            player.MoveLeft();
                            break;

                        default:
                            Debug.LogError(
                                $"Unknown direction: {direction}"
                            );
                            break;
                    }
                });

                return true;


            // =====================================================
            // harvest()
            // =====================================================
            case "harvest":

                if (args.Length != 0)
                {
                    Debug.LogError(
                        "harvest() does not take arguments."
                    );
                    return true;
                }

                queueGameAction(() =>
                {
                    Debug.Log("PLAYER -> HARVEST");
                    player.Harvest();
                });

                return true;
            case "clear":
                if (args.Length != 0)
                {
                    Debug.LogError(
                        "clear() does not take arguments."
                    );
                    return true;
                }
                queueGameAction(() =>
                {
                    Debug.Log("PLAYER -> CLEAR POSITION");
                    player.clear();
                });
                return true;

            // =====================================================
            // Station actions - only work while standing on the
            // matching station cell (checked inside Player).
            // =====================================================
            case "cook":
                return QueueTimedNoArgAction("cook", args, () => player.Cook());

            case "wash":
                return QueueTimedNoArgAction("wash", args, () => player.Wash());

            case "serve":
                return QueueTimedNoArgAction("serve", args, () => player.Serve());

            // =====================================================
            // Sensing - answer immediately, no queued action.
            // getStation()                  -> "Cooking", "None", ...
            // onStation(Station.Cooking)    -> true / false
            // =====================================================
            case "getStation":
                if (args.Length != 0)
                {
                    Debug.LogError("getStation() does not take arguments.");
                    return true;
                }

                result = player.CurrentStation.ToString();
                return true;

            case "onStation":
                if (args.Length != 1)
                {
                    Debug.LogError("onStation() requires exactly one station argument.");
                    return true;
                }

                if (!TryParseStation(args[0], out StationType wanted))
                {
                    Debug.LogError("Unknown station: " + args[0]);
                    result = false;
                    return true;
                }

                result = player.IsOnStation(wanted);
                return true;

            case "goToGrid":
                if (args.Length != 1)
                {
                    Debug.LogError("goToGrid() requires exactly one grid number.");
                    return true;
                }

                int gridNumber;
                try { gridNumber = Convert.ToInt32(args[0]); }
                catch
                {
                    Debug.LogError("goToGrid() needs a number, got: " + args[0]);
                    return true;
                }

                queueTimedAction(() => player.GoToGrid(gridNumber));
                return true;

            case "getGrid":
                if (args.Length != 0)
                {
                    Debug.LogError("getGrid() does not take arguments.");
                    return true;
                }

                result = player.GridNumber;
                return true;
            default:
                return false;
        }
    }
}