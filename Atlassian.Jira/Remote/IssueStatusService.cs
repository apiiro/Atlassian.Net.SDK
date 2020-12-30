using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace Atlassian.Jira.Remote
{
    internal class IssueStatusService : IIssueStatusService
    {
        private readonly Jira _jira;

        public IssueStatusService(Jira jira)
        {
            _jira = jira;
        }

        public async Task<IEnumerable<IssueStatus>> GetStatusesAsync(CancellationToken token = default(CancellationToken))
        {
            var results = await _jira.RestClient.ExecuteRequestAsync<RemoteStatus[]>(Method.GET, "rest/api/2/status", null, token).ConfigureAwait(false);
            return results.Select(s => new IssueStatus(s));
        }

        public async Task<IssueStatus> GetStatusAsync(string idOrName, CancellationToken token = default(CancellationToken))
        {
            var resource = $"rest/api/2/status/{idOrName}";
            var result = await _jira.RestClient.ExecuteRequestAsync<RemoteStatus>(Method.GET, resource, null, token).ConfigureAwait(false);
            return new IssueStatus(result);
        }
    }
}
