
using System;
using UnityEngine;

public class GameFunctions
{
    private readonly Player player;
    private readonly Action<Action> queueGameAction;

    public GameFunctions(Player player, Action<Action> queueGameAction)
    {
        this.player = player;
        this.queueGameAction = queueGameAction;
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
                            // player.MoveSouth();
                            break;

                        case "East":
                            Debug.Log("PLAYER -> MOVE EAST");
                            // player.MoveEast();
                            break;

                        case "West":
                            Debug.Log("PLAYER -> MOVE WEST");
                            // player.MoveWest();
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


            default:
                return false;
        }
    }
}
