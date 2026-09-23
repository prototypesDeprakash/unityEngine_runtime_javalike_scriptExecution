using System;
using System.Collections.Generic;
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

    // "Tomato", "tomato", "cooked meat", "Item.CookedMeat" all resolve to the same item.
    private static bool TryParseItem(object value, out ItemType item)
    {
        string name = value?.ToString() ?? "";

        int dot = name.LastIndexOf('.');
        if (dot >= 0)
            name = name.Substring(dot + 1);

        name = name.Replace(" ", "").Replace("_", "");

        item = ItemType.None;

        // Reject numbers like "3" (Enum.TryParse would accept them).
        if (name.Length == 0 || !char.IsLetter(name[0]))
            return false;

        return Enum.TryParse(name, true, out item) &&
               Enum.IsDefined(typeof(ItemType), item) &&
               item != ItemType.None;
    }

    private static bool TryGetItemArg(string fn, object[] args, out ItemType item)
    {
        item = ItemType.None;

        if (args.Length != 1)
        {
            GameLog.Warn(fn + "() takes one item name, e.g. " + fn + "(\"Tomato\").");
            return false;
        }

        if (!TryParseItem(args[0], out item))
        {
            GameLog.Warn("Unknown item: " + args[0]);
            return false;
        }

        return true;
    }

    // grab("Tomato", 3) / store("Tomato") - the count is optional (default 1).
    private bool QueueTransfer(string fn, object[] args, Func<ItemType, int, float> action)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            GameLog.Warn(fn + "() takes an item and an optional count, e.g. " + fn + "(\"Tomato\", 3).");
            return true;
        }

        if (!TryParseItem(args[0], out ItemType item))
        {
            GameLog.Warn("Unknown item: " + args[0]);
            return true;
        }

        int count = 1;

        if (args.Length == 2)
        {
            try { count = Convert.ToInt32(args[1]); }
            catch
            {
                GameLog.Warn(fn + "(): the count must be a number, got: " + args[1]);
                return true;
            }
        }

        queueTimedAction(() => action(item, count));
        return true;
    }

    // Navigation shortcuts (any capitalisation): name -> station to walk to.
    private static readonly Dictionary<string, StationType> NavShortcuts =
        new Dictionary<string, StationType>(StringComparer.OrdinalIgnoreCase)
        {
            { "goToKitchen", StationType.Cooking },
            { "goToStorage", StationType.Storage },
            { "goToServe",   StationType.Serving },
            { "goToWash",    StationType.Washing },
        };

    // Walks the shortest route to the NEAREST FREE cell of the station on the
    // agent's grid (a cell another agent is currently standing on doesn't
    // count as free). The route is computed now and queued as one step per
    // action, so it is walked with the normal step delay.
    //
    // 'result' (bool) tells the script whether it is now on its way:
    //   true  - already there, or just started walking
    //   false - every matching cell is taken right now (or none exist).
    // Scripts should retry, e.g.:  while (!goToKitchen()) { wait(1); }
    private bool QueueGoToStation(string fn, StationType station, object[] args, out object result)
    {
        result = false;

        if (args.Length != 0)
        {
            GameLog.Warn(fn + "() does not take arguments.");
            return true;
        }

        List<Vector2Int> route = player.RouteTo(station);

        // Already standing on a matching cell.
        if (route != null && route.Count == 0)
        {
            GameLog.Info($"{player.name}: already on a {station} cell.");
            result = true;
            return true;
        }

        if (route == null)
        {
            if (GridPathfinder.AnyStationExists(player.worldGrid, station))
                GameLog.Warn($"{player.name}: every {station} cell is occupied right now. Waiting for one to free up.");
            else
                GameLog.Warn($"{player.name}: no {station} cell on grid {player.GridNumber}.");

            result = false;
            return true;
        }

        GameLog.Info($"{player.name}: walking to a free {station} cell ({route.Count} steps).");

        foreach (Vector2Int step in route)
        {
            Vector2Int d = step;
            queueGameAction(() => player.StepDirection(d.x, d.y));
        }

        result = true;
        return true;
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

        // goToKitchen(), goToStorage(), goToServe(), goToWash(), goToStation("Cooking")
        // Each returns true/false - see QueueGoToStation above.
        if (NavShortcuts.TryGetValue(functionName, out StationType navStation))
            return QueueGoToStation(functionName, navStation, args, out result);

        if (string.Equals(functionName, "goToStation", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1)
            {
                GameLog.Warn("goToStation() takes one station name, e.g. goToStation(\"Cooking\").");
                return true;
            }

            if (!TryParseStation(args[0], out StationType namedStation))
            {
                GameLog.Warn("Unknown station: " + args[0]);
                return true;
            }

            return QueueGoToStation("goToStation", namedStation, new object[0], out result);
        }

        // Sensing that returns a value. Case and underscores don't matter:
        //   getCurrentOrder() / getcurrentorder()  -> "burger", "fries", "soup", "egg rice", "none"
        //   get_count("tomato") / getCount("Tomato") / count("tomato") -> number in YOUR backpack
        string senseKey = functionName.Replace("_", "").ToLowerInvariant();

        if (senseKey == "getcurrentorder")
        {
            if (args.Length != 0)
            {
                GameLog.Warn("getCurrentOrder() does not take arguments.");
                result = "none";
                return true;
            }

            result = player.CurrentOrderName;
            return true;
        }

        if (senseKey == "isplanted")
        {
            if (args.Length != 0)
            {
                GameLog.Warn("isPlanted() does not take arguments.");
                result = false;
                return true;
            }

            result = player.IsPlanted();
            return true;
        }

        if (senseKey == "getcount" || senseKey == "count")
        {
            if (!TryGetItemArg("get_count", args, out ItemType countedItem))
            {
                result = 0;
                return true;
            }

            result = player.Backpack.Get(countedItem);
            return true;
        }

        // wait(seconds) - pauses this script (nothing else happens for this agent).
        if (string.Equals(functionName, "wait", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1)
            {
                GameLog.Warn("wait() takes a number of seconds, e.g. wait(2).");
                return true;
            }

            float waitSeconds;
            try { waitSeconds = Convert.ToSingle(args[0]); }
            catch
            {
                GameLog.Warn("wait() needs a number, got: " + args[0]);
                return true;
            }

            if (waitSeconds > 0f)
                queueTimedAction(() => waitSeconds);

            return true;
        }

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
            // Farming (Drone only - Player logs "can't")
            //   plant("tomato")   - replaces whatever is on this cell
            //   canHarvest()      -> true/false, no wait
            //   harvest()         - clears the plant, adds it to your backpack
            // =====================================================
            case "plant":
                if (!TryGetItemArg("plant", args, out ItemType cropToPlant))
                    return true;

                queueTimedAction(() => player.Plant(cropToPlant));
                return true;

            case "canHarvest":
                if (args.Length != 0)
                {
                    GameLog.Warn("canHarvest() does not take arguments.");
                    result = false;
                    return true;
                }

                result = player.CanHarvest();
                return true;

            case "harvest":
                return QueueTimedNoArgAction("harvest", args, () => player.Harvest());
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

            // =====================================================
            // Grid travel and sensing
            //   goToGrid(2)   - drones only
            //   getGrid()     -> current grid number
            // =====================================================
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

            // =====================================================
            // Storage and carrying
            //   grab("Tomato", 3)      - Storage cell only
            //   store("Tomato", 2)     - Storage cell only
            //   count("Tomato")        -> how many you carry
            //   storageCount("Tomato") -> how many Storage holds
            // =====================================================
            case "grab":
                return QueueTransfer("grab", args, (item, n) => player.Grab(item, n));

            case "store":
                return QueueTransfer("store", args, (item, n) => player.Store(item, n));

            case "count":
                if (!TryGetItemArg("count", args, out ItemType carriedItem))
                    return true;

                result = player.Backpack.Get(carriedItem);
                return true;

            case "storageCount":
                if (!TryGetItemArg("storageCount", args, out ItemType stockItem))
                    return true;

                result = player.StorageCount(stockItem);
                return true;

            default:
                return false;
        }
    }
}