using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Atlassian.Jira.Test
{
    internal static class DeeplyNestedJson
    {
        /// <summary>
        /// A search response whose only issue has a description of nested bullet lists, in the Atlassian Document Format.
        /// Each list level adds four levels of JSON nesting.
        /// </summary>
        public static string SearchResponse(int listLevels)
        {
            JObject list = null;
            for (var level = listLevels; level >= 1; level--)
            {
                var itemContent = new JArray(Paragraph($"level {level}"));
                if (list != null)
                {
                    itemContent.Add(list);
                }

                var item = new JObject { ["type"] = "listItem", ["content"] = itemContent };
                list = new JObject { ["type"] = "bulletList", ["content"] = new JArray(item) };
            }

            var description = new JObject { ["type"] = "doc", ["version"] = 1, ["content"] = new JArray(list) };
            var issue = new JObject
            {
                ["id"] = "10001",
                ["key"] = "TST-1",
                ["fields"] = new JObject { ["summary"] = "Nested lists", ["description"] = description }
            };

            return new JObject { ["issues"] = new JArray(issue), ["isLast"] = true }.ToString(Formatting.None);
        }

        private static JObject Paragraph(string text)
        {
            return new JObject
            {
                ["type"] = "paragraph",
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = text })
            };
        }
    }
}
