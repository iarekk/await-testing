// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

namespace cancellation_token_disposed_early;

static class Program
{
    // Running this produces the following output, despite the cancellation token set for 1000ms
    // The execution doesn't cancel, and, in fact, continues running
    //
    //     Execution result: task 1 - 1800
    //     Elapsed time: 1826ms
    public static async Task Main()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        Task t;
        string result = "initial value";
        
        if (true) // removing lines 22, 23 and 31 - so the code below isn't wrapped
        {         //     - will ensure that the CTS is disposed at the end of the method, when the task is awaited
            // this 'using' is causing the issue
            // since the 'using' is wrapped inside a conditional block, cts is exposed at line 30
            // this means that it won't cancel the linked cancellation token source
            using var cts = new CancellationTokenSource(1000);
            var cancellationToken = cts.Token;

            t = Task.Run(async () => { result = await LongRunningOperation(cancellationToken); }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(t);
            Console.WriteLine("Execution result: {0}", result);
        }
        catch(OperationCanceledException)
        {
            Console.WriteLine("Operation canceled");
        }

        sw.Stop();
        Console.WriteLine("Elapsed time: {0}ms", sw.ElapsedMilliseconds);
    }

    private static async Task<string> LongRunningOperation(CancellationToken outerCt)
    {
        // this linked cancellation token source will not be canceled,
        // as the outer cancellation token source will have been disposed before being canceled
        // so the tasks working off linkedCts will never be canceled
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        
        return await RunTasks(linkedCts);
    }

    private static async Task<string> RunTasks(CancellationTokenSource linkedCts)
    {
        // this method will never be interrupted, and will run it's full duration - at least 1800ms
        var t1 = WaitFor(1800, "task 1 - 1800", linkedCts.Token);
        var t2 = WaitFor(1950,"task 2 - 1950", linkedCts.Token);
        var t3 = WaitFor(2000, "task 3 - 2000", linkedCts.Token);
        var taskArray = new Task<string>[] { t1, t2, t3 };
        await Task.WhenAny(taskArray);
        linkedCts.Cancel();
        linkedCts.Dispose();

        return taskArray.FirstOrDefault(t => t.IsCompletedSuccessfully)?.Result ?? "no task completed";
    }

    private static async Task<string> WaitFor(int delay, string returnVal, CancellationToken ct)
    {
        await Task.Run(async() =>
        {
            await Task.Delay(delay, ct);
        }, ct);
        return returnVal;
    }
}