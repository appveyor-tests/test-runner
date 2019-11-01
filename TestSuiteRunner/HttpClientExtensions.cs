using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestSuiteRunner
{
    public static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PostAsJsonUnchunkedAsync<T>(this HttpClient httpClient, string url, T data, CancellationToken cancellationToken)
        {
            var dataAsString = JsonConvert.SerializeObject(data);
            var content = new StringContent(dataAsString);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return httpClient.PostAsync(url, content, cancellationToken);
        }

        public static Task<HttpResponseMessage> PutAsJsonUnchunkedAsync<T>(this HttpClient httpClient, string url, T data, CancellationToken cancellationToken)
        {
            var dataAsString = JsonConvert.SerializeObject(data);
            var content = new StringContent(dataAsString);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return httpClient.PutAsync(url, content, cancellationToken);
        }
    }
}
