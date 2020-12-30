using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    }
}
