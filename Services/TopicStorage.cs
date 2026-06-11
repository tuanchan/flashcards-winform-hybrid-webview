using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class TopicStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string GetTopicsFilePath()
        {
            return Path.Combine(CardSetStorage.BaseDir, "topics.json");
        }

        public static List<Topic> LoadAllTopics()
        {
            try
            {
                var filePath = GetTopicsFilePath();
                if (!File.Exists(filePath))
                {
                    return new List<Topic>();
                }

                var json = File.ReadAllText(filePath);
                var topics = JsonSerializer.Deserialize<List<Topic>>(json, JsonOptions);
                return topics ?? new List<Topic>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading topics: {ex.Message}");
                return new List<Topic>();
            }
        }

        public static void SaveAllTopics(List<Topic> topics)
        {
            try
            {
                var filePath = GetTopicsFilePath();
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(topics, JsonOptions);
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving topics: {ex.Message}");
            }
        }

        public static void SaveTopic(Topic topic)
        {
            if (topic == null || string.IsNullOrWhiteSpace(topic.Id))
                return;

            var topics = LoadAllTopics();
            var index = topics.FindIndex(t => string.Equals(t.Id, topic.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                topics[index] = topic;
            }
            else
            {
                topics.Add(topic);
            }

            SaveAllTopics(topics);
        }

        public static bool DeleteTopic(string topicId)
        {
            if (string.IsNullOrWhiteSpace(topicId))
                return false;

            var topics = LoadAllTopics();
            var target = topics.FirstOrDefault(t => string.Equals(t.Id, topicId, StringComparison.OrdinalIgnoreCase));
            if (target == null)
                return false;

            topics.Remove(target);
            SaveAllTopics(topics);

            // Update any CardSets using this topic to remove the reference
            try
            {
                var sets = CardSetStorage.LoadAllSetsSafe();
                foreach (var set in sets)
                {
                    if (string.Equals(set.TopicId, topicId, StringComparison.OrdinalIgnoreCase))
                    {
                        set.TopicId = null;
                        CardSetStorage.SaveSetJson(set);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing topic references from cardsets: {ex.Message}");
            }

            return true;
        }
    }
}
