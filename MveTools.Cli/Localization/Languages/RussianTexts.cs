namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class RussianTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "Что вы хотите сделать?",
			["InspectAction"] = "Проверить MVE",
			["ToVideoAction"] = "Конвертировать MVE в видео",
			["ToMveAction"] = "Конвертировать видео в MVE",
			["LanguageAction"] = "Изменить язык",
			["QuitAction"] = "Выход",
			["LanguageTitle"] = "Язык",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "Целевое видео",
			["SourceVideo"] = "Исходное видео (AVI или MP4)",
			["TargetMve"] = "Целевой MVE",
			["FormatTitle"] = "Формат вывода",
			["AutoFormat"] = "Автоматически: {0}",
			["FileNotFound"] = "Файл не найден.",
			["RequiredPath"] = "Требуется путь.",
			["Done"] = "Готово",
			["Step"] = "Шаг",
			["FormatMp4"] = "MP4/H.264 (системный ffmpeg)"
		};
	}
}