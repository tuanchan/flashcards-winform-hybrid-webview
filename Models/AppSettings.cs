using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TocflQuiz.Models
{
    public sealed class GeminiAppSettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gemini-flash-lite-latest";
    }

    public sealed class PixabayAppSettings
    {
        public string ApiKey { get; set; } = "";
    }

    public sealed class AppSettings
    {
        public string DatasetRoot { get; set; } = "";
        public Dictionary<string, QuizPreference> QuizPreferences { get; set; } = new();
        public GeminiAppSettings Gemini { get; set; } = new();
        public PixabayAppSettings Pixabay { get; set; } = new();
    }
}
