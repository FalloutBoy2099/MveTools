namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class CzechTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Co chceš udělat?",
			["InspectAction"] = "Zkontrolovat MVE",
			["ToVideoAction"] = "Převést MVE na video",
			["ToMveAction"] = "Převést video na MVE",
			["LanguageAction"] = "Změnit jazyk",
			["QuitAction"] = "Konec",
			["LanguageTitle"] = "Jazyk",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Cílové video",
			["SourceVideo"] = "Zdrojové video (AVI nebo MP4)",
			["TargetMve"] = "Cílové MVE",
			["FormatTitle"] = "Výstupní formát",
			["AutoFormat"] = "Automaticky: {0}",
			["FileNotFound"] = "Soubor nenalezen.",
			["RequiredPath"] = "Cesta je povinná.",
			["Done"] = "Hotovo",
			["Step"] = "Krok",
			["FormatMp4"] = "MP4/H.264 (systémový ffmpeg)"
		};
	}
}