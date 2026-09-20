using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Antlr4.Runtime;
public class GameInterpreter : SimpleBaseVisitor<object>
{
    private readonly ProgrammableAgent player;
    private readonly GameFunctions gameFunctions;
    private readonly ExecutionRunner executionRunner;

    // Game actions produced while evaluating the CURRENT statement land
    // here instead of a long-lived queue. They get executed and paced out
    // (one WaitForSeconds each) immediately after that statement finishes,
    // before the interpreter moves on - see FlushPendingActions.
    // Each entry runs a game action and returns how many seconds the script
    // should wait afterwards. Return <= 0 to use the normal step delay.
    private readonly List<Func<float>> pendingActions = new();

    // Delay between game actions. This is the "internal clock" of the
    // programming game. Increase/decrease this to control execution speed.
    private const float DefaultStepDelay = 0.25f;

    /// <summary>
    /// Runs the interpreter's own program coroutine on Unity's main thread.
    ///
    /// IMPORTANT: this used to independently drain a queue of actions that
    /// the interpreter filled up-front by tree-walking the whole script in
    /// one frame. That's what made while-loops "run forever": the walk
    /// evaluated the loop condition against state that hadn't changed yet
    /// (the matching action was only queued, not yet executed), so it kept
    /// re-queuing until it hit the 1,000,000-iteration safety cap, then
    /// played all of that back at stepDelay seconds each.
    ///
    /// Now the interpreter itself is the coroutine: it performs an action
    /// and waits for it before re-checking any condition, so loops see
    /// up-to-date state and only ever run as many iterations as they should.
    /// </summary>
    // Was 'private' - now 'public' so Player (and CodeExecutor, via Player)
    // can fetch this component directly with GetComponent<GameInterpreter.ExecutionRunner>()
    // and stop it, instead of going through a cached GameInterpreter/CodeExecutor
    // reference that can go stale or point at the wrong instance.
    public class ExecutionRunner : MonoBehaviour
    {
        private Coroutine executionCoroutine;
        private float stepDelay = DefaultStepDelay;

        // Bumped on every Clear()/Run(). Each Drive() coroutine remembers
        // the id it started with and quits the moment it no longer matches,
        // so an old run can never keep stepping after it has been stopped
        // or replaced - no matter how Unity handles StopCoroutine.
        private int runId;
        private bool isRunning;

        public bool IsRunning => isRunning;

        public float StepDelay => stepDelay;

        public void Configure(float delay)
        {
            stepDelay = Mathf.Max(0f, delay);
        }

        public void Clear()
        {
            runId++;          // invalidates whatever Drive() is in flight
            isRunning = false;

            if (executionCoroutine != null)
            {
                StopCoroutine(executionCoroutine);
                executionCoroutine = null;
            }
        }

        public void Run(IEnumerator routine)
        {
            Clear(); // always kill the previous run first

            int myRun = runId;
            isRunning = true;
            executionCoroutine = StartCoroutine(Drive(routine, myRun));
        }

        // Pumps 'routine' by hand instead of "yield return routine".
        // Yielding an IEnumerator makes Unity spin up a nested coroutine
        // that StopCoroutine(parent) does not reliably stop - the old
        // script kept running as an orphan. Pumping it here keeps the
        // whole run inside ONE coroutine that Clear() can actually stop.
        private IEnumerator Drive(IEnumerator routine, int myRun)
        {
            while (myRun == runId)
            {
                bool moved;

                try
                {
                    moved = routine.MoveNext();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    break;
                }

                if (!moved)
                    break;

                yield return routine.Current;
            }

            // Only the run that is still current may mark itself finished.
            if (myRun == runId)
            {
                isRunning = false;
                executionCoroutine = null;
            }
        }

        private void OnDisable()
        {
            Clear();
        }
    }


    public GameInterpreter(ProgrammableAgent player)
    {
        this.player = player;

        if (player == null)
            throw new ArgumentNullException(nameof(player));

        // The runner lives on the Player GameObject so it can use Unity's
        // coroutine system without changing the way GameInterpreter is
        // created by the rest of your project.
        executionRunner = player.GetComponent<ExecutionRunner>();

        if (executionRunner == null)
            executionRunner = player.gameObject.AddComponent<ExecutionRunner>();

        executionRunner.Configure(DefaultStepDelay);
        gameFunctions = new GameFunctions(player, QueueGameAction, QueueTimedAction);
    }

    /// <summary>
    /// Change the delay between game instructions at runtime.
    /// Example: interpreter.SetExecutionSpeed(0.5f);
    /// </summary>
    public void SetExecutionSpeed(float secondsPerStep)
    {
        executionRunner.Configure(secondsPerStep);
    }

    /// <summary>
    /// True while Unity is still executing queued game actions.
    /// CodeExecutor uses this to wait without blocking the Unity main thread.
    /// </summary>
    public bool IsExecuting => executionRunner != null && executionRunner.IsRunning;

    /// <summary>
    /// Stops any pending game actions from the current run.
    /// </summary>
    public void StopExecution()
    {
        executionRunner.Clear();
    }
    // ============================================
    // CONTROL-FLOW SIGNALS
    // ============================================
    // return/break/continue are implemented as exceptions so they can
    // unwind through nested blocks/ifs/loops without every Visit method
    // needing to know about them explicitly.

    private class ReturnSignal : Exception
    {
        public readonly object Value;
        public ReturnSignal(object value) => Value = value;
    }

    private class BreakSignal : Exception { }

    private class ContinueSignal : Exception { }

    // ============================================
    // VARIABLES / SCOPING
    // ============================================
    // Globals live for the whole program. Each active user-function call
    // pushes its own local frame (parameters + locals declared inside it).
    // A function only sees its own locals + globals - not the caller's
    // locals - matching normal call-stack semantics.
    //
    // NOTE: there is no separate scope per if/while/for block - variables
    // declared inside one are visible for the rest of the enclosing
    // function (or globally, at top level). That's a simplification, not
    // a grammar limitation - fine for this kind of scripting game, but
    // worth knowing if you ever want stricter block scoping.

    private readonly Dictionary<string, object> globals = new();
    private readonly List<Dictionary<string, object>> localStack = new();

    // Stores user-created functions.
    private readonly Dictionary<string, SimpleParser.FunctionDeclarationContext> functions
        = new();

