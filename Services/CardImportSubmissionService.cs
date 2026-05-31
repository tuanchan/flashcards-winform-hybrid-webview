using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Models;
using TocflQuiz.Models.WebViews;

namespace TocflQuiz.Services
{
    public static class CardImportSubmissionService
    {
        public static async Task<CardSet> SaveFromWebPayloadAsync(JsonElement root)
        {
            var request = Parse(root);
            var parsedCards = CardImportParser.Parse(request.RawInput, request.TermDefSep, request.CardSep);

            if (parsedCards.Count == 0 && request.Cards.Count > 0)
            {
                parsedCards = request.Cards
                    .Where(card => !string.IsNullOrWhiteSpace(card.Term) && !string.IsNullOrWhiteSpace(card.Definition))
                    .Select(card => new CardItem
                    {
                        Term = card.Term.Trim(),
                        Definition = card.Definition.Trim(),
                        Pinyin = string.IsNullOrWhiteSpace(card.Pinyin) ? null : card.Pinyin.Trim()
                    })
                    .ToList();
            }

            var set = new CardSet
            {
                Title = request.Title,
                Language = request.Language,
                LanguageCode = request.LanguageCode,
                CreatedAt = DateTime.Now,
                Items = new List<CardItem>(parsedCards)
            };

            CardSetStorage.SaveSet(set, request.RawInput, request.TermDefSep, request.CardSep);
            await TrySaveCoverImageAsync(set, request);
            TryGeneratePixabayImages(set);
            TryGenerateAudio(set);
            
            if (request.AutoGenerateExamples)
            {
                TryGenerateBulkExamples(set);
            }

            return set;
        }

        private static void TryGenerateBulkExamples(CardSet set)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await GeminiService.GenerateBulkExamplesAsync(set);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Generate bulk examples failed: {ex.Message}");
                }
            });
        }

        private static void TryGeneratePixabayImages(CardSet set)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PixabayImageService.GenerateVocabularyImagesAsync(set);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Generate Pixabay images failed: {ex.Message}");
                }
            });
        }

        private static void TryGenerateAudio(CardSet set)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await CourseAudioService.GenerateMissingAudioAsync(set);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Generate course audio failed: {ex.Message}");
                }
            });
        }

        private static async Task TrySaveCoverImageAsync(CardSet set, CardImportSaveRequest request)
        {
            try
            {
                var firstTerm = set.Items?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Term))?.Term ?? "";
                var savedPath = await CourseCoverImageService
                    .SaveCoverAsync(set, request.CoverImageSource, firstTerm);

                if (string.IsNullOrWhiteSpace(savedPath))
                    return;

                set.CoverImagePath = savedPath;
                CardSetStorage.SaveSetJson(set);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save course cover failed: {ex.Message}");
            }
        }

        private static CardImportSaveRequest Parse(JsonElement root)
        {
            var request = new CardImportSaveRequest
            {
                Title = root.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString() ?? ""
                    : "",
                Language = root.TryGetProperty("language", out var languageElement)
                    ? languageElement.GetString() ?? ""
                    : "",
                LanguageCode = NormalizeImportLanguageCode(root.TryGetProperty("languageCode", out var languageCodeElement)
                    ? languageCodeElement.GetString()
                    : ""),
                RawInput = root.TryGetProperty("rawInput", out var rawInputElement)
                    ? rawInputElement.GetString() ?? ""
                    : "",
                CoverImageSource = root.TryGetProperty("coverImageSource", out var coverImageElement)
                    ? coverImageElement.GetString() ?? ""
                    : "",
                TermDefSep = root.TryGetProperty("termDefSep", out var termDefSepElement)
                    ? termDefSepElement.GetString() ?? "\t"
                    : "\t",
                CardSep = root.TryGetProperty("cardSep", out var cardSepElement)
                    ? cardSepElement.GetString() ?? "\n"
                    : "\n",
                AutoGenerateExamples = root.TryGetProperty("autoGenerateExamples", out var autoGenElement) && autoGenElement.GetBoolean()
            };

            if (!root.TryGetProperty("cards", out var cardsElement))
                return request;

            foreach (var card in cardsElement.EnumerateArray())
            {
                request.Cards.Add(new CardImportSaveItem
                {
                    Term = card.TryGetProperty("term", out var termElement)
                        ? termElement.GetString() ?? ""
                        : "",
                    Definition = card.TryGetProperty("definition", out var definitionElement)
                        ? definitionElement.GetString() ?? ""
                        : "",
                    Pinyin = card.TryGetProperty("pinyin", out var pinyinElement)
                        ? pinyinElement.GetString()
                        : null
                });
            }

            return request;
        }

        private static string NormalizeImportLanguageCode(string? languageCode)
        {
            var code = (languageCode ?? "").Trim();
            var lower = code.ToLowerInvariant();

            return lower switch
            {
                "zh" or "zh-tw" or "zh-hant" or "zh-hk" or "zh-mo" => "zh-TW",
                "zh-cn" or "zh-hans" or "zh-sg" => "zh-CN",
                _ => code
            };
        }
    }
}
