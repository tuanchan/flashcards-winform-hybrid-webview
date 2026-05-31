using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TocflQuiz.Services;

namespace TocflQuiz.Models
{
    public sealed class CardSetConfig
    {
        public string Title { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string Language { get; set; } = "";
        public string LanguageCode { get; set; } = "";
        public int VocabCount { get; set; }
        public string FolderName { get; set; } = "";
        public string CoverImagePath { get; set; } = "";
        public string RelativeVocabPath { get; set; } = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.VocabsFileNameValue}";
        public string RelativeNotYetPath { get; set; } = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.NotYetFileNameValue}";
        public string RelativeAudioDir { get; set; } = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.AudioFolderNameValue}";
    }
}
