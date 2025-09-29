using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Extensions;

namespace Atlassian.Jira.Remote
{
    /// <summary>
    /// Implements the IJiraRestClient interface using RestSharp.
    /// </summary>
    public class JiraRestClient : IJiraRestClient
    {
        private readonly RestClient _restClient;
        private readonly JiraRestClientSettings _clientSettings;

        /// <summary>
        /// Creates a new instance of the JiraRestClient class.
        /// </summary>
        /// <param name="url">Url to the JIRA server.</param>
        /// <param name="username">Username used to authenticate.</param>
        /// <param name="password">Password used to authenticate.</param>
        /// <param name="settings">Settings to configure the rest client.</param>
        public JiraRestClient(string url, string username = null, string password = null, JiraRestClientSettings settings = null)
            : this(url, new HttpBasicAuthenticator(username, password), settings)
        {
        }

        /// <summary>
        /// Creates a new instance of the JiraRestClient class.
        /// </summary>
        /// <param name="url">The url to the JIRA server.</param>
        /// <param name="authenticator">The authenticator used by RestSharp.</param>
        /// <param name="settings">The settings to configure the rest client.</param>
        protected JiraRestClient(string url, IAuthenticator authenticator, JiraRestClientSettings settings = null)
        {
            url = url.EndsWith("/") ? url : url += "/";
            _clientSettings = settings ?? new JiraRestClientSettings();
            _restClient = new RestClient(url)
            {
                Proxy = _clientSettings.Proxy
            };

            this._restClient.Authenticator = authenticator;
        }

        /// <summary>
        /// Rest sharp client used to issue requests.
        /// </summary>
        public RestClient RestSharpClient
        {
            get
            {
                return _restClient;
            }
        }

        /// <summary>
        /// Url to the JIRA server.
        /// </summary>
        public string Url
        {
            get
            {
                return _restClient.BaseUrl.ToString();
            }
        }

        /// <summary>
        /// Settings to configure the rest client.
        /// </summary>
        public JiraRestClientSettings Settings
        {
            get
            {
                return _clientSettings;
            }
        }

        /// <summary>
        /// Executes an async request and serializes the response to an object.
        /// </summary>
        public async Task<T> ExecuteRequestAsync<T>(Method method, string resource, object requestBody = null, CancellationToken token = default(CancellationToken))
        {
            var result = await ExecuteRequestAsync(method, resource, requestBody, token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(result.ToString(), Settings.JsonSerializerSettings);
        }

        /// <summary>
        /// Executes an async request and returns the response as JSON.
        /// </summary>
        public async Task<JToken> ExecuteRequestAsync(Method method, string resource, object requestBody = null, CancellationToken token = default(CancellationToken))
        {
            if (method == Method.GET && requestBody != null)
            {
                throw new InvalidOperationException($"GET requests are not allowed to have a request body. Resource: {resource}. Body: {requestBody}");
            }

            var request = new RestRequest();
            request.Method = method;
            request.Resource = resource;
            request.RequestFormat = DataFormat.Json;

            if (requestBody is string)
            {
                request.AddParameter(new Parameter(name: "application/json", value: requestBody, type: ParameterType.RequestBody));
            }
            else if (requestBody != null)
            {
                request.JsonSerializer = new RestSharpJsonSerializer(JsonSerializer.Create(Settings.JsonSerializerSettings));
                request.AddJsonBody(requestBody);
            }

            LogRequest(request, requestBody);
            var response = await ExecuteRawResquestAsync(request, token).ConfigureAwait(false);
            return GetValidJsonFromResponse(request, response);
        }

        /// <summary>
        /// Executes a request with logging and validation.
        /// </summary>
        public async Task<IRestResponse> ExecuteRequestAsync(IRestRequest request, CancellationToken token = default(CancellationToken))
        {
            LogRequest(request);
            var response = await ExecuteRawResquestAsync(request, token).ConfigureAwait(false);
            GetValidJsonFromResponse(request, response);
            return response;
        }

        /// <summary>
        /// Executes a raw request.
        /// </summary>
        protected virtual Task<IRestResponse> ExecuteRawResquestAsync(IRestRequest request, CancellationToken token)
        {
            return _restClient.ExecuteAsync(request, token);
        }

        /// <summary>
        /// Downloads file as a byte array.
        /// </summary>
        /// <param name="url">Url to the file location.</param>
        public byte[] DownloadData(string url)
        {
            return _restClient.DownloadData(new RestRequest(url, Method.GET));
        }

        /// <summary>
        /// Downloads file to the specified location.
        /// </summary>
        /// <param name="url">Url to the file location.</param>
        /// <param name="fullFileName">Full file name where the file will be downloaded.</param>
        public void Download(string url, string fullFileName)
        {
            _restClient.DownloadData(new RestRequest(url, Method.GET)).SaveAs(fullFileName);
        }

        private void LogRequest(IRestRequest request, object body = null)
        {
            if (this._clientSettings.EnableRequestTrace)
            {
                Trace.WriteLine(String.Format("[{0}] Request Url: {1}",
                    request.Method,
                    request.Resource));

                if (body != null)
                {
                    Trace.WriteLine(String.Format("[{0}] Request Data: {1}",
                        request.Method,
                        JsonConvert.SerializeObject(body, new JsonSerializerSettings()
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore
                        })));
                }
            }
        }

