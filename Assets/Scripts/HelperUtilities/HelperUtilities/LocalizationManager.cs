using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityHelperSDK.Data;



namespace UnityHelperSDK.HelperUtilities{


    /// <summary>
    /// A centralized Localization / I18n helper for loading language tables,
    /// retrieving translated strings with formatting and pluralization,
    /// and switching languages at runtime.
    /// Place JSON files under Resources/Localization/<lang>.json
    /// where <lang> is a culture code, e.g. "en", "fr", "es-US".
    /// </summary>

    public static class LocalizationManager
    {
        // Loaded translations for the current language
        private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Fallback (default) language code
        private static string _fallbackLanguage = "en";

        // Currently active language code
        private static string _currentLanguage = "";

        // Event fired when language changes
        public static event UnityAction<string> OnLanguageChanged;

        /// <summary>
        /// Initialize the manager and load the default language.
        /// Should be called on game start.
        /// </summary>
        public static void Initialize(string defaultLanguage = "en")
        {
            _fallbackLanguage = defaultLanguage;
            ChangeLanguage(Application.systemLanguage.ToString(), useFallback: true);
        }

        /// <summary>
        /// Change active language. Attempts to load Resources/Localization/{lang}.json.
        /// If missing and useFallback==true, loads fallback language.
        /// </summary>
        public static bool ChangeLanguage(string langCode, bool useFallback = true)
        {
            if (langCode == _currentLanguage) return true;

            var asset = Resources.Load<TextAsset>($"Localization/{langCode}");
            if (asset == null && useFallback)
                asset = Resources.Load<TextAsset>($"Localization/{_fallbackLanguage}");

            if (asset == null)
            {
                Debug.LogError($"[Localization] Could not load language '{langCode}' or fallback '{_fallbackLanguage}'.");
                return false;
            }

            try
            {
                _translations = JsonUtility.FromJson<LocalizationTable>(asset.text).ToDictionary();
                _currentLanguage = langCode;
                OnLanguageChanged?.Invoke(_currentLanguage);
                Debug.Log($"[Localization] Loaded language: {_currentLanguage}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Failed to parse '{langCode}' JSON: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Returns the translated string for key, or key itself if missing.
        /// Supports {0}, {1}… placeholders.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            if (!_translations.TryGetValue(key, out var format))
                format = $"[{key}]";

            return args != null && args.Length > 0
                ? string.Format(format, args)
                : format;
        }

        /// <summary>
        /// Returns a plural–aware localized string.
        /// Expects two entries: key + ".plural" and key + ".singular".
        /// Chooses based on count, then substitutes {0} with count.
        /// </summary>
        public static string GetPlural(string key, int count, params object[] extraArgs)
        {
            string entryKey = count == 1 ? $"{key}.singular" : $"{key}.plural";
            var args = new object[(extraArgs?.Length ?? 0) + 1];
            args[0] = count;
            if (extraArgs != null) Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
            return Get(entryKey, args);
        }

        /// <summary>
        /// Get all loaded keys.
        /// </summary>
        public static IEnumerable<string> GetAllKeys()
            => _translations.Keys;

        /// <summary>
        /// Returns list of available language codes by scanning Resources/Localization.
        /// Only works in Editor or with asset bundles; for builds, maintain your own list.
        /// </summary>
        public static List<string> GetAvailableLanguages()
        {
    #if UNITY_EDITOR
            var list = new List<string>();
            var folder = Path.Combine(Application.dataPath, "Resources/Localization");
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.GetFiles(folder, "*.json"))
                    list.Add(Path.GetFileNameWithoutExtension(file));
            }
            return list;
    #else
            // At runtime Resources can't list—return only current and fallback
            return new List<string> { _currentLanguage, _fallbackLanguage };
    #endif
        }

        /// <summary>
        /// Returns current language code.
        /// </summary>
        public static string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Returns fallback (default) language code.
        /// </summary>
        public static string FallbackLanguage => _fallbackLanguage;