    private Dictionary<string, object> CurrentLocalScope =>
        localStack.Count > 0 ? localStack[^1] : null;

    private bool TryGetVariable(string name, out object value)
    {
        var local = CurrentLocalScope;

        if (local != null && local.TryGetValue(name, out value))
            return true;

        return globals.TryGetValue(name, out value);
    }

    private void DeclareVariable(string name, object value)
    {
        if (CurrentLocalScope != null)
            CurrentLocalScope[name] = value;
        else
            globals[name] = value;
    }

    private void SetVariable(string name, object value)
    {
        var local = CurrentLocalScope;

        if (local != null && local.ContainsKey(name))
        {
            local[name] = value;
            return;
        }

        if (local == null || globals.ContainsKey(name))
        {
            globals[name] = value;
            return;
        }

        // Assigning to a name that doesn't exist anywhere yet while
        // inside a function - treat it as a new local.
        local[name] = value;
    }

    // ============================================
    // PROGRAM
    // ============================================

    public override object VisitProgram(SimpleParser.ProgramContext context)
    {
        executionRunner.Run(RunProgram(context));
        return null;
    }

    /// <summary>
    /// The actual program driver. Registers functions synchronously (fast,
    /// no game actions possible there), then executes every other
    /// top-level item through the coroutine-aware Exec* methods so a
    /// while/for/etc. only advances once its previous action has really
    /// happened in-game.
    /// </summary>
    private IEnumerator RunProgram(SimpleParser.ProgramContext context)
    {
        pendingActions.Clear();

        // First pass: register all user-defined functions.
        foreach (var item in context.topLevelItem())
        {
            if (item.functionDeclaration() != null)
                Visit(item.functionDeclaration());
        }

        // Second pass: execute everything else, top to bottom.
        foreach (var item in context.topLevelItem())
        {
            if (item.functionDeclaration() != null)
                continue;

            var exec = ExecTopLevelItem(item);

            while (true)
            {
                bool moved;

                try
                {
                    moved = exec.MoveNext();
                }
                catch (ReturnSignal)
                {
                    Console.WriteLine("'return' used outside of a function.");
                    break;
                }
                catch (BreakSignal)
                {
                    Console.WriteLine("'break' used outside of a loop.");
                    break;
                }
                catch (ContinueSignal)
                {
                    Console.WriteLine("'continue' used outside of a loop.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Runtime error: {ex.Message}");
                    break;
                }

                if (!moved)
                    break;

                yield return exec.Current;
            }
        }
    }

    /// <summary>
    /// Dispatches one top-level item to its coroutine-aware executor.
    /// Mirrors ExecBlockItem below, minus functionDeclaration (handled
    /// separately, up front, in RunProgram).
    /// </summary>
    private IEnumerator ExecTopLevelItem(SimpleParser.TopLevelItemContext context)
    {
        if (context.statement() != null) return ExecStatement(context.statement());
        if (context.ifBlock() != null) return ExecIfBlock(context.ifBlock());
        if (context.whileBlock() != null) return ExecWhileBlock(context.whileBlock());
        if (context.forBlock() != null) return ExecForBlock(context.forBlock());
        if (context.forEachBlock() != null) return ExecForEachBlock(context.forEachBlock());
        if (context.doWhileBlock() != null) return ExecDoWhileBlock(context.doWhileBlock());
        if (context.switchBlock() != null) return ExecSwitchBlock(context.switchBlock());

        throw new Exception($"Unknown top-level item: {context.GetText()}");
    }

    public override object VisitTopLevelItem(SimpleParser.TopLevelItemContext context)
    {
        return Visit(context.GetChild(0));
    }

    // ============================================
    // FUNCTION DECLARATION / CALL
    // ============================================

    public override object VisitFunctionDeclaration(
        SimpleParser.FunctionDeclarationContext context)
    {
        string functionName = context.ID().GetText();

        functions[functionName] = context;

        Console.WriteLine($"Registered function: {functionName}");

        return null;
    }

    public override object VisitFunctionCall(
        SimpleParser.FunctionCallContext context)
    {
        string functionName = context.ID().GetText();
        object[] args = EvaluateArguments(context.argumentList());

        if (ExecuteBuiltInFunction(functionName, args, out object builtInResult))
            return builtInResult;

        if (functions.TryGetValue(functionName, out var function))
            return CallUserFunction(function, args);

        Console.WriteLine($"Unknown function: {functionName}");
        return null;
    }

    private object[] EvaluateArguments(SimpleParser.ArgumentListContext argList)
    {
        if (argList == null)
            return Array.Empty<object>();

        var expressions = argList.expression();
        var result = new object[expressions.Length];

        for (int i = 0; i < expressions.Length; i++)
            result[i] = Visit(expressions[i]);

        return result;
    }

    private object CallUserFunction(
        SimpleParser.FunctionDeclarationContext function, object[] args)
    {
        var parameters = function.parameterList()?.parameter()
            ?? Array.Empty<SimpleParser.ParameterContext>();

        var frame = new Dictionary<string, object>();

        for (int i = 0; i < parameters.Length; i++)
        {
            string paramName = parameters[i].variableDeclaratorId().ID().GetText();
            frame[paramName] = i < args.Length ? args[i] : null;
        }

        localStack.Add(frame);

        try
        {
            Visit(function.block());
            return null; // fell off the end with no return statement
        }
        catch (ReturnSignal ret)
        {
            return ret.Value;
        }
        finally
        {
            localStack.RemoveAt(localStack.Count - 1);
        }
    }

    // Add your own game functions in this switch. Grammar-wise, ANY
    // name + parentheses already parses as a functionCall - new
    // built-ins only need to be registered here.
    private bool ExecuteBuiltInFunction(string functionName, object[] args, out object result)
    {
        return gameFunctions.Execute(functionName, args, out result);
    }

    // Normal instant action - script waits the regular step delay after it.
    private void QueueGameAction(Action action)
    {
        if (action == null)
            return;

        pendingActions.Add(() =>
        {
            action();
            return -1f;
        });
    }

    // Timed action - the action runs, returns how long it takes (seconds),
    // and the script pauses that long before the next statement.
    private void QueueTimedAction(Func<float> action)
    {
        if (action != null)
            pendingActions.Add(action);
    }

