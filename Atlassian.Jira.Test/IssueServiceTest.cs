using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlassian.Jira.Remote;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit;

namespace Atlassian.Jira.Test
{
    public class IssueServiceTest
    {
        private static TestableJira CreateJiraWithSearchResponses(params string[] responses)
        {
            var jira = TestableJira.Create();
            jira.RestService.Setup(c => c.Settings).Returns(new JiraRestClientSettings());
            jira.IssueFieldService
                .Setup(s => s.GetCustomFieldsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Enumerable.Empty<CustomField>()));
            jira.RestService
                .Setup(c => c.ExecuteRequestAsync(Method.POST, "rest/api/3/search/jql", It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsInOrder(responses.Select(response => (object)Task.FromResult(JToken.Parse(response))).ToArray());
            return jira;
        }

        private static IssueService CreateIssueService(TestableJira jira)
            => new IssueService(jira, new JiraRestClientSettings());

        [Fact]
        public async Task GetIssuesFromJqlAsync_IssuesFieldPresent_DoesNotRetry()
        {
            var jira = CreateJiraWithSearchResponses("{\"issues\":[],\"isLast\":true}");
            var service = CreateIssueService(jira);

            var result = await service.GetIssuesFromJqlAsync(new IssueSearchOptions("project = TST"), false);

            Assert.Empty(result);
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/3/search/jql", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task GetIssuesFromJqlAsync_IssuesFieldMissingOnce_RetriesAndSucceeds()
        {
            var jira = CreateJiraWithSearchResponses(
                "{\"isLast\":false}",
                "{\"issues\":[],\"isLast\":true}");
            var service = CreateIssueService(jira);

            var result = await service.GetIssuesFromJqlAsync(new IssueSearchOptions("project = TST"), false);

            Assert.Empty(result);
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/3/search/jql", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetIssuesFromJqlAsync_IssuesFieldMissingTwice_EmptyLastPage_ReturnsEmptyResult()
        {
            var jira = CreateJiraWithSearchResponses("{\"isLast\":true}", "{\"isLast\":true}");
            var service = CreateIssueService(jira);

            var result = await service.GetIssuesFromJqlAsync(new IssueSearchOptions("project = TST"), false);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetIssuesFromJqlAsync_IssuesFieldMissingTwice_NotLastPage_Throws()
        {
            var jira = CreateJiraWithSearchResponses("{\"nextPageToken\":\"token\"}", "{\"nextPageToken\":\"token\"}");
            var service = CreateIssueService(jira);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetIssuesFromJqlAsync(new IssueSearchOptions("project = TST"), false));

            Assert.Contains("'issues'", exception.Message);
            Assert.Contains("nextPageToken", exception.Message);
        }
    }
}
