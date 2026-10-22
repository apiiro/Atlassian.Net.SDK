using System;
using System.Net;
using Atlassian.Jira.Remote;
using RestSharp;
using Xunit;

namespace Atlassian.Jira.Test
{
    public class JiraRestClientTest
    {
        private static IRestResponse OkResponse(string content)
            => new RestResponse { StatusCode = HttpStatusCode.OK, ResponseStatus = ResponseStatus.Completed, Content = content };

        [Fact]
        public void GetValidJsonFromResponse_NestedDeeperThan64_ParsesResponse()
        {
            var client = new JiraRestClient("http://localhost");

            var result = client.GetValidJsonFromResponse(new RestRequest(), OkResponse(DeeplyNestedJson.SearchResponse(100)));

            Assert.Equal("TST-1", (string)result["issues"][0]["key"]);
        }

        [Fact]
        public void GetValidJsonFromResponse_NestedDeeperThanConfiguredMaxDepth_ThrowsWithTruncatedContent()
        {
            var settings = new JiraRestClientSettings();
            settings.JsonSerializerSettings.MaxDepth = 64;
            var client = new JiraRestClient("http://localhost", settings: settings);
            var content = DeeplyNestedJson.SearchResponse(100);

            var exception = Assert.Throws<InvalidOperationException>(
                () => client.GetValidJsonFromResponse(new RestRequest(), OkResponse(content)));

            Assert.Contains("MaxDepth of 64", exception.InnerException.Message);
            Assert.Equal(
                $"Failed to parse response as JSON. Content: {content.Substring(0, 1000)}... ({content.Length} characters in total)",
                exception.Message);
        }
    }
}
