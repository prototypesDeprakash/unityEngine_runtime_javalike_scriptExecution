using UnityEngine;
using System.Collections;

using Antlr4.Runtime;
public class CodeExecutor : MonoBehaviour
{
    private CodeReader codeReader;
    private Coroutine executionCoroutine;
    private GameInterpreter interpreter;

    // Which agent this window controls. Set by AgentManager.Bind(...)
    // (or in the Inspector if you prefer, for a fixed window).
    [SerializeField] private ProgrammableAgent target;
    public ProgrammableAgent Target => target;

    private void Awake()
    {
        codeReader = GetComponent<CodeReader>();
        if (codeReader == null)
            Debug.LogError("CodeReader not found.");
    }

    public void Bind(ProgrammableAgent agent)
    {
        StopExecution();   // never leave a run going on the old target
        target = agent;
    }

    public void Execute()
    {
        if (codeReader == null) return;

        if (target == null)
        {
            Debug.LogError(name + ": no agent bound to this window.");
            return;
        }

        StopExecution();

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
        AntlrInputStream input = new AntlrInputStream(code);
        SimpleLexer lexer = new SimpleLexer(input);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        SimpleParser parser = new SimpleParser(tokens);
        SimpleParser.ProgramContext tree = parser.program();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            Debug.LogError($"Program has {parser.NumberOfSyntaxErrors} syntax error(s).");
            executionCoroutine = null;
            yield break;
        }

        try
        {
            interpreter = new GameInterpreter(target);
            interpreter.SetExecutionSpeed(0.25f);
            interpreter.Visit(tree);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Runtime error: " + ex.Message);
            executionCoroutine = null;
            yield break;
        }

        while (interpreter != null && interpreter.IsExecuting)
            yield return null;

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

        if (interpreter != null)
        {
            interpreter.StopExecution();
            interpreter = null;
        }

        // Only THIS window's agent - other drones keep running.
        // Explicit null check on purpose (Unity's overloaded ==).
        if (target != null)
            target.StopScript();

        Debug.Log("Program stopped.");
    }
}