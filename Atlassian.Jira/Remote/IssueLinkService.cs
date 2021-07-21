using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Atlassian.Jira.Remote
{
    internal class IssueLinkService : IIssueLinkService
    {
        private readonly Jira _jira;

        public IssueLinkService(Jira jira)
        {
            _jira = jira;
        }

        public Task CreateLinkAsync(string outwardIssueKey, string inwardIssueKey, string linkName, string comment, CancellationToken token = default(CancellationToken))
        {
            var bodyObject = new JObject();
            bodyObject.Add("type", new JObject(new JProperty("name", linkName)));
            bodyObject.Add("inwardIssue", new JObject(new JProperty("key", inwardIssueKey)));
            bodyObject.Add("outwardIssue", new JObject(new JProperty("key", outwardIssueKey)));

            if (!String.IsNullOrEmpty(comment))
            {
                bodyObject.Add("comment", new JObject(new JProperty("body", comment)));
            }

            return _jira.RestClient.ExecuteRequestAsync(Method.POST, "rest/api/2/issueLink", bodyObject, token);
        }

        public async Task<IEnumerable<IssueLink>> GetLinksForIssueAsync(string issueKey, CancellationToken token = default(CancellationToken))
        {
            var issue = await _jira.Issues.GetIssueAsync(issueKey, token);
            return await GetLinksForIssueAsync(issue, null, token);
        }

        public async Task<IEnumerable<IssueLink>> GetLinksForIssueAsync(Issue issue, IEnumerable<string> linkTypeNames = null, CancellationToken token = default(CancellationToken))
        {
            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;
            var resource = String.Format("rest/api/2/issue/{0}?fields=issuelinks,created", issue.Key.Value);
            var issueLinksResult = await _jira.RestClient.ExecuteRequestAsync(Method.GET, resource, null, token).ConfigureAwait(false);
            var issueLinksJson = issueLinksResult["fields"]["issuelinks"];

            if (issueLinksJson == null)
            {
                throw new InvalidOperationException("There is no 'issueLinks' field on the issue data, make sure issue linking is turned on in JIRA.");
            }

            var issueLinks = issueLinksJson.Cast<JObject>().Where(_ => _ != null).ToList();
            var filteredIssueLinks = issueLinks;

            if (linkTypeNames != null)
            {
                filteredIssueLinks = issueLinks.Where(link => linkTypeNames.Contains(link["type"]["name"].ToString(), StringComparer.InvariantCultureIgnoreCase)).ToList();
            }

            var issuesToGet = filteredIssueLinks.Select(issueLink =>
            {
                var issueJson = issueLink["outwardIssue"] ?? issueLink["inwardIssue"];
                return issueJson["key"].Value<string>();
            }).Where(_ => !string.IsNullOrEmpty(_)).ToList();

            var issuesMap = await _jira.Issues.GetIssuesAsync(issuesToGet, token).ConfigureAwait(false) ?? new Dictionary<string, Issue>();
            if(!issuesMap.Keys.Contains(issue.Key.ToString()))
            {
                issuesMap.Add(issue.Key.ToString(), issue);
            }

            var receivedLinksCount = filteredIssueLinks.Count;

            filteredIssueLinks = filteredIssueLinks.Where(issueLink =>
            {
                var (outwardIssueKey, inwardIssueKey) = GetLinkedIssueKeysFromJObject(issueLink, string.Empty);
                return issuesMap.ContainsKey(outwardIssueKey) || issuesMap.ContainsKey(inwardIssueKey);
            }).ToList();

            if (receivedLinksCount > filteredIssueLinks.Count)
            {
                Console.WriteLine($"We're missing {receivedLinksCount - filteredIssueLinks.Count()} issues, probably because they're archived");
            }

            return filteredIssueLinks.Select(issueLink =>
            {
                var linkType = JsonConvert.DeserializeObject<IssueLinkType>(issueLink["type"].ToString(), serializerSettings);
                if (linkType == null)
                {
                    return null;
                }
                var (outwardIssueKey, inwardIssueKey) = GetLinkedIssueKeysFromJObject(issueLink, null);
                return new IssueLink(
                    linkType,
                    outwardIssueKey == null ? issue : issuesMap[outwardIssueKey],
                    inwardIssueKey == null ? issue : issuesMap[inwardIssueKey]);
            }).Where(_ => _ != null);
        }

        public async Task<IEnumerable<IssueLinkType>> GetLinkTypesAsync(CancellationToken token = default(CancellationToken))
        {
            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;

            var results = await _jira.RestClient.ExecuteRequestAsync(Method.GET, "rest/api/2/issueLinkType", null, token).ConfigureAwait(false);
            var linkTypes = results["issueLinkTypes"]
                .Cast<JObject>()
                .Select(issueLinkJson => JsonConvert.DeserializeObject<IssueLinkType>(issueLinkJson.ToString(), serializerSettings));

            return linkTypes;
        }

        private static (string outwardIssueKey, string inwardIssueKey) GetLinkedIssueKeysFromJObject(JObject issueLink, string defaultKeyValue)
        {
            var outwardIssue = issueLink["outwardIssue"];
            var inwardIssue = issueLink["inwardIssue"];
            var outwardIssueKey = outwardIssue != null ? (string) outwardIssue["key"] : defaultKeyValue;
            var inwardIssueKey = inwardIssue != null ? (string) inwardIssue["key"] : defaultKeyValue;
            return (outwardIssueKey, inwardIssueKey);
        }
    }
}
