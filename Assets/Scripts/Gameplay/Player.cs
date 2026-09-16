using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("References")]
    public WorldGrid worldGrid;

    private int gridX;
    private int gridY;

    [SerializeField]
    private int Player_y_offset;

    private void Start()
    {
        // Start at the first cell
        gridX = 0;
        gridY = 0;

        // Place player at the center of the first cell
        transform.position =
            worldGrid.GridCellCenter(gridX, gridY)
            + new Vector3(0f, Player_y_offset, 0f);

        Debug.Log(
            "Player spawned at cell (" +
            gridX + ", " + gridY + ")"
        );
    }

    public void TurnLeft()
    { 
    
    }

    public void TurnRight()
    {

    }
    public void Harvest() 
    {

    }
    public void Move()
    {
        // Forward = next cell along Y
        int nextX = gridX;
        int nextY = gridY + 1;

        // Check whether the next cell exists
        if (!worldGrid.IsInBounds(nextX, nextY))
        {
            gridX = 0;
            gridY = 0;
            // Reset actual world position
            transform.position = worldGrid.GridCellCenter(gridX, gridY)+ new Vector3(0f, Player_y_offset, 0f);

            // Debug.Log( "Cannot move. Cell (" +nextX + ", " +nextY +") is outside the grid.");

            return;
        }

        // Update grid position
        gridX = nextX;
        gridY = nextY;

        // Move player to the new cell with Y offset
        transform.position =
            worldGrid.GridCellCenter(gridX, gridY)
            + new Vector3(0f, Player_y_offset, 0f);

        Debug.Log(
            "Player moved to cell (" +
            gridX + ", " + gridY + ")"
        );
    }
}