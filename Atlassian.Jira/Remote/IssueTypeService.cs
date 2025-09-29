using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    }
}