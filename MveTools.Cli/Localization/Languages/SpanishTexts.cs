namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class SpanishTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "¿Qué quieres hacer?",
			["InspectAction"] = "Inspeccionar un MVE",
			["ToVideoAction"] = "Convertir MVE a vídeo",
			["ToMveAction"] = "Convertir vídeo a MVE",
			["LanguageAction"] = "Cambiar idioma",
			["QuitAction"] = "Salir",
			["LanguageTitle"] = "Idioma",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Vídeo de destino",
			["SourceVideo"] = "Vídeo de origen (AVI o MP4)",
			["TargetMve"] = "MVE de destino",
			["FormatTitle"] = "Formato de salida",
			["AutoFormat"] = "Automático: {0}",
			["FileNotFound"] = "Archivo no encontrado.",
			["RequiredPath"] = "Ruta obligatoria.",
			["Done"] = "Terminado",
			["Step"] = "Paso",
			["FormatMp4"] = "MP4/H.264 (ffmpeg del sistema)"
		};
	}
}