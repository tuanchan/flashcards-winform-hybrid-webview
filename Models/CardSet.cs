using System;
using System.Collections.Generic;

namespace TocflQuiz.Models
{
    public sealed class CardSet
    {
        public string Id { get; set; } = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";
        public string Title { get; set; } = "Untitled";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Language { get; set; }
        public string? LanguageCode { get; set; }
        public string? FolderName { get; set; }
        public string? BaseFolder { get; set; }
        public string? VocabsFilePath { get; set; }
        public string? NotYetFilePath { get; set; }
        public string? ConfigFilePath { get; set; }
        public string? CoverImagePath { get; set; }
        public int VocabCount { get; set; }

        public List<CardItem> Items { get; set; } = new();
    }

    public sealed class CardItem
    {
        public string Term { get; set; } = "";
        public string Definition { get; set; } = "";
        public string? Pinyin { get; set; } // optional: lấy từ (...) cuối dòng nếu có
        public bool IsStarred { get; set; }
        public int SrsLevel { get; set; }
        public string? SrsDueDate { get; set; }
        public string? SrsLastReviewedAt { get; set; }
        public int SrsReviewCount { get; set; }
        public int SrsLapseCount { get; set; }
    }
}