        /// <summary>
        /// Format a date according to the current culture.
        /// Automatically uses the correct date format for the current language/culture.
        /// </summary>
        /// <param name="date">The date to format</param>
        /// <param name="format">Optional format string. If null, uses the culture's default format</param>
        /// <returns>Localized date string</returns>
        public static string FormatDate(DateTime date, string format = null)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                return format != null ? date.ToString(format, culture) : date.ToLocalTime().ToString(culture);
            }
            catch
            {
                return date.ToString(format);
            }
        }

        /// <summary>
        /// Format a number according to the current culture.
        /// Handles decimal separators, thousand separators, and currency symbols correctly.
        /// </summary>
        /// <param name="number">The number to format</param>
        /// <param name="format">Optional format string (e.g., "C" for currency, "N" for number, "P" for percent)</param>
        /// <returns>Localized number string</returns>
        public static string FormatNumber(decimal number, string format = null)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                return format != null ? number.ToString(format, culture) : number.ToString(culture);
            }
            catch
            {
                return number.ToString(format);
            }
        }

        /// <summary>
        /// Format currency amount according to the current culture.
        /// </summary>
        /// <param name="amount">The amount to format</param>
        /// <param name="currencyCode">Optional ISO currency code (e.g., "USD", "EUR"). If null, uses culture default</param>
        /// <returns>Localized currency string</returns>
        public static string FormatCurrency(decimal amount, string currencyCode = null)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                if (currencyCode != null)
                {
                    var regionInfo = new RegionInfo(culture.LCID);
                    culture = (CultureInfo)culture.Clone();
                    culture.NumberFormat.CurrencySymbol = currencyCode;
                }
                return amount.ToString("C", culture);
            }
            catch
            {
                return amount.ToString("C");
            }
        }

        /// <summary>
        /// Get a localized string with title case according to the current culture's rules.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Localized string in title case</returns>
        public static string GetTitleCase(string key, params object[] args)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                var textInfo = culture.TextInfo;
                return textInfo.ToTitleCase(Get(key, args).ToLower(culture));
            }
            catch
            {
                return Get(key, args);
            }
        }

        /// <summary>
        /// Get a localized string in upper case according to the current culture's rules.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Localized string in upper case</returns>
        public static string GetUpper(string key, params object[] args)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                return Get(key, args).ToUpper(culture);
            }
            catch
            {
                return Get(key, args).ToUpper();
            }
        }

        /// <summary>
        /// Get a localized string in lower case according to the current culture's rules.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Localized string in lower case</returns>
        public static string GetLower(string key, params object[] args)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                return Get(key, args).ToLower(culture);
            }
            catch
            {
                return Get(key, args).ToLower();
            }
        }

        /// <summary>
        /// Format a relative time span in a human-readable way (e.g., "2 hours ago", "in 5 minutes").
        /// Uses appropriate translation keys for each time unit.
        /// </summary>
        /// <param name="dateTime">The date/time to format relative to now</param>
        /// <returns>Localized relative time string</returns>
        public static string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            bool isFuture = timeSpan.TotalMilliseconds < 0;
            timeSpan = timeSpan.Duration();

            if (timeSpan.TotalSeconds < 60)
                return isFuture ? Get("time.in_seconds", (int)timeSpan.TotalSeconds) : Get("time.seconds_ago", (int)timeSpan.TotalSeconds);
            if (timeSpan.TotalMinutes < 60)
                return isFuture ? Get("time.in_minutes", (int)timeSpan.TotalMinutes) : Get("time.minutes_ago", (int)timeSpan.TotalMinutes);
            if (timeSpan.TotalHours < 24)
                return isFuture ? Get("time.in_hours", (int)timeSpan.TotalHours) : Get("time.hours_ago", (int)timeSpan.TotalHours);
            if (timeSpan.TotalDays < 30)
                return isFuture ? Get("time.in_days", (int)timeSpan.TotalDays) : Get("time.days_ago", (int)timeSpan.TotalDays);
            if (timeSpan.TotalDays < 365)
                return isFuture ? Get("time.in_months", (int)(timeSpan.TotalDays / 30)) : Get("time.months_ago", (int)(timeSpan.TotalDays / 30));
            
            return isFuture ? Get("time.in_years", (int)(timeSpan.TotalDays / 365)) : Get("time.years_ago", (int)(timeSpan.TotalDays / 365));
        }

        /// <summary>
        /// Format a list of items according to the current culture's list formatting rules.
        /// For example: "apple, banana, and orange" in English or "pomme, banane et orange" in French
        /// </summary>
        /// <param name="items">The list of items to format</param>
        /// <returns>Localized list string</returns>
        public static string FormatList(IEnumerable<string> items)
        {
            var itemsList = items.ToList();
            if (itemsList.Count == 0) return string.Empty;
            if (itemsList.Count == 1) return itemsList[0];
            if (itemsList.Count == 2) return Get("list.two_items", itemsList[0], itemsList[1]);

            var allButLast = string.Join(Get("list.separator"), itemsList.Take(itemsList.Count - 1));
            return Get("list.format", allButLast, itemsList.Last());
        }

        /// <summary>
        /// Get a random localized string from a numbered set of keys.
        /// Useful for variety in responses, e.g., "error.1", "error.2", "error.3" etc.
        /// </summary>
        /// <param name="baseKey">The base key without number</param>
        /// <param name="count">Number of variations available</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Random localized string from the set</returns>
        public static string GetRandom(string baseKey, int count, params object[] args)
        {
            if (count <= 0) return Get(baseKey, args);
            int index = UnityEngine.Random.Range(1, count + 1);
            return Get($"{baseKey}.{index}", args);
        }

        /// <summary>
        /// Check if a translation key exists in the current language.
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists</returns>
        public static bool HasKey(string key)
        {
            return _translations.ContainsKey(key);
        }

        /// <summary>
        /// Get all translations that start with a specific prefix.
        /// Useful for getting all related translations, e.g., all error messages.
        /// </summary>
        /// <param name="prefix">The prefix to search for</param>
        /// <returns>Dictionary of matching keys and their translations</returns>
        public static Dictionary<string, string> GetAllWithPrefix(string prefix)
        {
            return _translations
                .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a price string with currency symbol (e.g., "$12.34", "€15.99") and returns the currency symbol and numeric value.
        /// </summary>
        /// <param name="priceString">The price string to parse</param>
        /// <param name="currencySymbol">Output: The detected currency symbol (e.g., "$", "€")</param>
        /// <param name="amount">Output: The numeric value of the price</param>
        /// <returns>True if parsing was successful, false otherwise</returns>
        public static bool TryParsePrice(string priceString, out string currencySymbol, out decimal amount)
        {
            currencySymbol = null;
            amount = 0m;
            if (string.IsNullOrWhiteSpace(priceString))
                return false;

            priceString = priceString.Trim();
            // Find the first digit in the string
            int digitIndex = priceString.IndexOfAny("0123456789".ToCharArray());
            if (digitIndex == -1)
                return false;

            currencySymbol = priceString.Substring(0, digitIndex).Trim();
            string numberPart = priceString.Substring(digitIndex).Trim();
            if (decimal.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                return true;
            // Try with current culture as fallback
            if (decimal.TryParse(numberPart, NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
                return true;
            return false;
        }

        /// <summary>
        /// Extracts only the currency symbol from a price string (e.g., "$12.34" returns "$", "€15.99" returns "€").
        /// </summary>
        /// <param name="priceString">The price string to parse</param>
        /// <returns>The currency symbol, or null if not found</returns>
        public static string ExtractCurrencySymbol(string priceString)
        {
            if (string.IsNullOrWhiteSpace(priceString))
                return null;
            priceString = priceString.Trim();
            int digitIndex = priceString.IndexOfAny("0123456789".ToCharArray());
            if (digitIndex == -1)
                return null;
            return priceString.Substring(0, digitIndex).Trim();
        }

        /// <summary>
        /// Extracts only the numeric value from a price string (e.g., "$12.34" returns 12.34).
        /// </summary>
        /// <param name="priceString">The price string to parse</param>
        /// <returns>The numeric value, or null if not found/invalid</returns>
        public static decimal? ExtractPriceAmount(string priceString)
        {
            if (string.IsNullOrWhiteSpace(priceString))
                return null;
            priceString = priceString.Trim();
            int digitIndex = priceString.IndexOfAny("0123456789".ToCharArray());
            if (digitIndex == -1)
                return null;
            string numberPart = priceString.Substring(digitIndex).Trim();
            if (decimal.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                return amount;
            if (decimal.TryParse(numberPart, NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
                return amount;
            return null;
        }
        /// <summary>
        /// Simple table container matching JSON structure:
        /// { "entries": [ {"key":"HELLO","value":"Hello"} , ... ] }
        /// </summary>
        [Serializable]
        private class LocalizationTable
        {
            public Entry[] entries;
            [Serializable]
            public struct Entry { public string key; public string value; }

            public Dictionary<string, string> ToDictionary()
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (entries != null)
                    foreach (var e in entries)
                        if (!dict.ContainsKey(e.key))
                            dict.Add(e.key, e.value);
                return dict;
            }
        }

        /// <summary>
        /// Attempts to get a translation for a key, returns true if found, false otherwise.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="value">The translated value if found</param>
        /// <returns>True if the key exists and value is set</returns>
        public static bool TryGet(string key, out string value)
        {
            return _translations.TryGetValue(key, out value);
        }

        /// <summary>
        /// Reloads the current language from disk. Useful for hot-reloading in development.
        /// </summary>
        public static void ReloadCurrentLanguage()
        {
            ChangeLanguage(_currentLanguage, useFallback: true);
        }

        /// <summary>
        /// Returns a dictionary of all translations for the current language.
        /// </summary>
        public static Dictionary<string, string> GetAllTranslations()
        {
            return new Dictionary<string, string>(_translations, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the language display name (e.g., "English" for "en").
        /// </summary>
        /// <param name="langCode">The language code</param>
        /// <returns>Display name for the language</returns>
        public static string GetLanguageDisplayName(string langCode)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(langCode);
                return culture.EnglishName;
            }
            catch
            {
                return langCode;
            }
        }

        /// <summary>
        /// Returns the native language name (e.g., "Deutsch" for "de").
        /// </summary>
        /// <param name="langCode">The language code</param>
        /// <returns>Native name for the language</returns>
        public static string GetLanguageNativeName(string langCode)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(langCode);
                return culture.NativeName;
            }
            catch
            {
                return langCode;
            }
        }

        /// <summary>
        /// Returns a list of all available language display names.
        /// </summary>
        public static List<string> GetAvailableLanguageDisplayNames()
        {
            var codes = GetAvailableLanguages();
            var names = new List<string>();
            foreach (var code in codes)
                names.Add(GetLanguageDisplayName(code));
            return names;
        }

        /// <summary>
        /// Returns a list of all available language native names.
        /// </summary>
        public static List<string> GetAvailableLanguageNativeNames()
        {
            var codes = GetAvailableLanguages();
            var names = new List<string>();
            foreach (var code in codes)
                names.Add(GetLanguageNativeName(code));
            return names;
        }

        /// <summary>
        /// Returns the current system language code (Unity's SystemLanguage as string, e.g., "en", "fr").
        /// </summary>
        public static string GetSystemLanguageCode()
        {
            return Application.systemLanguage.ToString();
        }

        /// <summary>
        /// Returns a translation for a key, or a fallback value if not found.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="fallback">The fallback value if key is not found</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Translation or fallback</returns>
        public static string GetOrDefault(string key, string fallback, params object[] args)
        {
            if (!_translations.TryGetValue(key, out var format))
                format = fallback;
            return args != null && args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// Returns a translation for a key, or null if not found.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Translation or null</returns>
        public static string GetOrNull(string key, params object[] args)
        {
            if (!_translations.TryGetValue(key, out var format))
                return null;
            return args != null && args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// Returns a translation for a key, or throws an exception if not found.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Translation</returns>
        public static string GetOrThrow(string key, params object[] args)
        {
            if (!_translations.TryGetValue(key, out var format))
                throw new KeyNotFoundException($"Translation key not found: {key}");
            return args != null && args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// Returns a translation for a key, but strips all rich text tags (e.g., <b>, <i>, <color>).
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Translation with rich text tags removed</returns>
        public static string GetPlain(string key, params object[] args)
        {
            var str = Get(key, args);
            return System.Text.RegularExpressions.Regex.Replace(str, "<.*?>", string.Empty);
        }

        /// <summary>
        /// Returns a translation for a key, but with all whitespace collapsed to single spaces.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Translation with collapsed whitespace</returns>
        public static string GetCollapsedWhitespace(string key, params object[] args)
        {
            var str = Get(key, args);
            return System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Get a localized string for a given key. Returns the key if no translation exists.
        /// </summary>
        public static string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            
            // Try current language
            if (!string.IsNullOrEmpty(_currentLanguage) && 
                _translations.TryGetValue(key, out string translation))
            {
                return translation;
            }
            
            // Try fallback language
            if (_fallbackLanguage != _currentLanguage)
            {
                var fallbackTranslations = LoadTranslations(_fallbackLanguage);
                if (fallbackTranslations.TryGetValue(key, out translation))
                {
                    return translation;
                }
            }
            
            // Return key as fallback
            return key;
        }

        /// <summary>
        /// Load translations for a specific language from Resources
        /// </summary>
        private static Dictionary<string, string> LoadTranslations(string languageCode)
        {
            var textAsset = Resources.Load<TextAsset>($"Localization/{languageCode}");
            if (textAsset != null)
            {
                try
                {
                    return UnityHelperSDK.Data.JsonHelper.Deserialize<Dictionary<string, string>>(textAsset.text);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading translations for {languageCode}: {e.Message}");
                }
            }
            return new Dictionary<string, string>();
        }
    }
}