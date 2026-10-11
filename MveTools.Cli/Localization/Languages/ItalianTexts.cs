namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class ItalianTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Cosa vuoi fare?",
			["InspectAction"] = "Ispeziona un MVE",
			["ToVideoAction"] = "Converti MVE in video",
			["ToMveAction"] = "Converti video in MVE",
			["LanguageAction"] = "Cambia lingua",
			["QuitAction"] = "Esci",
			["LanguageTitle"] = "Lingua",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Video di destinazione",
			["SourceVideo"] = "Video sorgente (AVI o MP4)",
			["TargetMve"] = "MVE di destinazione",
			["FormatTitle"] = "Formato di uscita",
			["AutoFormat"] = "Automatico: {0}",
			["FileNotFound"] = "File non trovato.",
			["RequiredPath"] = "Percorso obbligatorio.",
			["Done"] = "Completato",
			["Step"] = "Passaggio",
			["FormatMp4"] = "MP4/H.264 (ffmpeg di sistema)"
		};
	}
}