using System;
using System.Collections.Generic;
using System.Text;

namespace KanadeSA.Core
{
    public static class LanguageExtensions
    {
        public static string ToJsonKey(this LanguageType language)
        {
            return language switch
            {
                LanguageType.English => "english",
                LanguageType.TChinese => "tchinese",
                LanguageType.SChinese => "schinese",
                LanguageType.Russian => "russian",
                LanguageType.Koreana => "koreana",
                LanguageType.Japanese => "japanese",
                LanguageType.German => "german",
                LanguageType.Italian => "italian",
                LanguageType.French => "french",
                LanguageType.Spanish => "spanish",
                LanguageType.Latam => "latam",
                LanguageType.Brazilian => "brazilian",
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
            };
        }

        public static string GetLocalized(this LocalizationRoot root, string key, LanguageType language, string fallback = null)
        {
            if (root != null && root.TryGetValue(key, out var entry))
            {
                string jsonKey = language.ToJsonKey();
                var prop = typeof(LocalizationEntry).GetProperty(jsonKey);
                if (prop != null)
                {
                    string value = prop.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            return fallback;
        }

        /// <summary>
        /// 根据本地化文本逆向查找键（仅搜索指定前缀的键）
        /// </summary>
        /// <param name="root">本地化字典</param>
        /// <param name="language">目标语言</param>
        /// <param name="searchText">要搜索的文本</param>
        /// <param name="comparison">字符串比较方式（默认忽略大小写）</param>
        /// <param name="keyPrefix">只搜索以该前缀开头的键（默认 "Item."）</param>
        /// <returns>匹配的键列表（若 root 或 searchText 为空则返回空列表）</returns>
        public static List<string> FindKeysByLocalizedText(
            this LocalizationRoot root,
            LanguageType language,
            string searchText,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase,
            string keyPrefix = "Item.")
        {
            if (root == null || string.IsNullOrEmpty(searchText))
                return new List<string>();

            string searchProcessed = searchText;
            if (language == LanguageType.Japanese)
            {
                searchProcessed = NormalizeForJapanese(searchText);
            }

            string jsonKey = language.ToJsonKey();
            var results = new List<string>();
            var prop = typeof(LocalizationEntry).GetProperty(jsonKey);
            if (prop == null) return results;

            foreach (var kvp in root)
            {
                string key = kvp.Key;
                if (!string.IsNullOrEmpty(keyPrefix) && !key.StartsWith(keyPrefix))
                    continue;

                var entry = kvp.Value;
                if (entry == null) continue;

                string localizedText = prop.GetValue(entry) as string;
                if (string.IsNullOrEmpty(localizedText)) continue;

                string textProcessed = localizedText;
                if (language == LanguageType.Japanese)
                {
                    textProcessed = NormalizeForJapanese(localizedText);
                }

                if (textProcessed.IndexOf(searchProcessed, comparison) >= 0)
                {
                    results.Add(key);
                }
            }
            return results;
        }

        private static string NormalizeForJapanese(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. FormKC 会把全角英数(Ａ->A)、半角片假名(ｶ->カ)转为标准全角片假名
            string normalized = text.Normalize(NormalizationForm.FormKC);

            // 2. 将全角片假名 (カタカナ) 映射到对应的平假名 (かたかな)
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                // 全角片假名范围 U+30A1 ~ U+30F6，平假名范围 U+3041 ~ U+3096，差值为 0x60
                if (c >= 0x30A1 && c <= 0x30F6)
                {
                    sb.Append((char)(c - 0x0060));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    [System.Serializable]
    public class LocalizationEntry
    {
        public string english { get; set; }
        public string tchinese { get; set; }
        public string schinese { get; set; }
        public string russian { get; set; }
        public string koreana { get; set; }
        public string japanese { get; set; }
        public string german { get; set; }
        public string italian { get; set; }
        public string french { get; set; }
        public string spanish { get; set; }
        public string latam { get; set; }
        public string brazilian { get; set; }
    }

    [System.Serializable]
    public class LocalizationRoot : Dictionary<string, LocalizationEntry> { }

    public enum LanguageType
    {
        English,
        TChinese,
        SChinese,
        Russian,
        Koreana,
        Japanese,
        German,
        Italian,
        French,
        Spanish,
        Latam,
        Brazilian
    }
}