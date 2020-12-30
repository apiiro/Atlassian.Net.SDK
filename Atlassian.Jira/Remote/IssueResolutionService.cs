using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace Atlassian.Jira.Remote
{
    internal class IssueResolutionService : IIssueResolutionService
    {
        private readonly Jira _jira;

        public IssueResolutionService(Jira jira)
        {
            _jira = jira;
        }

        public async Task<IEnumerable<IssueResolution>> GetResolutionsAsync(CancellationToken token)
        {
            var resolutions = await _jira.RestClient.ExecuteRequestAsync<RemoteResolution[]>(Method.GET, "rest/api/2/resolution", null, token).ConfigureAwait(false);
            return resolutions.Select(r => new IssueResolution(r));
        }
    }
}