using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace Atlassian.Jira.Remote
{
    internal class IssueTypeService : IIssueTypeService
    {
        private readonly Jira _jira;

        public IssueTypeService(Jira jira)
        {
            _jira = jira;
        }

        public async Task<IEnumerable<IssueType>> GetIssueTypesAsync(CancellationToken token = default(CancellationToken))
        {
            var remoteIssueTypes = await _jira.RestClient.ExecuteRequestAsync<RemoteIssueType[]>(Method.GET, "rest/api/2/issuetype", null, token).ConfigureAwait(false);
            var issueTypes = remoteIssueTypes.Select(t => new IssueType(t));
            return issueTypes;
        }

        public async Task<IEnumerable<IssueType>> GetIssueTypesForProjectAsync(string projectKey, CancellationToken token = default(CancellationToken))
        {
            var resource = String.Format("rest/api/2/project/{0}", projectKey);
            var projectJson = await _jira.RestClient.ExecuteRequestAsync(Method.GET, resource, null, token).ConfigureAwait(false);
            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;

            var issueTypes = projectJson["issueTypes"]
                .Select(issueTypeJson => JsonConvert.DeserializeObject<RemoteIssueType>(issueTypeJson.ToString(), serializerSettings))
                .Select(remoteIssueType => new IssueType(remoteIssueType));

            return issueTypes;
        }
    }
}