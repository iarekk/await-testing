using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AwaitTesting
{
    class Program
    {
        private static readonly CancellationTokenSource Cts = new();

        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Cancel event triggered");
                Cts.Cancel();
                eventArgs.Cancel = true;
            };

            var numberOfCycles = 50000;

            Console.WriteLine("Invoking a CPU/IO method that takes about 1200ms CPU work and 1000ms IO work");
            await CpuThenIo_DiscardTask(numberOfCycles);
            Console.WriteLine("NOTE that outer exited after CPU but before IO");
            await Task.Delay(5000);
            Console.WriteLine("------------------------------------------------------------");
            await CpuThenIo_TaskRunAndDiscard(numberOfCycles);
            Console.WriteLine("NOTE that outer exited before both CPU and IO, which is correct for Fire & Forget");


            // Some intermediate bits for deeper testing below:
            // the console traces are cosmetically different from the comments as I added bits and pieces

            // hangs this thread until CPU work is done
            // Starting outer
            //     * * * * Starting CPU
            //     * * * * Finished CPU: 1169
            // Finished outer: 1170
            // await CpuOnly_DiscardTask(numberOfCycles);

            // schedules the task and exits within 1ms
            // Starting outer
            // Finished outer: 1
            // * * * * Starting CPU
            // * * * * Finished CPU: 1251
            // await CpuWorkOnly_TaskRun_Discard(numberOfCycles);

            // basic implementation of the await - waits until both CPU and the IO 
            // Starting outer
            // * * Starting inner
            // * * * * Starting CPU
            // * * * * Finished CPU: 1214
            // * * Finished inner: 2216
            // Finished outer: 2217
            // await CpuThenIo(numberOfCycles);

            // with task discard, waits until the CPU-bound work is gone
            // THEN continues the execution of this thread which the IO call finishes in the background
            // Starting outer
            // * * Starting inner
            // * * * * Starting CPU
            // * * * * Finished CPU: 1204
            // * * * * Starting IO
            // Finished outer: 1207
            // * * * * Finished IO
            // * * Finished inner: 2207
            // await CpuThenIo_DiscardTask(numberOfCycles);

            // awaiting the CPU and IO, note this does not require .Unwrap()
            // Starting outer
            // * * Starting inner
            // * * * * Starting CPU
            // * * * * Finished CPU: 1271
            // * * * * Starting IO
            // * * * * Finished IO
            // * * Finished inner: 2276
            // Finished outer: 2280
            // await CpuThenIo_AwaitTaskRun(numberOfCycles);

            // Task.Run that is not awaited - correctly does Fire and Forget
            // Starting outer
            // Finished outer: 1ms
            // * * Starting inner
            // * * * * Starting CPU
            // * * * * Finished CPU: 1254ms
            // * * * * Starting IO
            // * * * * Finished IO 2256ms
            // * * Finished inner: 2256ms

            // await CpuThenIo_TaskRunAndDiscard(numberOfCycles);

            WaitHandle.WaitAny(new[] { Cts.Token.WaitHandle }, TimeSpan.FromSeconds(5));
        }

        private static async Task CpuThenIo_TaskRunAndDiscard(int numberOfCycles)
        {
            Console.WriteLine(@"RIGHT: `_ = Task.Run(() => CpuThenIo(numberOfCycles), canToken.Token);`");
            Console.WriteLine("Starting outer");
            var sw = new Stopwatch();
            sw.Start();

            _ = Task.Run(() => CpuThenIo(numberOfCycles), Cts.Token);

            sw.Stop();
            Console.WriteLine("Finished outer: {0}ms", sw.ElapsedMilliseconds);
        }

        private static async Task CpuThenIo_AwaitTaskRun(int numberOfCycles)
        {
            Console.WriteLine("Starting outer");
            var sw = new Stopwatch();
            sw.Start();

            await Task.Run(() => CpuThenIo(numberOfCycles), Cts.Token);


            sw.Stop();
            Console.WriteLine("Finished outer: {0}ms", sw.ElapsedMilliseconds);
        }

        private static async Task CpuThenIo_DiscardTask(int numberOfCycles)
        {
            Console.WriteLine(@"WRONG: `_ = CpuThenIo(numberOfCycles);`");
            Console.WriteLine("Starting outer");
            var sw = new Stopwatch();
            sw.Start();

            _ = CpuThenIo(numberOfCycles);

            sw.Stop();
            Console.WriteLine("Finished outer: {0}ms", sw.ElapsedMilliseconds);
        }

        private static async Task CpuWorkOnly_TaskRun_Discard(int numberOfCycles)
        {
            Console.WriteLine("Starting outer");
            var sw = new Stopwatch();
            sw.Start();

            _ = Task.Run(() => CpuBoundWork(numberOfCycles));


            sw.Stop();
            Console.WriteLine("Finished outer: {0}ms", sw.ElapsedMilliseconds);
        }

        private static async Task CpuOnly_DiscardTask(int numberOfCycles)
        {
            Console.WriteLine("Starting outer");
            var sw = new Stopwatch();
            sw.Start();

            _ = CpuBoundWork(numberOfCycles);


            sw.Stop();
            Console.WriteLine("Finished outer: {0}ms", sw.ElapsedMilliseconds);
        }

        private static async Task<int> CpuThenIo(int numberOfCycles)
        {
            var sw = new Stopwatch();
            Console.WriteLine("* * Starting inner");
            sw.Start();

            await CpuBoundWork(numberOfCycles);

            Console.WriteLine("* * * * Starting IO");
            await Task.Delay(1000); // simulates the IO call
            Console.WriteLine("* * * * Finished IO {0}ms", sw.ElapsedMilliseconds);

            sw.Stop();

            Console.WriteLine("* * Finished inner: {0}ms", sw.ElapsedMilliseconds);
            return await Task.FromResult(300); // random value, doesn't actually matter
        }

        private static async Task CpuBoundWork(int numberOfCycles)
        {
            var sw = new Stopwatch();
            //await Task.Delay(1);
            Console.WriteLine("* * * * Starting CPU");
            sw.Start();

            // this is designed to load the CPU for about 1200ms on my MacBook Pro CPU
            string s = "";
            for (int i = 0; i < numberOfCycles; i++)
            {
                s += i.ToString();
            }


            sw.Stop();

            Console.WriteLine("* * * * Finished CPU: {0}ms", sw.ElapsedMilliseconds);
        }
    }
}