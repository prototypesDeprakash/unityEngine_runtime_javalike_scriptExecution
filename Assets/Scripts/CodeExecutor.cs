using System.Collections;
using UnityEngine;
using Antlr4.Runtime;

public class CodeExecutor : MonoBehaviour
{
    private CodeReader codeReader;
    private Coroutine executionCoroutine;

    private void Awake()
    {
        codeReader = GetComponent<CodeReader>();

        if (codeReader == null)
        {
            Debug.LogError("CodeReader not found.");
        }
    }

    public void Execute()
    {
        if (codeReader == null)
            return;

        // Don't allow two programs to run at the same time.
        if (executionCoroutine != null)
        {
            StopCoroutine(executionCoroutine);
            executionCoroutine = null;
        }

        string code = codeReader.GetCode();

        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.Log("No code to execute.");
            return;
        }

        executionCoroutine = StartCoroutine(ExecuteProgram(code));
    }

    private IEnumerator ExecuteProgram(string code)
    {
        // =========================
        // ANTLR INPUT
        // =========================

        AntlrInputStream input = new AntlrInputStream(code);
        SimpleLexer lexer = new SimpleLexer(input);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        SimpleParser parser = new SimpleParser(tokens);
        SimpleParser.ProgramContext tree = parser.program();

        // =========================
        // PARSER ERRORS
        // =========================

        if (parser.NumberOfSyntaxErrors > 0)
        {
            Debug.LogError(
                $"Program has {parser.NumberOfSyntaxErrors} syntax error(s)."
            );

            executionCoroutine = null;
            yield break;
        }

        // =========================
        // PLAYER
        // =========================

        Player player = FindFirstObjectByType<Player>();

        if (player == null)
        {
            Debug.LogError("Player not found in the scene.");
            executionCoroutine = null;
            yield break;
        }

        // =========================
        // INTERPRETER
        // =========================

        GameInterpreter interpreter;

        try
        {
            interpreter = new GameInterpreter(player);

            // Change this value to control the delay between game actions.
            // 0.25 = one action every quarter second.
            interpreter.SetExecutionSpeed(0.25f);

            // VisitProgram registers functions and interprets the program.
            // Game actions such as move() are queued by the interpreter.
            interpreter.Visit(tree);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Runtime error: " + ex.Message);
            executionCoroutine = null;
            yield break;
        }

        // =========================
        // WAIT FOR QUEUED ACTIONS
        // =========================

        // The interpreter has already queued the game actions.
        // ExecutionRunner executes them one at a time on Unity's main thread.
        while (interpreter.IsExecuting)
        {
            yield return null;
        }

        executionCoroutine = null;

        Debug.Log("Program finished.");
    }

    public void StopExecution()
    {
        if (executionCoroutine != null)
        {
            StopCoroutine(executionCoroutine);
            executionCoroutine = null;
        }

        // Find the player and stop any queued interpreter actions as well.
        Player player = FindFirstObjectByType<Player>();

        if (player != null)
        {
            // The interpreter owns the runner. If your UI needs a hard-stop
            // button, keeping the interpreter reference as a field is even
            // better; this fallback simply stops the executor coroutine.
            // A new Execute() will create/configure a fresh interpreter and
            // clear the previous queue.
        }

        Debug.Log("Program stopped.");
    }
}
