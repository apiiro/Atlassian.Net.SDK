using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Atlassian.Jira.Remote
{
    /// <summary>
    /// Custom JSON converter to handle Jira description field which can be either:
    /// - A simple string (legacy format)
    /// - An Atlassian Document Format object (new format in API v3)
    /// </summary>
    public class DescriptionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                // Legacy format - simple string
                return reader.Value?.ToString();
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                // New format - Atlassian Document Format object
                var jObject = JObject.Load(reader);
                
                // For now, we'll extract a simplified text representation
                // In the future, this could be enhanced to properly parse the ADF
                return ExtractTextFromAtlassianDocumentFormat(jObject);
            }

            // Fallback for unexpected formats
            return reader.Value?.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // When writing, always write as a simple string
            // The API will handle the conversion if needed
            writer.WriteValue(value);
        }

        private string ExtractTextFromAtlassianDocumentFormat(JObject adfObject)
        {
            try
            {
                // Basic text extraction from Atlassian Document Format
                // This is a simplified implementation that extracts plain text
                var content = adfObject["content"];
                if (content != null && content.Type == JTokenType.Array)
                {
                    return ExtractTextFromContent(content as JArray);
                }
                
                return string.Empty;
            }
            catch
            {
                // If we can't parse the ADF, return the raw JSON as fallback
                return adfObject.ToString(Formatting.None);
            }
        }

        private string ExtractTextFromContent(JArray contentArray)
        {
            var textParts = new System.Collections.Generic.List<string>();

            foreach (var item in contentArray)
            {
                if (item is JObject contentItem)
                {
                    var type = contentItem["type"]?.ToString();
                    
                    switch (type)
                    {
                        case "paragraph":
                        case "heading":
                            var paragraphContent = contentItem["content"] as JArray;
                            if (paragraphContent != null)
                            {
                                textParts.Add(ExtractTextFromContent(paragraphContent));
                            }
                            break;
                            
                        case "text":
                            var text = contentItem["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                textParts.Add(text);
                            }
                            break;
                            
                        case "hardBreak":
                            textParts.Add("\n");
                            break;
                            
                        case "bulletList":
                        case "orderedList":
                            var listContent = contentItem["content"] as JArray;
                            if (listContent != null)
                            {
                                textParts.Add(ExtractTextFromContent(listContent));
                            }
                            break;
                            
                        case "listItem":
                            var listItemContent = contentItem["content"] as JArray;
                            if (listItemContent != null)
                            {
                                textParts.Add("• " + ExtractTextFromContent(listItemContent));
                            }
                            break;
                            
                        case "codeBlock":
                            var codeContent = contentItem["content"] as JArray;
                            if (codeContent != null)
                            {
                                textParts.Add("```\n" + ExtractTextFromContent(codeContent) + "\n```");
                            }
                            break;
                            
                        default:
                            // For unknown types, try to extract nested content
                            var nestedContent = contentItem["content"] as JArray;
                            if (nestedContent != null)
                            {
                                textParts.Add(ExtractTextFromContent(nestedContent));
                            }
                            break;
                    }
                }
            }

            return string.Join("", textParts);
        }
    }
}
