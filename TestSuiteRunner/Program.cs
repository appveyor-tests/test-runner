using Newtonsoft.Json;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TestSuiteRunner
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Running test suite");

            int maxConcurrency = Int32.TryParse(Environment.GetEnvironmentVariable("TESTS_CONCURRENCY"), out int mc) ? mc : 1;

            // load test items
            var testSuite = GetVariable("TEST_SUITE");
            GetVariable("TEST_CLOUD");
            GetVariable("TEST_IMAGE");

            var binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var suitePath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "test-suites", $"{testSuite}.json"), binDir);
            var exclusionsPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "test-suites", "exclusions.json"), binDir);
            var overridesPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "test-suites", "overrrides.json"), binDir);
            var suiteFailed = false;

            if (!File.Exists(suitePath))
            {
                Console.WriteLine($"Test suite {suitePath} not found.");
                Environment.Exit(1);
            }

            var tests = JsonConvert.DeserializeObject<TestItem[]>(File.ReadAllText(suitePath));
            var exclusionsDict = JsonConvert.DeserializeObject<Dictionary<string, TestItem[]>>(File.ReadAllText(exclusionsPath));
            var exclusions = exclusionsDict[testSuite];
            var overridesDict = JsonConvert.DeserializeObject<Dictionary<string, TestItem[]>>(File.ReadAllText(overridesPath));
            var overrides = overridesDict[testSuite];

            // add all tests to AppVeyor
            // first override if existing
            foreach (var ovrd in overrides)
            {
                int index = Array.FindIndex(tests, t => t.TestName == ovrd.TestName);
                if (index != -1) tests[index] = ovrd;
            }
            // filter via linq for exceptions here
            foreach(var test in tests.Except(exclusions))
            {
                await BuildWorkerApi.AddTest(test.TestName);
            }

            int testNum = 1;

            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrency))
            {
                List<Task> tasks = new List<Task>();

                //filter via linq for exceptions here
                foreach (var test in tests)
                {
                    concurrencySemaphore.Wait();

                    Console.WriteLine($"Running test [{testNum++}/{tests.Length}]");
                    var worker = new TestBuildWorker(test);
                    tasks.Add(worker.Start().ContinueWith(ct =>
                    {
                        concurrencySemaphore.Release();
                        if (ct.IsFaulted)
                        {
                            suiteFailed = true;
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }

            return suiteFailed ? 1 : 0;
        }

        private static string GetVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrEmpty(value))
            {
                Console.WriteLine($"${name} variable is not set.");
                Environment.Exit(1);
            }
            else
            {
                Console.WriteLine($"{name}={value}");
            }

            return value;
        }
    }
}
