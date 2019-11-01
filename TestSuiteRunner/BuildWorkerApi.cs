using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestSuiteRunner
{
    class BuildWorkerApi
    {
        public static async Task AddTest(string testName)
        {
            if (!IsRunningInsideCI())
            {
                return;
            }

            using (var client = GetAppveyorWorkerApiClient())
            {
                var request = new
                {
                    testName = testName,
                    testFramework = "AppVeyor",
                    outcome = "None"
                };

                var response = await client.PostAsJsonUnchunkedAsync("api/tests", request, CancellationToken.None);
                response.EnsureSuccessStatusCode();
            }
        }

        public static async Task UpdateTest(string testName, string outcome, string stdOut = null)
        {
            if (!IsRunningInsideCI())
            {
                return;
            }

            using (var client = GetAppveyorWorkerApiClient())
            {
                var request = new
                {
                    testName = testName,
                    outcome = outcome,
                    stdOut = stdOut
                };

                var response = await client.PutAsJsonUnchunkedAsync("api/tests", request, CancellationToken.None);
                response.EnsureSuccessStatusCode();
            }
        }

        private static bool IsRunningInsideCI()
        {
            return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
        }

        private static HttpClient GetAppveyorWorkerApiClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("APPVEYOR_API_URL"));
            client.Timeout = TimeSpan.FromMinutes(1);
            return client;
        }
    }
}