        private JToken GetValidJsonFromResponse(IRestRequest request, IRestResponse response)
        {
            var content = response.Content != null ? response.Content.Trim() : string.Empty;

            if (this._clientSettings.EnableRequestTrace)
            {
                Trace.WriteLine(String.Format("[{0}] Response for Url: {1}\n{2}",
                    request.Method,
                    request.Resource,
                    content));
            }

            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                try
                {
                    return FallbackToHttpClientOnFailureAsync(request).Result;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Fallback to HttpClient failed.\n" +
                        $"Original request error message: {response.ErrorMessage}\n" +
                        $"Content: {content}\n" +
                        $"Code: {response.StatusCode}",
                        exception
                    );
                }
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new System.Security.Authentication.AuthenticationException(string.Format("Response Content: {0}", content));
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ResourceNotFoundException($"Response Content: {content}");
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ResourceNotFoundException($"Rate limit exceeded. Response headers: {string.Join(", ", response.Headers.Select(header => $"{header.Name}: {header.Value}"))}");
            }
            else if ((int)response.StatusCode >= 400)
            {
                throw new InvalidOperationException($"Response Status Code: {(int)response.StatusCode}. Response Content: {content}");
            }
            else if (string.IsNullOrWhiteSpace(content))
            {
                return new JObject();
            }
            else if (!content.StartsWith("{") && !content.StartsWith("["))
            {
                throw new InvalidOperationException(String.Format("Response was not recognized as JSON. Content: {0}", content));
            }
            else
            {
                JToken parsedContent;

                try
                {
                    parsedContent = JToken.Parse(content);
                }
                catch (JsonReaderException ex)
                {
                    throw new InvalidOperationException(String.Format("Failed to parse response as JSON. Content: {0}", content), ex);
                }

                if (parsedContent != null && parsedContent.Type == JTokenType.Object && parsedContent["errorMessages"] != null)
                {
                    throw new InvalidOperationException(string.Format("Response reported error(s) from JIRA: {0}", parsedContent["errorMessages"].ToString()));
                }

                return parsedContent;
            }
        }

        private async Task<JToken> FallbackToHttpClientOnFailureAsync(IRestRequest request)
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, certificate, chain, errors) => true
            };

            if (_restClient.Proxy != null)
            {
                HttpClient.DefaultProxy = _restClient.Proxy;
            }

            var client = new HttpClient(handler);

            var authHeader = request.Parameters.FirstOrDefault(header => header.Name == "Authorization");
            var splitAuthHeader = authHeader?.Value?.ToString()?.Split();

            if (splitAuthHeader == null || splitAuthHeader.Length < 2)
            {
                return null;
            }

            var authValue = splitAuthHeader[1];

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            var response = await client.GetAsync($"{_restClient.BaseUrl}{request.Resource}");

            Trace.WriteLine(
                $"Retry request attempt headers: {Convert.ToBase64String(Encoding.ASCII.GetBytes(response.Headers.ToString()))}");

            return await JToken.LoadAsync(
                new JsonTextReader(new StreamReader(await response.Content.ReadAsStreamAsync())));
        }
    }
}