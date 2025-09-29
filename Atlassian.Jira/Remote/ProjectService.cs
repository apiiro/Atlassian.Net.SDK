using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace Atlassian.Jira.Remote
{
    internal class ProjectService : IProjectService
    {
        private readonly Jira _jira;

        public ProjectService(Jira jira)
        {
            _jira = jira;
        }

        public async Task<IEnumerable<Project>> GetProjectsAsync(CancellationToken token = default(CancellationToken))
        {
            var remoteProjects = await _jira.RestClient.ExecuteRequestAsync<RemoteProject[]>(Method.GET, "rest/api/2/project?expand=lead,url", null, token).ConfigureAwait(false);
            return remoteProjects.Select(p => new Project(_jira, p));
        }

        public async Task<Project> GetProjectAsync(string projectKey, CancellationToken token = new CancellationToken())
        {
            var resource = String.Format("rest/api/2/project/{0}?expand=lead,url", projectKey);
            var remoteProject = await _jira.RestClient.ExecuteRequestAsync<RemoteProject>(Method.GET, resource, null, token).ConfigureAwait(false);
            return new Project(_jira, remoteProject);
        }

        public async Task<(IEnumerable<IssueType> IssueTypes, IEnumerable<ProjectComponent> Components)>
            GetProjectIssueTypesAndComponentsAsync(string projectKey, CancellationToken token = default)
        {
            var resource = String.Format("rest/api/2/project/{0}", projectKey);
            var projectJson = await _jira.RestClient.ExecuteRequestAsync(Method.GET, resource, null, token).ConfigureAwait(false);
            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;

            var issueTypes = projectJson["issueTypes"]
                .Select(issueTypeJson => JsonConvert.DeserializeObject<RemoteIssueType>(issueTypeJson.ToString(), serializerSettings))
                .Select(remoteIssueType => new IssueType(remoteIssueType));

            var components = projectJson["components"]
                .Select(componentJson => JsonConvert.DeserializeObject<RemoteComponent>(componentJson.ToString(), serializerSettings))
                .Select(remoteComponent => new ProjectComponent(remoteComponent));

            return (issueTypes, components);
        }
    }
}
