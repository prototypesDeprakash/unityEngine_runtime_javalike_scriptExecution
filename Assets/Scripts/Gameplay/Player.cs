using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("References")]
    public WorldGrid worldGrid;

    private int gridX;
    private int gridY;

    [SerializeField]
    private int Player_y_offset;

    // Player facing direction
    private enum Direction
    {
        North,
        East,
        South,
        West
    }

    private Direction facingDirection = Direction.North;

    private void Start()
    {
        // Start at the first cell
        gridX = 0;
        gridY = 0;

        // Start facing North
        facingDirection = Direction.North;

        UpdateWorldPosition();

        Debug.Log(
            "Player spawned at cell (" +
            gridX + ", " + gridY + ")"
        );
    }

    // --------------------------------------------------
    // TURNING
    // --------------------------------------------------

    public void TurnLeft()
    {
        switch (facingDirection)
        {
            case Direction.North:
                facingDirection = Direction.West;
                break;

            case Direction.West:
                facingDirection = Direction.South;
                break;

            case Direction.South:
                facingDirection = Direction.East;
                break;

            case Direction.East:
                facingDirection = Direction.North;
                break;
        }

        Debug.Log("Player turned LEFT. Facing: " + facingDirection);
    }

    public void TurnRight()
    {
        switch (facingDirection)
        {
            case Direction.North:
                facingDirection = Direction.East;
                break;

            case Direction.East:
                facingDirection = Direction.South;
                break;

            case Direction.South:
                facingDirection = Direction.West;
                break;

            case Direction.West:
                facingDirection = Direction.North;
                break;
        }

        Debug.Log("Player turned RIGHT. Facing: " + facingDirection);
    }

    // --------------------------------------------------
    // MOVEMENT
    // --------------------------------------------------

    public void Move()
    {
        int nextX = gridX;
        int nextY = gridY;

        switch (facingDirection)
        {
            case Direction.North:
                nextY++;
                break;

            case Direction.East:
                nextX++;
                break;

            case Direction.South:
                nextY--;
                break;

            case Direction.West:
                nextX--;
                break;
        }

        TryMove(nextX, nextY);
    }
    public void clear()
    {
        gridX = 0;
        gridY = 0;

        UpdateWorldPosition();

        Debug.Log("Player position cleared. Returned to (0, 0).");
    }
    public void MoveLeft()
    {
        int nextX = gridX;
        int nextY = gridY;

        switch (facingDirection)
        {
            case Direction.North:
                nextX--;
                break;

            case Direction.East:
                nextY++;
                break;

            case Direction.South:
                nextX++;
                break;

            case Direction.West:
                nextY--;
                break;
        }

        TryMove(nextX, nextY);
    }

    public void MoveRight()
    {
        int nextX = gridX;
        int nextY = gridY;

        switch (facingDirection)
        {
            case Direction.North:
                nextX++;
                break;

            case Direction.East:
                nextY--;
                break;

            case Direction.South:
                nextX--;
                break;

            case Direction.West:
                nextY++;
                break;
        }

        TryMove(nextX, nextY);
    }

    public void MoveDown()
    {
        int nextX = gridX;
        int nextY = gridY;

        switch (facingDirection)
        {
            case Direction.North:
                nextY--;
                break;

            case Direction.East:
                nextX--;
                break;

            case Direction.South:
                nextY++;
                break;

            case Direction.West:
                nextX++;
                break;
        }

        TryMove(nextX, nextY);
    }

    // --------------------------------------------------
    // ACTUAL MOVEMENT
    // --------------------------------------------------

    private void TryMove(int nextX, int nextY)
    {
        // ----------------------------------------------
        // WRAP X
        // ----------------------------------------------

        if (nextX < 0)
        {
            nextX = worldGrid.width - 1;
        }
        else if (nextX >= worldGrid.width)
        {
            nextX = 0;
        }

        // ----------------------------------------------
        // WRAP Y
        // ----------------------------------------------

        if (nextY < 0)
        {
            nextY = worldGrid.height - 1;
        }
        else if (nextY >= worldGrid.height)
        {
            nextY = 0;
        }

        // ----------------------------------------------
        // UPDATE GRID POSITION
        // ----------------------------------------------

        gridX = nextX;
        gridY = nextY;

        // ----------------------------------------------
        // MOVE PLAYER IN WORLD
        // ----------------------------------------------

        UpdateWorldPosition();

        Debug.Log(
            "Player moved to cell (" +
            gridX + ", " + gridY + ")"
        );
    }

    private void UpdateWorldPosition()
    {
        transform.position =
            worldGrid.GridCellCenter(gridX, gridY)
            + new Vector3(0f, Player_y_offset, 0f);
    }

    // --------------------------------------------------
    // OTHER ACTIONS
    // --------------------------------------------------

    public void Harvest()
    {
    }
}