    /// <summary>
    /// Executes and paces out any game actions produced while evaluating
    /// the statement just visited (e.g. a move() call), one at a time,
    /// waiting stepDelay between each. Nothing to do 99% of the time -
    /// most statements don't touch the game at all.
    /// </summary>
    private IEnumerator FlushPendingActions()
    {
        if (pendingActions.Count == 0)
            yield break;

        var actions = new List<Func<float>>(pendingActions);
        pendingActions.Clear();

        float stepDelay = executionRunner.StepDelay;

        foreach (var action in actions)
        {
            float duration = -1f;

            try
            {
                duration = action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // The action's own duration replaces the step delay.
            float wait = duration > 0f ? duration : stepDelay;

            if (wait > 0f)
                yield return new WaitForSeconds(wait);
            else
                yield return null;
        }
    }

    // ============================================
    // BLOCK / STATEMENT WRAPPERS
    // ============================================

    public override object VisitBlock(SimpleParser.BlockContext context)
    {
        foreach (var item in context.blockItem())
            Visit(item);

        return null;
    }

    public override object VisitBlockItem(SimpleParser.BlockItemContext context)
    {
        return Visit(context.GetChild(0));
    }

    public override object VisitStatement(SimpleParser.StatementContext context)
    {
        return Visit(context.GetChild(0));
    }

    public override object VisitExpressionStatement(
        SimpleParser.ExpressionStatementContext context)
    {
        Visit(context.expression());
        return null;
    }

    // ============================================
    // PRINT / RETURN / BREAK / CONTINUE
    // ============================================

    public override object VisitPrintStatement(
        SimpleParser.PrintStatementContext context)
    {
        if (context.expression() == null)
        {
            Debug.Log("");
            return null;
        }

        Debug.Log(Stringify(Visit(context.expression())));
        return null;
    }

    public override object VisitReturnStatement(
        SimpleParser.ReturnStatementContext context)
    {
        object value = context.expression() != null
            ? Visit(context.expression())
            : null;

        throw new ReturnSignal(value);
    }

    public override object VisitBreakStatement(
        SimpleParser.BreakStatementContext context)
    {
        throw new BreakSignal();
    }

    public override object VisitContinueStatement(
        SimpleParser.ContinueStatementContext context)
    {
        throw new ContinueSignal();
    }

    // ============================================
    // VARIABLE DECLARATION
    // ============================================

    public override object VisitVariableDeclaration(
        SimpleParser.VariableDeclarationContext context)
    {
        foreach (var declarator in context.variableDeclarator())
            DeclareOne(declarator);

        return null;
    }

    public override object VisitVariableDeclarationNoSemicolon(
        SimpleParser.VariableDeclarationNoSemicolonContext context)
    {
        foreach (var declarator in context.variableDeclarator())
            DeclareOne(declarator);

        return null;
    }

    private void DeclareOne(SimpleParser.VariableDeclaratorContext declarator)
    {
        string name = declarator.variableDeclaratorId().ID().GetText();
        object value = declarator.expression() != null
            ? Visit(declarator.expression())
            : null;

        DeclareVariable(name, value);
    }

    public override object VisitForInit(SimpleParser.ForInitContext context)
    {
        return Visit(context.GetChild(0));
    }

    public override object VisitExpressionList(SimpleParser.ExpressionListContext context)
    {
        foreach (var expr in context.expression())
            Visit(expr);

        return null;
    }

    // ============================================
    // IF / ELSE
    // ============================================

    public override object VisitIfBlock(SimpleParser.IfBlockContext context)
    {
        bool condition = Convert.ToBoolean(Visit(context.expression()));

        if (condition)
        {
            Visit(context.block(0));
            return null;
        }

        if (context.ELSE() == null)
            return null;

        if (context.ifBlock() != null)
        {
            Visit(context.ifBlock());
            return null;
        }

        if (context.block().Length > 1)
            Visit(context.block(1));

        return null;
    }

    // ============================================
    // COROUTINE-AWARE STATEMENT EXECUTION (paced)
    // ============================================
    // Everything below runs the *top-level script* and *user functions
    // called as their own statement* (the normal way to call a helper
    // function in these scripts, e.g. "patrol();"). It mirrors the
    // Visit* methods below almost exactly, except each one returns an
    // IEnumerator instead of executing everything in one shot, so a
    // while/for/etc. only re-checks its condition after its previous
    // iteration's game action has actually happened.
    //
    // Rule used throughout: NEVER "yield return SomeExecutor(...)"
    // directly - that just hands an un-started enumerator up the chain
    // for someone else (eventually Unity itself) to resolve later, which
    // means an exception thrown deep inside (break/continue/return) can
    // no longer be caught by the try/catch here, because this method
    // will have already returned by the time it actually runs. Instead,
    // always create the child, manually pump its MoveNext() in a loop,
    // and relay its Current - that's what makes break/continue/return
    // catchable at the right level, and what guarantees only plain
    // WaitForSeconds/null values ever bubble up to Unity.
    //
    // A function call used INSIDE a larger expression (e.g. "if
    // (isDone())" or "x = compute(5)") still goes through the old
    // synchronous CallUserFunction/Visit path further below - expression
    // evaluation itself isn't coroutine-based. If such a function
    // contains a while-loop full of game actions, that loop will still
    // run instantly. Call action-heavy functions as bare statements, not
    // from inside a condition, to get proper pacing.

    private IEnumerator ExecBlock(SimpleParser.BlockContext context)
    {
        foreach (var item in context.blockItem())
        {
            var child = ExecBlockItem(item);

            while (child.MoveNext())
                yield return child.Current;
        }
    }

    private IEnumerator ExecBlockItem(SimpleParser.BlockItemContext context)
    {
        if (context.statement() != null) return ExecStatement(context.statement());
        if (context.ifBlock() != null) return ExecIfBlock(context.ifBlock());
        if (context.whileBlock() != null) return ExecWhileBlock(context.whileBlock());
        if (context.forBlock() != null) return ExecForBlock(context.forBlock());
        if (context.forEachBlock() != null) return ExecForEachBlock(context.forEachBlock());
        if (context.doWhileBlock() != null) return ExecDoWhileBlock(context.doWhileBlock());
        if (context.switchBlock() != null) return ExecSwitchBlock(context.switchBlock());

        throw new Exception($"Unknown block item: {context.GetText()}");
    }

    private IEnumerator ExecStatement(SimpleParser.StatementContext context)
    {
        if (context.expressionStatement() != null)
        {
            var exprStmt = context.expressionStatement();
            var bareCall = AsBareFunctionCall(exprStmt.expression());

            // A bare call to a USER-DEFINED function, e.g. "patrol();" -
            // run its body through the paced executor too, so loops
            // inside it are correctly paced.
            if (bareCall != null &&
                functions.TryGetValue(bareCall.ID().GetText(), out var userFunc))
            {
                object[] args = EvaluateArguments(bareCall.argumentList());
                var call = CallUserFunctionPaced(userFunc, args);

                while (call.MoveNext())
                    yield return call.Current;
            }
            else
            {
                // Built-in calls, assignments, x++, etc. Any game action
                // this produces lands in pendingActions; flush it before
                // moving on to the next statement.
                Visit(exprStmt.expression());

                var flush = FlushPendingActions();
                while (flush.MoveNext())
                    yield return flush.Current;
            }

            yield break;
        }

        // print / variable declaration / return / break / continue never
        // themselves produce game actions - run them synchronously.
        // (return/break/continue throw here, which is fine: this happens
        // on the very first MoveNext() of this method, so it propagates
        // normally to whoever is driving us.)
        Visit(context);
    }

    // True only when 'expr' is syntactically just a bare call like
    // "foo(1, 2)" - i.e. every wrapping expression rule has exactly one
    // child, all the way down to primaryExpression's functionCall.
    private SimpleParser.FunctionCallContext AsBareFunctionCall(
        SimpleParser.ExpressionContext expr)
    {
        RuleContext node = expr;

        while (node != null && node.ChildCount == 1 && node.GetChild(0) is RuleContext next)
            node = next;

        return node as SimpleParser.FunctionCallContext;
    }

    // Paced counterpart to CallUserFunction, used when a user function is
    // called as its own statement. Discards the return value (there's
    // nowhere for it to go when the call isn't part of an expression).
    private IEnumerator CallUserFunctionPaced(
        SimpleParser.FunctionDeclarationContext function, object[] args)
    {
        var parameters = function.parameterList()?.parameter()
            ?? Array.Empty<SimpleParser.ParameterContext>();

        var frame = new Dictionary<string, object>();

        for (int i = 0; i < parameters.Length; i++)
        {
            string paramName = parameters[i].variableDeclaratorId().ID().GetText();
            frame[paramName] = i < args.Length ? args[i] : null;
        }

        localStack.Add(frame);

        try
        {
            var body = ExecBlock(function.block());

            while (true)
            {
                bool moved;

                try
                {
                    moved = body.MoveNext();
                }
                catch (ReturnSignal)
                {
                    yield break; // return value discarded - called as a statement
                }

                if (!moved)
                    yield break;

                yield return body.Current;
            }
        }
        finally
        {
            localStack.RemoveAt(localStack.Count - 1);
        }
    }

    private IEnumerator ExecIfBlock(SimpleParser.IfBlockContext context)
    {
        bool condition = Convert.ToBoolean(Visit(context.expression()));

        if (condition)
        {
            var body = ExecBlock(context.block(0));
            while (body.MoveNext())
                yield return body.Current;

            yield break;
        }

        if (context.ELSE() == null)
            yield break;

        if (context.ifBlock() != null)
        {
            var elseIf = ExecIfBlock(context.ifBlock());
            while (elseIf.MoveNext())
                yield return elseIf.Current;

            yield break;
        }

        if (context.block().Length > 1)
        {
            var elseBody = ExecBlock(context.block(1));
            while (elseBody.MoveNext())
                yield return elseBody.Current;
        }
    }

    private IEnumerator ExecWhileBlock(SimpleParser.WhileBlockContext context)
    {
        int safety = 0;

        while (Convert.ToBoolean(Visit(context.expression())))
        {
            var body = ExecBlock(context.block());
            bool broke = false;

            while (true)
            {
                bool moved = false;
                bool stop = false;

                try { moved = body.MoveNext(); }
                catch (BreakSignal) { broke = true; stop = true; }
                catch (ContinueSignal) { stop = true; }

                if (stop) break;
                if (!moved) break;

                yield return body.Current;
            }

            if (broke) break;

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }
    }

    private IEnumerator ExecForBlock(SimpleParser.ForBlockContext context)
    {
        if (context.forInit() != null)
            Visit(context.forInit());

        int safety = 0;

        while (true)
        {
            if (context.expression() != null
                && !Convert.ToBoolean(Visit(context.expression())))
                break;

            var body = ExecBlock(context.block());
            bool broke = false;

            while (true)
            {
                bool moved = false;
                bool stop = false;

                try { moved = body.MoveNext(); }
                catch (BreakSignal) { broke = true; stop = true; }
                catch (ContinueSignal) { stop = true; }

                if (stop) break;
                if (!moved) break;

                yield return body.Current;
            }

            if (broke) break;

            if (context.forUpdate() != null)
                Visit(context.forUpdate());

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }
    }

    private IEnumerator ExecForEachBlock(SimpleParser.ForEachBlockContext context)
    {
        object collection = Visit(context.expression());

        if (collection is not object[] items)
        {
            Console.WriteLine("for-each requires an array value.");
            yield break;
        }

        string varName = context.ID().GetText();
        int safety = 0;

        foreach (var item in items)
        {
            DeclareVariable(varName, item);

            var body = ExecBlock(context.block());
            bool broke = false;

            while (true)
            {
                bool moved = false;
                bool stop = false;

                try { moved = body.MoveNext(); }
                catch (BreakSignal) { broke = true; stop = true; }
                catch (ContinueSignal) { stop = true; }

                if (stop) break;
                if (!moved) break;

                yield return body.Current;
            }

            if (broke) break;

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }
    }

    private IEnumerator ExecDoWhileBlock(SimpleParser.DoWhileBlockContext context)
    {
        int safety = 0;

        do
        {
            var body = ExecBlock(context.block());
            bool broke = false;

            while (true)
            {
                bool moved = false;
                bool stop = false;

                try { moved = body.MoveNext(); }
                catch (BreakSignal) { broke = true; stop = true; }
                catch (ContinueSignal) { stop = true; }

                if (stop) break;
                if (!moved) break;

                yield return body.Current;
            }

            if (broke) break;

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }
        while (Convert.ToBoolean(Visit(context.expression())));
    }

    private IEnumerator ExecSwitchBlock(SimpleParser.SwitchBlockContext context)
    {
        object switchValue = Visit(context.expression());
        bool matched = false;

        var items = new List<SimpleParser.BlockItemContext>();

        foreach (var switchCase in context.switchCase())
        {
            if (!matched)
            {
                object caseValue = switchCase.literal() != null
                    ? Visit(switchCase.literal())
                    : ReadQualifiedName(switchCase.qualifiedName());

                if (!ValuesEqual(switchValue, caseValue))
                    continue;

                matched = true;
            }

            items.AddRange(switchCase.blockItem());
        }

        if (!matched && context.defaultCase() != null)
            items.AddRange(context.defaultCase().blockItem());

        foreach (var item in items)
        {
            var child = ExecBlockItem(item);
            bool broke = false;

            while (true)
            {
                bool moved = false;
                bool stop = false;

                try { moved = child.MoveNext(); }
                catch (BreakSignal) { broke = true; stop = true; }

                if (stop) break;
                if (!moved) break;

                yield return child.Current;
            }

            if (broke) yield break;
        }
    }

    // ============================================
    // WHILE (synchronous - only reached from CallUserFunction, for a
    // function called from inside an expression rather than as its own
    // statement; see the note at the top of the coroutine-aware section
    // above)
    // ============================================

    public override object VisitWhileBlock(SimpleParser.WhileBlockContext context)
    {
        int safety = 0;

        while (Convert.ToBoolean(Visit(context.expression())))
        {
            try
            {
                Visit(context.block());
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { /* fall through to re-check condition */ }

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }

        return null;
    }

    // ============================================
    // FOR
    // ============================================

    public override object VisitForBlock(SimpleParser.ForBlockContext context)
    {
        if (context.forInit() != null)
            Visit(context.forInit());

        int safety = 0;

        while (true)
        {
            if (context.expression() != null
                && !Convert.ToBoolean(Visit(context.expression())))
                break;

            try
            {
                Visit(context.block());
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { /* still run the update step below */ }

            if (context.forUpdate() != null)
                Visit(context.forUpdate());

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }

        return null;
    }

    public override object VisitForUpdate(SimpleParser.ForUpdateContext context)
    {
        return Visit(context.expressionList());
    }

    // ============================================
    // FOR-EACH
    // ============================================

    public override object VisitForEachBlock(SimpleParser.ForEachBlockContext context)
    {
        object collection = Visit(context.expression());

        if (collection is not object[] items)
        {
            Console.WriteLine("for-each requires an array value.");
            return null;
        }

        string varName = context.ID().GetText();
        int safety = 0;

        foreach (var item in items)
        {
            DeclareVariable(varName, item);

            try
            {
                Visit(context.block());
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { /* next item */ }

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }

        return null;
    }

    // ============================================
    // DO-WHILE
    // ============================================

    public override object VisitDoWhileBlock(SimpleParser.DoWhileBlockContext context)
    {
        int safety = 0;

        do
        {
            try
            {
                Visit(context.block());
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { /* fall through to condition check */ }

            if (++safety > 1_000_000)
            {
                Console.WriteLine("Possible infinite loop.");
                break;
            }
        }
        while (Convert.ToBoolean(Visit(context.expression())));

        return null;
    }

    // ============================================
    // SWITCH
    // ============================================

    public override object VisitSwitchBlock(SimpleParser.SwitchBlockContext context)
    {
        object switchValue = Visit(context.expression());
        bool matched = false;

        try
        {
            foreach (var switchCase in context.switchCase())
            {
                if (!matched)
                {
                    object caseValue = switchCase.literal() != null
                        ? Visit(switchCase.literal())
                        : ReadQualifiedName(switchCase.qualifiedName());

                    if (!ValuesEqual(switchValue, caseValue))
                        continue;

                    matched = true;
                }

                foreach (var item in switchCase.blockItem())
                    Visit(item);
            }

            if (!matched && context.defaultCase() != null)
            {
                foreach (var item in context.defaultCase().blockItem())
                    Visit(item);
            }
        }
        catch (BreakSignal)
        {
            // break exits the switch
        }

        return null;
    }

    // ============================================
    // EXPRESSION (entry point)
    // ============================================

    public override object VisitExpression(SimpleParser.ExpressionContext context)
    {
        return Visit(context.assignmentExpression());
    }

    // ============================================
    // ASSIGNMENT
    // ============================================

    public override object VisitAssignmentExpression(
        SimpleParser.AssignmentExpressionContext context)
    {
        if (context.assignmentTarget() == null)
            return Visit(context.conditionalExpression());

        var target = context.assignmentTarget();
        string op = context.assignmentOperator().GetText();
        object rightValue = Visit(context.assignmentExpression());

        object newValue;

        if (op == "=")
        {
            newValue = rightValue;
        }
        else
        {
            object currentValue = ReadTarget(target);
            string binaryOp = op.Substring(0, op.Length - 1); // "+=" -> "+"
            newValue = EvaluateBinary(currentValue, binaryOp, rightValue);
        }

        WriteTarget(target, newValue);
        return newValue;
    }

    private object ReadTarget(SimpleParser.AssignmentTargetContext ctx)
    {
        return ctx.qualifiedName() != null
            ? ReadQualifiedName(ctx.qualifiedName())
            : ReadArrayAccess(ctx.arrayAccess());
    }

    private void WriteTarget(SimpleParser.AssignmentTargetContext ctx, object value)
    {
        if (ctx.qualifiedName() != null)
        {
            var idNodes = ctx.qualifiedName().ID();

            if (idNodes.Length != 1)
                throw new Exception(
                    $"Cannot assign to '{ctx.qualifiedName().GetText()}'.");

            SetVariable(idNodes[0].GetText(), value);
        }
        else
        {
            WriteArrayAccess(ctx.arrayAccess(), value);
        }
    }

    // ============================================
    // TERNARY
    // ============================================

    public override object VisitConditionalExpression(
        SimpleParser.ConditionalExpressionContext context)
    {
        if (context.expression() == null)
            return Visit(context.logicalOrExpression());

        bool condition = Convert.ToBoolean(Visit(context.logicalOrExpression()));
        return condition
            ? Visit(context.expression())
            : Visit(context.conditionalExpression());
    }

    // ============================================
    // LOGICAL OR / AND (short-circuiting)
    // ============================================

    public override object VisitLogicalOrExpression(
        SimpleParser.LogicalOrExpressionContext context)
    {
        if (context.logicalOrExpression() == null)
            return Visit(context.logicalAndExpression());

        if (Convert.ToBoolean(Visit(context.logicalOrExpression())))
            return true;

        return Convert.ToBoolean(Visit(context.logicalAndExpression()));
    }

    public override object VisitLogicalAndExpression(
        SimpleParser.LogicalAndExpressionContext context)
    {
        if (context.logicalAndExpression() == null)
            return Visit(context.bitwiseOrExpression());

        if (!Convert.ToBoolean(Visit(context.logicalAndExpression())))
            return false;

        return Convert.ToBoolean(Visit(context.bitwiseOrExpression()));
    }

    // ============================================
    // BITWISE OR / XOR / AND
    // ============================================

    public override object VisitBitwiseOrExpression(
        SimpleParser.BitwiseOrExpressionContext context)
    {
        if (context.bitwiseOrExpression() == null)
            return Visit(context.bitwiseXorExpression());

        object left = Visit(context.bitwiseOrExpression());
        object right = Visit(context.bitwiseXorExpression());
        return EvaluateBinary(left, "|", right);
    }

    public override object VisitBitwiseXorExpression(
        SimpleParser.BitwiseXorExpressionContext context)
    {
        if (context.bitwiseXorExpression() == null)
            return Visit(context.bitwiseAndExpression());

        object left = Visit(context.bitwiseXorExpression());
        object right = Visit(context.bitwiseAndExpression());
        return EvaluateBinary(left, "^", right);
    }

    public override object VisitBitwiseAndExpression(
        SimpleParser.BitwiseAndExpressionContext context)
    {
        if (context.bitwiseAndExpression() == null)
            return Visit(context.equalityExpression());

        object left = Visit(context.bitwiseAndExpression());
        object right = Visit(context.equalityExpression());
        return EvaluateBinary(left, "&", right);
    }

    // ============================================
    // EQUALITY / RELATIONAL / SHIFT / ADDITIVE / MULTIPLICATIVE
    // ============================================

    public override object VisitEqualityExpression(
        SimpleParser.EqualityExpressionContext context)
    {
        if (context.equalityExpression() == null)
            return Visit(context.relationalExpression());

        object left = Visit(context.equalityExpression());
        object right = Visit(context.relationalExpression());
        string op = context.GetChild(1).GetText();
        return EvaluateBinary(left, op, right);
    }

    public override object VisitRelationalExpression(
        SimpleParser.RelationalExpressionContext context)
    {
        if (context.relationalExpression() == null)
            return Visit(context.shiftExpression());

        object left = Visit(context.relationalExpression());
        object right = Visit(context.shiftExpression());
        string op = context.GetChild(1).GetText();
        return EvaluateBinary(left, op, right);
    }

    public override object VisitShiftExpression(
        SimpleParser.ShiftExpressionContext context)
    {
        if (context.shiftExpression() == null)
            return Visit(context.additiveExpression());

        object left = Visit(context.shiftExpression());
        object right = Visit(context.additiveExpression());
        string op = context.GetChild(1).GetText();
        return EvaluateBinary(left, op, right);
    }

    public override object VisitAdditiveExpression(
        SimpleParser.AdditiveExpressionContext context)
    {
        if (context.additiveExpression() == null)
            return Visit(context.multiplicativeExpression());

        object left = Visit(context.additiveExpression());
        object right = Visit(context.multiplicativeExpression());
        string op = context.GetChild(1).GetText();
        return EvaluateBinary(left, op, right);
    }

    public override object VisitMultiplicativeExpression(
        SimpleParser.MultiplicativeExpressionContext context)
    {
        if (context.multiplicativeExpression() == null)
            return Visit(context.unaryExpression());

        object left = Visit(context.multiplicativeExpression());
        object right = Visit(context.unaryExpression());
        string op = context.GetChild(1).GetText();
        return EvaluateBinary(left, op, right);
    }

    // ============================================
    // UNARY
    // ============================================

    public override object VisitUnaryExpression(
        SimpleParser.UnaryExpressionContext context)
    {
        if (context.ChildCount == 1)
            return Visit(context.postfixExpression());

        string op = context.GetChild(0).GetText();

        switch (op)
        {
            case "!":
                return !Convert.ToBoolean(Visit(context.unaryExpression()));

            case "+":
                return Visit(context.unaryExpression());

            case "-":
                return Negate(Visit(context.unaryExpression()));

            case "~":
                return ~Convert.ToInt32(Visit(context.unaryExpression()));

            case "++":
                {
                    object oldValue = ReadTarget(context.assignmentTarget());
                    object newValue = Increment(oldValue);
                    WriteTarget(context.assignmentTarget(), newValue);
                    return newValue;
                }

            case "--":
                {
                    object oldValue = ReadTarget(context.assignmentTarget());
                    object newValue = Decrement(oldValue);
                    WriteTarget(context.assignmentTarget(), newValue);
                    return newValue;
                }

            default:
                throw new Exception($"Unknown unary operator: {op}");
        }
    }

    // ============================================
    // POSTFIX (x++, x--, arr[i], arr.length)
    // ============================================

    public override object VisitPostfixExpression(
        SimpleParser.PostfixExpressionContext context)
    {
        if (context.ChildCount == 1)
            return Visit(context.primaryExpression());

        string lastText = context.GetChild(context.ChildCount - 1).GetText();

        if (lastText == "++" || lastText == "--")
        {
            var inner = context.postfixExpression();
            object oldValue = GetPostfixLValue(inner, out Action<object> setter);
            object newValue = lastText == "++" ? Increment(oldValue) : Decrement(oldValue);
            setter(newValue);
            return oldValue; // postfix returns the OLD value
        }

        if (context.GetChild(1).GetText() == "[")
        {
            if (Visit(context.postfixExpression()) is not object[] arr)
                throw new Exception(
                    $"'{context.postfixExpression().GetText()}' is not an array.");

            int index = Convert.ToInt32(Visit(context.expression()));
            return arr[index];
        }

        if (context.GetChild(1).GetText() == ".")
        {
            object baseValue = Visit(context.postfixExpression());
            string member = context.ID().GetText();

            if (member == "length" && baseValue is object[] arr)
                return arr.Length;

            throw new Exception($"Unknown member access: .{member}");
        }

        throw new Exception($"Unrecognized expression: {context.GetText()}");
    }

    // Resolves a postfix-expression operand of ++/-- to its current value
    // plus a setter that writes back to wherever it came from
    // (a plain variable, or one slot of an array).
    private object GetPostfixLValue(
        SimpleParser.PostfixExpressionContext ctx, out Action<object> setter)
    {
        if (ctx.ChildCount == 1)
        {
            var primary = ctx.primaryExpression();

            if (primary.qualifiedName() != null && primary.qualifiedName().ID().Length == 1)
            {
                string name = primary.qualifiedName().ID(0).GetText();
                object value = ReadQualifiedName(primary.qualifiedName());
                setter = v => SetVariable(name, v);
                return value;
            }

            throw new Exception($"Cannot increment/decrement: {ctx.GetText()}");
        }

        if (ctx.GetChild(1).GetText() == "[")
        {
            if (Visit(ctx.postfixExpression()) is not object[] arr)
                throw new Exception(
                    $"'{ctx.postfixExpression().GetText()}' is not an array.");

            int index = Convert.ToInt32(Visit(ctx.expression()));
            object value = arr[index];
            setter = v => arr[index] = v;
            return value;
        }

        throw new Exception($"Cannot increment/decrement: {ctx.GetText()}");
    }

    // ============================================
    // PRIMARY EXPRESSIONS
    // ============================================

    public override object VisitPrimaryExpression(
        SimpleParser.PrimaryExpressionContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());

        if (context.functionCall() != null)
            return Visit(context.functionCall());

        if (context.qualifiedName() != null)
            return ReadQualifiedName(context.qualifiedName());

        if (context.arrayCreation() != null)
            return Visit(context.arrayCreation());

        if (context.arrayInitializer() != null)
            return Visit(context.arrayInitializer());

        // '(' expression ')'
        return Visit(context.expression());
    }

    /*
        private object ReadQualifiedName(SimpleParser.QualifiedNameContext ctx)
        {
            var ids = ctx.ID();

            if (ids.Length == 1)
            {
                string name = ids[0].GetText();

                if (TryGetVariable(name, out object value))
                    return value;

                Console.WriteLine($"Unknown variable: {name}");
                return null;
            }

            // Dotted name with no matching variable -> treat as a symbolic
            // constant, e.g. Entities.Pumpkin, Items.Water.
            var parts = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                parts[i] = ids[i].GetText();

            return string.Join(".", parts);
        }
    */
    private object ReadQualifiedName(SimpleParser.QualifiedNameContext ctx)
    {
        var ids = ctx.ID();

        // Normal variable
        if (ids.Length == 1)
        {
            string name = ids[0].GetText();

            if (TryGetVariable(name, out object value))
                return value;

            Console.WriteLine($"Unknown variable: {name}");
            return null;
        }

        // ------------------------------------------------
        // ARRAY.length
        // ------------------------------------------------

        if (ids.Length == 2 &&
            ids[1].GetText() == "length")
        {
            string arrayName = ids[0].GetText();

            if (TryGetVariable(arrayName, out object value))
            {
                if (value is object[] array)
                    return array.Length;

                throw new Exception(
                    $"'{arrayName}' is not an array.");
            }
        }

        // ------------------------------------------------
        // Dotted symbolic values
        // Example:
        // Entities.Pumpkin
        // Items.Water
        // Grounds.Soil
        // ------------------------------------------------

        var parts = new string[ids.Length];

        for (int i = 0; i < ids.Length; i++)
        {
            parts[i] = ids[i].GetText();
        }

        return string.Join(".", parts);
    }
    // ============================================
    // ARRAYS
    // ============================================

    public override object VisitArrayCreation(SimpleParser.ArrayCreationContext context)
    {
        var dims = context.arrayCreationDimensions().arrayDimension();

        // new int[] {1, 2, 3}
        if (context.arrayInitializer() != null)
            return Visit(context.arrayInitializer());

        // new int[5] or new int[3][4]
        var sizes = new int[dims.Length];

        for (int i = 0; i < dims.Length; i++)
        {
            var sizeExpr = dims[i].expression();

            if (sizeExpr == null)
                throw new Exception(
                    "Array size is required when no initializer is given.");

            sizes[i] = Convert.ToInt32(Visit(sizeExpr));
        }

        object defaultValue = DefaultValueFor(context.baseType());
        return CreateArray(sizes, 0, defaultValue);
    }

    private object CreateArray(int[] sizes, int dimIndex, object defaultValue)
    {
        int size = sizes[dimIndex];
        var array = new object[size];

        if (dimIndex == sizes.Length - 1)
        {
            for (int i = 0; i < size; i++)
                array[i] = defaultValue;
        }
        else
        {
            for (int i = 0; i < size; i++)
                array[i] = CreateArray(sizes, dimIndex + 1, defaultValue);
        }

        return array;
    }

    private object DefaultValueFor(SimpleParser.BaseTypeContext baseType)
    {
        return baseType.GetText() switch
        {
            "int" => 0,
            "long" => 0L,
            "double" => 0.0,
            "float" => 0f,
            "short" => (short)0,
            "byte" => (byte)0,
            "boolean" => false,
            "char" => '\0',
            _ => null, // String and anything else defaults to null
        };
    }

    public override object VisitArrayInitializer(
        SimpleParser.ArrayInitializerContext context)
    {
        if (context.expressionList() == null)
            return Array.Empty<object>();

        var expressions = context.expressionList().expression();
        var result = new object[expressions.Length];

        for (int i = 0; i < expressions.Length; i++)
            result[i] = Visit(expressions[i]);

        return result;
    }

    private object ReadArrayAccess(SimpleParser.ArrayAccessContext ctx)
    {
        var array = ResolveArrayContainer(ctx, out int index);
        return array[index];
    }

    private void WriteArrayAccess(SimpleParser.ArrayAccessContext ctx, object value)
    {
        var array = ResolveArrayContainer(ctx, out int index);
        array[index] = value;
    }

    // Walks all but the last '[' expr ']' to find the innermost array,
    // and returns that array plus the final index to read/write.
    private object[] ResolveArrayContainer(
        SimpleParser.ArrayAccessContext ctx, out int lastIndex)
    {
        var idNodes = ctx.qualifiedName().ID();

        if (idNodes.Length != 1)
            throw new Exception(
                $"Cannot index '{ctx.qualifiedName().GetText()}'.");

        string name = idNodes[0].GetText();

        if (!TryGetVariable(name, out object current))
            throw new Exception($"Unknown array: {name}");

        var indices = ctx.expression();

        for (int i = 0; i < indices.Length - 1; i++)
        {
            if (current is not object[] arr)
                throw new Exception($"'{name}' is not an array.");

            int idx = Convert.ToInt32(Visit(indices[i]));
            current = arr[idx];
        }

        if (current is not object[] finalArray)
            throw new Exception($"'{name}' is not an array.");

        lastIndex = Convert.ToInt32(Visit(indices[^1]));
        return finalArray;
    }

    // ============================================
    // LITERALS
    // ============================================

    public override object VisitLiteral(SimpleParser.LiteralContext context)
    {
        if (context.INT() != null)
            return int.Parse(context.INT().GetText());

        if (context.LONG() != null)
        {
            string text = context.LONG().GetText();
            return long.Parse(text[..^1]); // trim trailing l/L
        }

        if (context.DOUBLE() != null)
            return double.Parse(context.DOUBLE().GetText(), CultureInfo.InvariantCulture);

        if (context.FLOAT() != null)
        {
            string text = context.FLOAT().GetText();
            return float.Parse(text[..^1], CultureInfo.InvariantCulture); // trim f/F
        }

        if (context.STRING() != null)
        {
            string raw = context.STRING().GetText();
            return Unescape(raw.Substring(1, raw.Length - 2));
        }

        if (context.CHAR() != null)
        {
            string raw = context.CHAR().GetText();
            string inner = Unescape(raw.Substring(1, raw.Length - 2));
            return inner[0];
        }

        if (context.TRUE() != null) return true;
        if (context.FALSE() != null) return false;
        if (context.NULL() != null) return null;

        throw new Exception($"Unknown literal: {context.GetText()}");
    }

    private string Unescape(string raw)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                i++;
                sb.Append(raw[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\'' => '\'',
                    '\\' => '\\',
                    _ => raw[i],
                });
            }
            else
            {
                sb.Append(raw[i]);
            }
        }

        return sb.ToString();
    }

    // ============================================
    // BINARY OPERATORS
    // ============================================

    private object EvaluateBinary(object left, string op, object right)
    {
        switch (op)
        {
            case "+":
                if (left is string || right is string)
                    return Stringify(left) + Stringify(right);
                return NumericOp(left, right, (a, b) => a + b, (a, b) => a + b);

            case "-":
                return NumericOp(left, right, (a, b) => a - b, (a, b) => a - b);

            case "*":
                return NumericOp(left, right, (a, b) => a * b, (a, b) => a * b);

            case "/":
                return NumericOp(left, right, (a, b) => a / b, (a, b) => a / b);

            case "%":
                return NumericOp(left, right, (a, b) => a % b, (a, b) => a % b);

            case "<":
                return Convert.ToDouble(left) < Convert.ToDouble(right);
            case ">":
                return Convert.ToDouble(left) > Convert.ToDouble(right);
            case "<=":
                return Convert.ToDouble(left) <= Convert.ToDouble(right);
            case ">=":
                return Convert.ToDouble(left) >= Convert.ToDouble(right);

            case "==":
                return ValuesEqual(left, right);
            case "!=":
                return !ValuesEqual(left, right);

            case "&&":
                return Convert.ToBoolean(left) && Convert.ToBoolean(right);
            case "||":
                return Convert.ToBoolean(left) || Convert.ToBoolean(right);

            case "&":
                return Convert.ToInt32(left) & Convert.ToInt32(right);
            case "|":
                return Convert.ToInt32(left) | Convert.ToInt32(right);
            case "^":
                return Convert.ToInt32(left) ^ Convert.ToInt32(right);

            case "<<":
                return Convert.ToInt32(left) << Convert.ToInt32(right);
            case ">>":
                return Convert.ToInt32(left) >> Convert.ToInt32(right);
            case ">>>":
                return (int)((uint)Convert.ToInt32(left) >> Convert.ToInt32(right));

            default:
                throw new Exception($"Unknown operator: {op}");
        }
    }

    private object NumericOp(
        object left, object right,
        Func<double, double, double> doubleOp,
        Func<int, int, int> intOp)
    {
        if (left is double || right is double || left is float || right is float)
            return doubleOp(Convert.ToDouble(left), Convert.ToDouble(right));

        if (left is long || right is long)
            return (long)doubleOp(Convert.ToDouble(left), Convert.ToDouble(right));

        return intOp(Convert.ToInt32(left), Convert.ToInt32(right));
    }

    private bool ValuesEqual(object a, object b)
    {
        if (a == null || b == null)
            return Equals(a, b);

        if (IsNumeric(a) && IsNumeric(b))
            return Convert.ToDouble(a) == Convert.ToDouble(b);

        return Equals(a, b);
    }

    private bool IsNumeric(object value) =>
        value is int or long or double or float or short or byte;

    private object Increment(object value) => value switch
    {
        int i => i + 1,
        long l => l + 1,
        double d => d + 1,
        float f => f + 1,
        _ => Convert.ToInt32(value) + 1,
    };

    private object Decrement(object value) => value switch
    {
        int i => i - 1,
        long l => l - 1,
        double d => d - 1,
        float f => f - 1,
        _ => Convert.ToInt32(value) - 1,
    };

    private object Negate(object value) => value switch
    {
        int i => -i,
        long l => -l,
        double d => -d,
        float f => -f,
        _ => -Convert.ToInt32(value),
    };

    private string Stringify(object value) => value switch
    {
        null => "null",
        object[] arr => "[" + string.Join(", ", Array.ConvertAll(arr, Stringify)) + "]",
        _ => value.ToString(),
    };
}