using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using CinemaPOS.Core.Models;

namespace CinemaPOS.Core.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        
        public string JwtToken { get; set; }

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:8080");
            _httpClient.Timeout = TimeSpan.FromSeconds(2);

            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                .WaitAndRetryAsync(1, _ => TimeSpan.FromMilliseconds(300));
        }

        private void ConfigureHeaders(HttpRequestMessage request, string idempotencyKey = null, string supervisorPin = null)
        {
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };
            request.Headers.Pragma.ParseAdd("no-cache");

            if (!string.IsNullOrEmpty(JwtToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtToken);
            }

            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.Headers.Add("X-Idempotency-Key", idempotencyKey);
            }

            if (!string.IsNullOrEmpty(supervisorPin))
            {
                request.Headers.Add("X-Supervisor-Pin", supervisorPin);
            }
        }

        public async Task<T> GetAsync<T>(string uri)
        {
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                string separator = uri.Contains("?") ? "&" : "?";
                string cacheBustUri = $"{uri}{separator}_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var request = new HttpRequestMessage(HttpMethod.Get, cacheBustUri);
                ConfigureHeaders(request);
                
                var res = await _httpClient.SendAsync(request);
                res.EnsureSuccessStatusCode();
                return res;
            });

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // If T is a collection/list, check if the JSON is wrapped in an envelope like {"value": [...]}
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(string))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.Array)
                    {
                        return JsonSerializer.Deserialize<T>(valProp.GetRawText(), options);
                    }
                    if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                    {
                        return JsonSerializer.Deserialize<T>(dataProp.GetRawText(), options);
                    }
                    if (doc.RootElement.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                    {
                        return JsonSerializer.Deserialize<T>(itemsProp.GetRawText(), options);
                    }
                }
            }

            return JsonSerializer.Deserialize<T>(json, options);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(
            string uri, 
            TRequest payload, 
            string idempotencyKey = null, 
            string supervisorPin = null)
        {
            IAsyncPolicy<HttpResponseMessage> policy = string.IsNullOrEmpty(idempotencyKey) ? Policy.NoOpAsync<HttpResponseMessage>() : _retryPolicy;

            var response = await policy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = JsonContent.Create(payload)
                };
                ConfigureHeaders(request, idempotencyKey, supervisorPin);

                var res = await _httpClient.SendAsync(request);
                res.EnsureSuccessStatusCode();
                return res;
            });
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        
        public async Task DeleteAsync(string uri)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, uri);
                ConfigureHeaders(request);
                
                var res = await _httpClient.SendAsync(request);
                res.EnsureSuccessStatusCode();
                return res;
            });
        }
    }
}
