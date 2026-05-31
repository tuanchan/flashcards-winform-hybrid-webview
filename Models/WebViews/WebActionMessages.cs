using System.Text.Json;
using System.Text.Json.Serialization;

namespace TocflQuiz.Models.WebViews
{
    public sealed class StringWebActionMessage
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [JsonPropertyName("data")]
        public string Data { get; set; } = "{}";
    }

    public sealed class JsonWebActionMessage
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }
}
