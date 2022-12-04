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
    internal class IssueFieldService : IIssueFieldService
    {
        private readonly Jira _jira;

        public IssueFieldService(Jira jira)
            => _jira = jira;

        public async Task<IEnumerable<CustomField>> GetCustomFieldsAsync(CancellationToken token = default(CancellationToken))
        {
            var remoteFields = await _jira.RestClient.ExecuteRequestAsync<RemoteField[]>(Method.GET, "rest/api/2/field", null, token).ConfigureAwait(false);
            var results = remoteFields.Where(f => f.IsCustomField).Select(f => new CustomField(f));
            return results;
        }

        public async Task<IEnumerable<CustomField>> GetCustomFieldsAsync(CustomFieldFetchOptions options, CancellationToken token = default(CancellationToken))
        {
            var projectKey = options.ProjectKeys.FirstOrDefault();
            var issueTypeId = options.IssueTypeIds.FirstOrDefault();
            var issueTypeName = options.IssueTypeNames.FirstOrDefault();

            if (!String.IsNullOrEmpty(issueTypeId) || !String.IsNullOrEmpty(issueTypeName))
            {
                projectKey = $"{projectKey}::{issueTypeId}::{issueTypeName}";
            }
            else if (String.IsNullOrEmpty(projectKey))
            {
                return await GetCustomFieldsAsync(token);
            }

            var resource = $"rest/api/2/issue/createmeta?expand=projects.issuetypes.fields";

            if (options.ProjectKeys.Any())
            {
                resource += $"&projectKeys={String.Join(",", options.ProjectKeys)}";
            }

            if (options.IssueTypeIds.Any())
            {
                resource += $"&issuetypeIds={String.Join(",", options.IssueTypeIds)}";
            }

            if (options.IssueTypeNames.Any())
            {
                resource += $"&issuetypeNames={String.Join(",", options.IssueTypeNames)}";
            }

            var jObject = await _jira.RestClient.ExecuteRequestAsync(Method.GET, resource, null, token).ConfigureAwait(false);
            var jProject = jObject["projects"].FirstOrDefault();

            if (jProject == null)
            {
                throw new InvalidOperationException($"Project with key '{projectKey}' was not found on the Jira server.");
            }

            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;
            var customFields = jProject["issuetypes"].SelectMany(issueType => GetCustomFieldsFromIssueType(issueType, serializerSettings));
            var distinctFields = customFields.GroupBy(c => c.Id).Select(g => g.First());

            return distinctFields;
        }

        public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<RemoteField>>> GetCreateFieldsSchemaForProjectAndIssueTypeAsync(string projectKey, string issueTypeId = null, CancellationToken token = default(CancellationToken))
        {
            var resource = "rest/api/2/issue/createmeta?expand=projects.issuetypes.fields";

            if (string.IsNullOrEmpty(projectKey))
            {
                throw new InvalidOperationException("Project key is empty.");
            }

            resource += $"&projectKeys={projectKey}";

            if (!string.IsNullOrEmpty(issueTypeId))
            {
                resource += $"&issuetypeIds={issueTypeId}";
            }

            var jObject = await _jira.RestClient.ExecuteRequestAsync(Method.GET, resource, null, token).ConfigureAwait(false);
            var jProject = jObject["projects"].FirstOrDefault();

            if (jProject == null)
            {
                throw new InvalidOperationException($"Project with key '{projectKey}' was not found on the Jira server.");
            }

            var serializerSettings = _jira.RestClient.Settings.JsonSerializerSettings;
            var results = new Dictionary<string, IReadOnlyCollection<RemoteField>>();
            foreach (var issueType in jProject["issuetypes"])
            {
                results[(string)issueType["id"]] = ((JObject)issueType["fields"]).Properties()
                    .Select(f => JsonConvert.DeserializeObject<RemoteField>(f.Value.ToString(), serializerSettings))
                    .ToList();
            }

            return results;
        }

        public Task<IEnumerable<CustomField>> GetCustomFieldsForProjectAsync(string projectKey, CancellationToken token = default(CancellationToken))
        {
            var options = new CustomFieldFetchOptions();
            options.ProjectKeys.Add(projectKey);

            return GetCustomFieldsAsync(options, token);
        }

        private static IEnumerable<CustomField> GetCustomFieldsFromIssueType(JToken issueType, JsonSerializerSettings serializerSettings)
        {
            return ((JObject) issueType["fields"]).Properties()
                .Where(f => f.Name.StartsWith("customfield_", StringComparison.OrdinalIgnoreCase))
                .Select(f => JsonConvert.DeserializeObject<RemoteField>(f.Value.ToString(), serializerSettings))
                .Select(remoteField => new CustomField(remoteField));
        }
    }
}