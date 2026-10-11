namespace MveTools.Cli.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using MveTools.Conversion;
	using Languages;

	internal static partial class AppText
	{
		private static readonly Dictionary<CliLanguage, IReadOnlyDictionary<String, String>> Texts = CreateTexts();

		public static CliLanguage Current { get; private set; } = DetectSystemLanguage();

		public static IReadOnlyList<LanguageOption> Languages { get; } =
		[
			new(CliLanguage.English, "English"),
			new(CliLanguage.French, "Français"),
			new(CliLanguage.Spanish, "Español"),
			new(CliLanguage.Italian, "Italiano"),
			new(CliLanguage.German, "Deutsch"),
			new(CliLanguage.Portuguese, "Português"),
			new(CliLanguage.Polish, "Polski"),
			new(CliLanguage.Czech, "Čeština"),
			new(CliLanguage.Swedish, "Svenska"),
			new(CliLanguage.Chinese, "中文"),
			new(CliLanguage.Russian, "Русский")
		];

		public static void SetLanguage(CliLanguage language)
		{
			Current = language;
			String cultureName = language switch
			{
				CliLanguage.French => "fr-FR",
				CliLanguage.Spanish => "es-ES",
				CliLanguage.Italian => "it-IT",
				CliLanguage.German => "de-DE",
				CliLanguage.Portuguese => "pt-PT",
				CliLanguage.Polish => "pl-PL",
				CliLanguage.Czech => "cs-CZ",
				CliLanguage.Swedish => "sv-SE",
				CliLanguage.Chinese => "zh-CN",
				CliLanguage.Russian => "ru-RU",
				_ => "en-US"
			};
			CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
			CultureInfo.CurrentCulture = culture;
			CultureInfo.CurrentUICulture = culture;
		}

		public static String Get(String key)
		{
			if (Texts.TryGetValue(Current, out IReadOnlyDictionary<String, String>? current) && current.TryGetValue(key, out String? value))
			{
				return value;
			}

			return Texts[CliLanguage.English][key];
		}

		public static String Format(String key, params Object[] values)
		{
			return String.Format(CultureInfo.CurrentCulture, Get(key), values);
		}

		public static String DescribeVideoStandard(VideoStandard standard)
		{
			return standard switch
			{
				VideoStandard.AviIndexed8 => Get("FormatAvi8"),
				VideoStandard.AviRgb24 => Get("FormatAvi24"),
				VideoStandard.WavPcm => Get("FormatWav"),
				VideoStandard.Mp4H264 => Get("FormatMp4"),
				_ => standard.ToString()
			};
		}

		public static String DescribeCompressionMode(MveCompressionMode mode)
		{
			return mode switch
			{
				MveCompressionMode.HighQuality => Get("CompressionHighQuality"),
				MveCompressionMode.Old => Get("CompressionOld"),
				_ => Get("CompressionDefault")
			};
		}

		private static CliLanguage DetectSystemLanguage()
		{
			return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
			{
				"fr" => CliLanguage.French,
				"es" => CliLanguage.Spanish,
				"it" => CliLanguage.Italian,
				"de" => CliLanguage.German,
				"pt" => CliLanguage.Portuguese,
				"pl" => CliLanguage.Polish,
				"cs" => CliLanguage.Czech,
				"sv" => CliLanguage.Swedish,
				"zh" => CliLanguage.Chinese,
				"ru" => CliLanguage.Russian,
				_ => CliLanguage.English
			};
		}

		private static Dictionary<CliLanguage, IReadOnlyDictionary<String, String>> CreateTexts()
		{
			return new Dictionary<CliLanguage, IReadOnlyDictionary<String, String>>
			{
				[CliLanguage.English] = EnglishTexts.Entries,
				[CliLanguage.French] = FrenchTexts.Entries,
				[CliLanguage.Spanish] = SpanishTexts.Entries,
				[CliLanguage.Italian] = ItalianTexts.Entries,
				[CliLanguage.German] = GermanTexts.Entries,
				[CliLanguage.Portuguese] = PortugueseTexts.Entries,
				[CliLanguage.Polish] = PolishTexts.Entries,
				[CliLanguage.Czech] = CzechTexts.Entries,
				[CliLanguage.Swedish] = SwedishTexts.Entries,
				[CliLanguage.Chinese] = ChineseTexts.Entries,
				[CliLanguage.Russian] = RussianTexts.Entries
			};
		}
	}
}