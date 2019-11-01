using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace TestSuiteRunner
{
    class TestBuildWorker
    {
        string _instanceId;
        TestItem _item;
        string _appveyorUrl;
        string _appveyorApiToken;

        public TestBuildWorker(string instanceId, TestItem item)
        {
            _instanceId = instanceId;
            _item = item;
            _appveyorUrl = Environment.GetEnvironmentVariable("APPVEYOR_URL") ?? "https://ci.appveyor.com";
            _appveyorApiToken = Environment.GetEnvironmentVariable("APPVEYOR_API_TOKEN");
        }

        public async Task Start()
        {
            string error = null;
            bool downloadLog = false;

            string jobId = null;

            var MaxProvisioningTime = 10; // minutes
            var MaxRunTime = 10; // minutes

            try
            {

                // start new build
                var build = await StartNewBuild(_item.AccountName, _item.ProjectSlug, _item.Branch);
                string buildVersion = build.Value<string>("version");
                WriteLog("Build version: " + buildVersion);

                DateTime buildStarted = DateTime.UtcNow;
                WriteLog("Build started");

                string previousStatus = null;
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    var elapsed = DateTime.UtcNow - buildStarted;

                    build = await GetBuildDetails(_item.AccountName, _item.ProjectSlug, buildVersion);
                    var job = build["build"]["jobs"].First();
                    jobId = job.Value<string>("jobId");

                    var status = job.Value<string>("status");
                    WriteLog("Build status at " + elapsed.ToString() + " - " + status);

                    if ((status == "queued" || status == "starting") && elapsed.TotalMinutes > MaxProvisioningTime)
                    {
                        string message = "Build has not started in allotted time.";
                        WriteLog(message);
                        await CancelBuild(_item.AccountName, _item.ProjectSlug, buildVersion);
                        throw new Exception(message);
                    }
                    else if (status == "running" && (previousStatus == "queued" || previousStatus == "starting"))
                    {
                        buildStarted = DateTime.UtcNow;
                    }
                    else if (status == "running" && elapsed.TotalMinutes > MaxRunTime)
                    {
                        string message = "Build has not finished in allotted time.";
                        downloadLog = true;
                        WriteLog(message);
                        await CancelBuild(_item.AccountName, _item.ProjectSlug, buildVersion);
                        throw new Exception(message);
                    }
                    else if (status == "failed")
                    {
                        string message = "Build has failed.";
                        downloadLog = true;
                        WriteLog(message);
                        throw new Exception(message);
                    }
                    else if (status == "cancelled")
                    {
                        string message = "Build has been failed.";
                        downloadLog = true;
                        WriteLog(message);
                        throw new Exception(message);
                    }
                    else if (status == "success")
                    {
                        var started = job.Value<DateTime>("started");
                        var finished = job.Value<DateTime>("finished");

                        WriteLog(String.Format("Build duration: {0}", (finished - started)));
                        break;
                    }

                    previousStatus = status;
                } // while
            }
            catch (Exception ex)
            {
                error = ex.Message;
                WriteLog(ex.Message);

                if (ex.InnerException != null)
                {
                    WriteLog(" + " + ex.InnerException.Message);
                }
            }

            // download build log if there was an error
            string buildLog = null;
            try
            {
                if (error != null && downloadLog)
                {
                    // download build log
                    buildLog = await DownloadBuildLog(jobId);
                }
            }
            catch (Exception ex)
            {
                WriteLog("Cannot download build log: " + ex.Message);
            }
        }

        private async Task<JToken> StartNewBuild(string accountName, string projectSlug, string branch)
        {
            Console.WriteLine("Starting a new build");

            string errorMessage = "Error starting a new build in AppVeyor";

            try
            {
                using (var client = GetAppveyorClient())
                {
                    // should respond in 30 seconds
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var request = new
                    {
                        accountName = accountName,
                        projectSlug = projectSlug,
                        branch = branch,
                        environmentVariables = new
                        {
                            APPVEYOR_BUILD_WORKER_CLOUD = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_WORKER_CLOUD"),
                            APPVEYOR_BUILD_WORKER_IMAGE = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_WORKER_IMAGE")
                        }
                    };

                    var response = await client.PostAsJsonAsync("api/builds", request);
                    if (!response.IsSuccessStatusCode)
                    {
                        // read response as string
                        string responseContents = await response.Content.ReadAsStringAsync();
                        throw new Exception(String.Format("Starting build returned {0}: {1}", (int)response.StatusCode, responseContents));
                    }

                    Console.WriteLine(errorMessage);
                    return await response.Content.ReadAsAsync<JToken>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(errorMessage);
                throw new Exception(errorMessage, ex);
            }
        }

        private async Task CancelBuild(string accountName, string projectSlug, string buildVersion)
        {
            Console.WriteLine("Cancelling build");

            try
            {
                using (var client = GetAppveyorClient())
                {
                    // should respond in 30 seconds
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var response = await client.DeleteAsync(String.Format("api/builds/{0}/{1}/{2}", accountName, projectSlug, buildVersion));
                    if (!response.IsSuccessStatusCode)
                    {
                        // read response as string
                        string responseContents = await response.Content.ReadAsStringAsync();
                        throw new Exception(String.Format("Cancelling build returned {0}: {1}", (int)response.StatusCode, responseContents));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error cancelling build", ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(20));

            // check build status after 20 seconds
            var build = await GetBuildDetails(accountName, projectSlug, buildVersion);
            var status = build["build"].Value<string>("status");
            if (status == "cancelling")
            {
                Console.WriteLine("Build has stuck in Cancelling state");
            }
        }

        private async Task<JToken> GetBuildDetails(string accountName, string projectSlug, string buildVersion)
        {
            try
            {
                using (var client = GetAppveyorClient())
                {
                    // should respond in 30 seconds
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var response = await client.GetAsync(String.Format("api/projects/{0}/{1}/build/{2}", accountName, projectSlug, buildVersion));
                    if (!response.IsSuccessStatusCode)
                    {
                        // read response as string
                        string responseContents = await response.Content.ReadAsStringAsync();
                        throw new Exception(String.Format("Getting build details returned {0}: {1}", (int)response.StatusCode, responseContents));
                    }

                    return await response.Content.ReadAsAsync<JToken>();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting build details", ex);
            }
        }

        private async Task<string> DownloadBuildLog(string jobId)
        {
            try
            {
                using (var client = GetAppveyorClient())
                {
                    // should respond in 30 seconds
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var response = await client.GetAsync(String.Format("api/buildjobs/{0}/log", jobId));
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error downloading build log", ex);
            }
        }

        private HttpClient GetAppveyorClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(_appveyorUrl);
            client.Timeout = TimeSpan.FromMinutes(1);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _appveyorApiToken);
            return client;
        }

        private void WriteLog(string text)
        {
            string line = String.Format("{0} [{1}]: {2}", DateTime.UtcNow.ToLongTimeString(), _instanceId, text);
            Console.WriteLine(line);
        }
    }
}
