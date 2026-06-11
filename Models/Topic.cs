using System;

namespace TocflQuiz.Models
{
    public sealed class Topic
    {
        public string Id { get; set; } = $"topic_{DateTime.Now:yyyyMMdd_HHmmss}";
        public string Title { get; set; } = "Untitled";
        public string? CoverImagePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
