namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class PortugueseTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "O que deseja fazer?",
			["InspectAction"] = "Inspecionar um MVE",
			["ToVideoAction"] = "Converter MVE para vídeo",
			["ToMveAction"] = "Converter vídeo para MVE",
			["LanguageAction"] = "Alterar idioma",
			["QuitAction"] = "Sair",
			["LanguageTitle"] = "Idioma",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Vídeo de destino",
			["SourceVideo"] = "Vídeo de origem (AVI ou MP4)",
			["TargetMve"] = "MVE de destino",
			["FormatTitle"] = "Formato de saída",
			["AutoFormat"] = "Automático: {0}",
			["FileNotFound"] = "Arquivo não encontrado.",
			["RequiredPath"] = "Caminho obrigatório.",
			["Done"] = "Concluído",
			["Step"] = "Etapa",
			["FormatMp4"] = "MP4/H.264 (ffmpeg do sistema)"
		};
	}
}