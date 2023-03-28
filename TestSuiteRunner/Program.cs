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
            var testImage = GetVariable("TEST_IMAGE");

            var binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var suitePath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "test-suites", $"{testSuite}.json"), binDir);
            var suiteFailed = false;

            if (!File.Exists(suitePath))
            {
                Console.WriteLine($"Test suite {suitePath} not found.");
                Environment.Exit(1);
            }

            var tests = JsonConvert.DeserializeObject<TestItem[]>(File.ReadAllText(suitePath));

            // add all relevant tests to AppVeyor
            var filteredTests = tests.Where(t => t.Images.Contains(testImage) || t.Images.Length == 0).ToArray<TestItem>();
            foreach(var test in filteredTests)
            {
                await BuildWorkerApi.AddTest(test.TestName);
            }

            int testNum = 1;

            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrency))
            {
                List<Task> tasks = new List<Task>();

                foreach (var test in filteredTests)
                {
                    concurrencySemaphore.Wait();

                    Console.WriteLine($"Running test [{testNum++}/{filteredTests.Length}]");
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
