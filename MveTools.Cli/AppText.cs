namespace MveTools.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using MveTools.Conversion;

	internal static partial class AppText
	{
		private static readonly Dictionary<CliLanguage, Dictionary<String, String>> Texts = CreateTexts();

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
			if (Texts.TryGetValue(Current, out Dictionary<String, String>? current) && current.TryGetValue(key, out String? value))
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

		private static Dictionary<CliLanguage, Dictionary<String, String>> CreateTexts()
		{
			Dictionary<String, String> english = CreateEnglishTexts();

			return new Dictionary<CliLanguage, Dictionary<String, String>>
			{
				[CliLanguage.English] = english,
				[CliLanguage.French] = Merge(english, CreateFrenchTexts()),
				[CliLanguage.Spanish] = Merge(english, Basic("¿Qué quieres hacer?", "Inspeccionar un MVE", "Convertir MVE a vídeo", "Convertir vídeo a MVE", "Cambiar idioma", "Salir", "Idioma", "Vídeo de destino", "Vídeo de origen (AVI o MP4)", "MVE de destino", "Formato de salida", "Automático: {0}", "Archivo no encontrado.", "Ruta obligatoria.", "Terminado", "Paso", "MP4/H.264 (ffmpeg del sistema)")),
				[CliLanguage.Italian] = Merge(english, Basic("Cosa vuoi fare?", "Ispeziona un MVE", "Converti MVE in video", "Converti video in MVE", "Cambia lingua", "Esci", "Lingua", "Video di destinazione", "Video sorgente (AVI o MP4)", "MVE di destinazione", "Formato di uscita", "Automatico: {0}", "File non trovato.", "Percorso obbligatorio.", "Completato", "Passaggio", "MP4/H.264 (ffmpeg di sistema)")),
				[CliLanguage.German] = Merge(english, Basic("Was möchtest du tun?", "MVE prüfen", "MVE in Video konvertieren", "Video in MVE konvertieren", "Sprache ändern", "Beenden", "Sprache", "Zielvideo", "Quellvideo (AVI oder MP4)", "Ziel-MVE", "Ausgabeformat", "Automatisch: {0}", "Datei nicht gefunden.", "Pfad ist erforderlich.", "Fertig", "Schritt", "MP4/H.264 (System-ffmpeg)")),
				[CliLanguage.Portuguese] = Merge(english, Basic("O que deseja fazer?", "Inspecionar um MVE", "Converter MVE para vídeo", "Converter vídeo para MVE", "Alterar idioma", "Sair", "Idioma", "Vídeo de destino", "Vídeo de origem (AVI ou MP4)", "MVE de destino", "Formato de saída", "Automático: {0}", "Arquivo não encontrado.", "Caminho obrigatório.", "Concluído", "Etapa", "MP4/H.264 (ffmpeg do sistema)")),
				[CliLanguage.Polish] = Merge(english, Basic("Co chcesz zrobić?", "Sprawdź MVE", "Konwertuj MVE na wideo", "Konwertuj wideo na MVE", "Zmień język", "Zakończ", "Język", "Docelowe wideo", "Wideo źródłowe (AVI lub MP4)", "Docelowy MVE", "Format wyjściowy", "Automatycznie: {0}", "Nie znaleziono pliku.", "Ścieżka jest wymagana.", "Gotowe", "Krok", "MP4/H.264 (systemowy ffmpeg)")),
				[CliLanguage.Czech] = Merge(english, Basic("Co chceš udělat?", "Zkontrolovat MVE", "Převést MVE na video", "Převést video na MVE", "Změnit jazyk", "Konec", "Jazyk", "Cílové video", "Zdrojové video (AVI nebo MP4)", "Cílové MVE", "Výstupní formát", "Automaticky: {0}", "Soubor nenalezen.", "Cesta je povinná.", "Hotovo", "Krok", "MP4/H.264 (systémový ffmpeg)")),
				[CliLanguage.Swedish] = Merge(english, Basic("Vad vill du göra?", "Inspektera en MVE", "Konvertera MVE till video", "Konvertera video till MVE", "Ändra språk", "Avsluta", "Språk", "Målvideo", "Källvideo (AVI eller MP4)", "Mål-MVE", "Utdataformat", "Automatiskt: {0}", "Filen hittades inte.", "Sökväg krävs.", "Klart", "Steg", "MP4/H.264 (systemets ffmpeg)")),
				[CliLanguage.Chinese] = Merge(english, Basic("你想做什么？", "检查 MVE", "将 MVE 转换为视频", "将视频转换为 MVE", "更改语言", "退出", "语言", "目标视频", "源视频（AVI 或 MP4）", "目标 MVE", "输出格式", "自动：{0}", "找不到文件。", "必须提供路径。", "完成", "步骤", "MP4/H.264（系统 ffmpeg）")),
				[CliLanguage.Russian] = Merge(english, Basic("Что вы хотите сделать?", "Проверить MVE", "Конвертировать MVE в видео", "Конвертировать видео в MVE", "Изменить язык", "Выход", "Язык", "Целевое видео", "Исходное видео (AVI или MP4)", "Целевой MVE", "Формат вывода", "Автоматически: {0}", "Файл не найден.", "Требуется путь.", "Готово", "Шаг", "MP4/H.264 (системный ffmpeg)"))
			};
		}

		private static Dictionary<String, String> Basic(String mainMenuTitle, String inspect, String toVideo, String toMve, String language, String quit, String languageTitle, String targetVideo, String sourceVideo, String targetMve, String formatTitle, String autoFormat, String fileNotFound, String requiredPath, String done, String step, String mp4)
		{
			return new Dictionary<String, String>(StringComparer.Ordinal)
			{
				["MainMenuTitle"] = mainMenuTitle,
				["InspectAction"] = inspect,
				["ToVideoAction"] = toVideo,
				["ToMveAction"] = toMve,
				["LanguageAction"] = language,
				["QuitAction"] = quit,
				["LanguageTitle"] = languageTitle,
				["LanguageChanged"] = "{0}",
				["TargetVideo"] = targetVideo,
				["SourceVideo"] = sourceVideo,
				["TargetMve"] = targetMve,
				["FormatTitle"] = formatTitle,
				["AutoFormat"] = autoFormat,
				["FileNotFound"] = fileNotFound,
				["RequiredPath"] = requiredPath,
				["Done"] = done,
				["Step"] = step,
				["FormatMp4"] = mp4
			};
		}

		private static Dictionary<String, String> Merge(Dictionary<String, String> fallback, Dictionary<String, String> overlay)
		{
			Dictionary<String, String> merged = new(fallback, StringComparer.Ordinal);
			foreach (KeyValuePair<String, String> pair in overlay)
			{
				merged[pair.Key] = pair.Value;
			}

			return merged;
		}
	}
}