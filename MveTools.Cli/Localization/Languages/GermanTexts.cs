namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class GermanTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Was möchtest du tun?",
			["InspectAction"] = "MVE prüfen",
			["ToVideoAction"] = "MVE in Video konvertieren",
			["ToMveAction"] = "Video in MVE konvertieren",
			["LanguageAction"] = "Sprache ändern",
			["QuitAction"] = "Beenden",
			["LanguageTitle"] = "Sprache",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Zielvideo",
			["SourceVideo"] = "Quellvideo (AVI oder MP4)",
			["TargetMve"] = "Ziel-MVE",
			["FormatTitle"] = "Ausgabeformat",
			["AutoFormat"] = "Automatisch: {0}",
			["FileNotFound"] = "Datei nicht gefunden.",
			["RequiredPath"] = "Pfad ist erforderlich.",
			["Done"] = "Fertig",
			["Step"] = "Schritt",
			["FormatMp4"] = "MP4/H.264 (System-ffmpeg)"
		};
	}
}