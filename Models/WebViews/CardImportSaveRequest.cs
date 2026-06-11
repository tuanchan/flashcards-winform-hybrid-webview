using System.Collections.Generic;

namespace TocflQuiz.Models.WebViews
{
    public sealed class CardImportSaveRequest
    {
        public string Title { get; set; } = "";
        public string Language { get; set; } = "";
        public string LanguageCode { get; set; } = "";
        public string RawInput { get; set; } = "";
        public string TermDefSep { get; set; } = "\t";
        public string CardSep { get; set; } = "\n";
        public string CoverImageSource { get; set; } = "";
        public bool AutoGenerateExamples { get; set; }
        public string? TopicId { get; set; }
        public List<CardImportSaveItem> Cards { get; set; } = new();
    }

    public sealed class CardImportSaveItem
    {
        public string Term { get; set; } = "";
        public string Definition { get; set; } = "";
        public string? Pinyin { get; set; }
    }
}
