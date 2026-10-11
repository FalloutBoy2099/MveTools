namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class SwedishTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Vad vill du göra?",
			["InspectAction"] = "Inspektera en MVE",
			["ToVideoAction"] = "Konvertera MVE till video",
			["ToMveAction"] = "Konvertera video till MVE",
			["LanguageAction"] = "Ändra språk",
			["QuitAction"] = "Avsluta",
			["LanguageTitle"] = "Språk",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Målvideo",
			["SourceVideo"] = "Källvideo (AVI eller MP4)",
			["TargetMve"] = "Mål-MVE",
			["FormatTitle"] = "Utdataformat",
			["AutoFormat"] = "Automatiskt: {0}",
			["FileNotFound"] = "Filen hittades inte.",
			["RequiredPath"] = "Sökväg krävs.",
			["Done"] = "Klart",
			["Step"] = "Steg",
			["FormatMp4"] = "MP4/H.264 (systemets ffmpeg)"
		};
	}
}