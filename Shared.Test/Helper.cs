using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Shared.Test
{
    public static class Helper
    {
        public class Clock
        {
            interface IStopwatch
            {
                bool IsRunning { get; }
                TimeSpan Elapsed { get; }

                void Start();
                void Stop();
                void Reset();
            }

            class TimeWatch : IStopwatch
            {
                Stopwatch stopwatch = new Stopwatch();

                public TimeSpan Elapsed => stopwatch.Elapsed;

                public bool IsRunning => stopwatch.IsRunning;


                public TimeWatch()
                {
                    if (!Stopwatch.IsHighResolution)
                        throw new NotSupportedException("Your hardware doesn't support high resolution counter");

                    //prevent the JIT Compiler from optimizing Fkt calls away
                    long seed = Environment.TickCount;

                    //use the second Core/Processor for the test
                    Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);

                    //prevent "Normal" Processes from interrupting Threads
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

                    //prevent "Normal" Threads from interrupting this thread
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                }



                public void Start()
                {
                    stopwatch.Start();
                }

                public void Stop()
                {
                    stopwatch.Stop();
                }

                public void Reset()
                {
                    stopwatch.Reset();
                }
            }

            class CpuWatch : IStopwatch
            {
                TimeSpan startTime;
                TimeSpan endTime;


                public TimeSpan Elapsed
                {
                    get
                    {
                        if (IsRunning)
                            throw new NotImplementedException("Getting elapsed span while watch is running is not implemented");

                        return endTime - startTime;
                    }
                }

                public bool IsRunning { get; private set; }


                public void Start()
                {
                    startTime = Process.GetCurrentProcess().TotalProcessorTime;
                    IsRunning = true;
                }

                public void Stop()
                {
                    endTime = Process.GetCurrentProcess().TotalProcessorTime;
                    IsRunning = false;
                }

                public void Reset()
                {
                    startTime = TimeSpan.Zero;
                    endTime = TimeSpan.Zero;
                }
            }

            static void Benchmark<T>(Action action, int iterations) where T : IStopwatch, new()
            {
                //clean Garbage
                GC.Collect();

                //wait for the finalizer queue to empty
                GC.WaitForPendingFinalizers();

                //clean Garbage
                GC.Collect();

                //warm up
                action();

                var stopwatch = new T();
                var timings = new double[5];
                for (int i = 0; i < timings.Length; i++)
                {
                    stopwatch.Reset();
                    stopwatch.Start();
                    for (int j = 0; j < iterations; j++)
                        action();
                    stopwatch.Stop();
                    timings[i] = stopwatch.Elapsed.TotalMilliseconds;
                    Console.WriteLine(timings[i]);
                }
                Console.WriteLine("normalized mean: " + NormalizedMean(timings));
            }

            public static void BenchmarkTime(Action action, int iterations = 10000)
            {
                Benchmark<TimeWatch>(action, iterations);
            }

            public static void BenchmarkCpu(Action action, int iterations = 10000)
            {
                Benchmark<CpuWatch>(action, iterations);
            }

            private static double NormalizedMean(ICollection<double> values)
            {
                if (values.Count == 0)
                    return double.NaN;

                var deviations = Deviations(values).ToArray();
                var meanDeviation = deviations.Sum(t => Math.Abs(t.Item2)) / values.Count;
                return deviations.Where(t => t.Item2 > 0 || Math.Abs(t.Item2) <= meanDeviation).Average(t => t.Item1);
            }

            private static IEnumerable<Tuple<double, double>> Deviations(ICollection<double> values)
            {
                if (values.Count == 0)
                    yield break;

                var avg = values.Average();
                foreach (var d in values)
                    yield return Tuple.Create(d, avg - d);
            }

        }

        public static object GetMethod<T>(this T instance, string methodName, params object[] parametres)
        {
            return typeof(T).GetRuntimeMethods().FirstOrDefault(s => s.Name.Equals(methodName))?.Invoke(instance, parametres);
        }

        public static object GetGenericMethod<T, TGen>(this T instance, string methodName, params object[] parametres)
        {
            return typeof(T).GetRuntimeMethods().FirstOrDefault(s => s.Name.Equals(methodName))?.MakeGenericMethod(typeof(TGen)).Invoke(instance, parametres);
        }

        public static object GetField<T>(this T instance, string fieldName)
        {
            return typeof(T).GetRuntimeFields().FirstOrDefault(s => s.Name.Equals(fieldName))?.GetValue(instance);
        }

    }


}
