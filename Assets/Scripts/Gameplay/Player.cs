using UnityEngine;

public class Player : ProgrammableAgent
{
    // Kept so existing Inspector references/values are not lost.
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private CodeExecutor codeExecutor;

    private void Start()
    {
        Place(0, 0);
        Debug.Log("Player spawned at cell (0, 0)");
    }
}