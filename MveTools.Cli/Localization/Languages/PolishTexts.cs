namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class PolishTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Co chcesz zrobić?",
			["InspectAction"] = "Sprawdź MVE",
			["ToVideoAction"] = "Konwertuj MVE na wideo",
			["ToMveAction"] = "Konwertuj wideo na MVE",
			["LanguageAction"] = "Zmień język",
			["QuitAction"] = "Zakończ",
			["LanguageTitle"] = "Język",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Docelowe wideo",
			["SourceVideo"] = "Wideo źródłowe (AVI lub MP4)",
			["TargetMve"] = "Docelowy MVE",
			["FormatTitle"] = "Format wyjściowy",
			["AutoFormat"] = "Automatycznie: {0}",
			["FileNotFound"] = "Nie znaleziono pliku.",
			["RequiredPath"] = "Ścieżka jest wymagana.",
			["Done"] = "Gotowe",
			["Step"] = "Krok",
			["FormatMp4"] = "MP4/H.264 (systemowy ffmpeg)"
		};
	}
}