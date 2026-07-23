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
            => CreateJiraWithSearchResponses("rest/api/3/search/jql", responses);

        private static TestableJira CreateJiraWithSearchResponses(string searchResource, string[] responses)
        {
            var jira = TestableJira.Create();
            jira.RestService.Setup(c => c.Settings).Returns(new JiraRestClientSettings());
            jira.IssueFieldService
                .Setup(s => s.GetCustomFieldsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Enumerable.Empty<CustomField>()));
            jira.RestService
                .Setup(c => c.ExecuteRequestAsync(Method.POST, searchResource, It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsInOrder(responses.Select(response => (object)Task.FromResult(JToken.Parse(response))).ToArray());
            return jira;
        }

        private static IssueService CreateIssueService(TestableJira jira, bool isJiraServer = false)
            => new IssueService(jira, new JiraRestClientSettings { IsJiraServer = isJiraServer });

        private static string IssuePage(string issueKeys, string pagingMetadata)
        {
            var issues = string.Join(",", issueKeys.Split(',').Select(key => $"{{\"key\":\"{key}\",\"fields\":{{}}}}"));
            return $"{{\"issues\":[{issues}],{pagingMetadata}}}";
        }

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

        [Fact]
        public async Task GetIssuesFromJqlAsync_SettingsIsJiraServer_UsesServerSearchEndpoint()
        {
            var jira = CreateJiraWithSearchResponses("rest/api/2/search", new[] { "{\"issues\":[],\"startAt\":0,\"total\":0}" });
            var service = CreateIssueService(jira, isJiraServer: true);

            var result = await service.GetIssuesFromJqlAsync(new IssueSearchOptions("project = TST"), false);

            Assert.Empty(result);
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/2/search", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Once());
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/3/search/jql", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task GetIssuesAsync_JiraCloud_FollowsNextPageToken()
        {
            var jira = CreateJiraWithSearchResponses(
                IssuePage("TST-1", "\"nextPageToken\":\"page2\""),
                IssuePage("TST-2", "\"isLast\":true"));
            var service = CreateIssueService(jira);

            var result = await service.GetIssuesAsync(new[] { "TST-1", "TST-2" });

            Assert.Equal(2, result.Count);
            Assert.Contains("TST-1", result.Keys);
            Assert.Contains("TST-2", result.Keys);
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/3/search/jql", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetIssuesAsync_JiraServer_PagesWithStartAt()
        {
            var jira = CreateJiraWithSearchResponses(
                "rest/api/2/search",
                new[]
                {
                    IssuePage("TST-1", "\"startAt\":0,\"total\":150"),
                    IssuePage("TST-2", "\"startAt\":100,\"total\":150")
                });
            var service = CreateIssueService(jira, isJiraServer: true);

            var result = await service.GetIssuesAsync(new[] { "TST-1", "TST-2" });

            Assert.Equal(2, result.Count);
            jira.RestService.Verify(
                c => c.ExecuteRequestAsync(Method.POST, "rest/api/2/search", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetIssuesAsync_JiraCloud_RepeatedNextPageToken_Throws()
        {
            var jira = CreateJiraWithSearchResponses(
                IssuePage("TST-1", "\"nextPageToken\":\"page2\""),
                IssuePage("TST-2", "\"nextPageToken\":\"page2\""));
            var service = CreateIssueService(jira);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetIssuesAsync(new[] { "TST-1", "TST-2" }));

            Assert.Contains("nextPageToken", exception.Message);
        }
    }
}
