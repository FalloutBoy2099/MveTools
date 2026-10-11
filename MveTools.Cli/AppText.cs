namespace MveTools.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using MveTools.Conversion;

	internal static class AppText
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

		public static String LocalizeStep(String step)
		{
			if (Current != CliLanguage.French)
			{
				return step;
			}

			if (step == "Read MVE with integrated 8 bpp video and audio decoder")
			{
				return "Lecture du MVE avec le décodeur vidéo 8 bpp et audio intégré";
			}

			if (step == "Wrote AVI DIB indexed 8 bpp with PCM audio without external tools")
			{
				return "Écriture AVI DIB 8 bpp palettisé avec audio PCM, sans outil externe";
			}

			if (step == "Wrote AVI DIB RGB 24 bpp with PCM audio without external tools")
			{
				return "Écriture AVI DIB RGB 24 bpp avec audio PCM, sans outil externe";
			}

			if (step == "Wrote WAV PCM 16-bit audio without external tools")
			{
				return "Écriture audio WAV PCM 16 bits, sans outil externe";
			}

			if (step == "Wrote MP4/H.264 with system FFmpeg")
			{
				return "Écriture MP4/H.264 avec le FFmpeg du système";
			}

			if (step.StartsWith("Wrote MP4/H.264 with system FFmpeg using CRF ", StringComparison.Ordinal))
			{
				String value = step["Wrote MP4/H.264 with system FFmpeg using CRF ".Length..];
				return $"Écriture MP4/H.264 avec le FFmpeg du système en CRF {value}";
			}

			if (step == "Read AVI DIB and PCM audio with integrated reader")
			{
				return "Lecture AVI DIB et audio PCM avec le lecteur intégré";
			}

			if (step == "Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader")
			{
				return "Conversion de la vidéo source en AVI temporaire non compressé avec le FFmpeg du système, puis lecture avec le lecteur intégré";
			}

			if (step.StartsWith("Detected source video profile: ", StringComparison.Ordinal))
			{
				String value = step["Detected source video profile: ".Length..].Replace(" at ", " à ", StringComparison.Ordinal).Replace(" fps", " i/s", StringComparison.Ordinal);
				return $"Profil vidéo source détecté : {value}";
			}

			if (step.StartsWith("Selected frame range: ", StringComparison.Ordinal))
			{
				String value = step["Selected frame range: ".Length..].Replace(" frame(s)", " image(s)", StringComparison.Ordinal);
				return $"Plage sélectionnée : {value}";
			}

			if (step.StartsWith("Selected audio: PCM ", StringComparison.Ordinal))
			{
				String value = step["Selected audio: ".Length..]
					.Replace("-bit", " bits", StringComparison.Ordinal)
					.Replace(" channel(s)", " canal/canaux", StringComparison.Ordinal);
				return $"Audio sélectionné : {value}";
			}

			if (step == "Selected audio: none")
			{
				return "Audio sélectionné : aucun";
			}

			if (step.StartsWith("Compression profile: ", StringComparison.Ordinal))
			{
				String value = step["Compression profile: ".Length..] switch
				{
					"HighQuality" => Get("CompressionHighQuality"),
					"Old" => Get("CompressionOld"),
					_ => Get("CompressionDefault")
				};
				return $"Profil de compression : {value}";
			}

			if (step.StartsWith("Bitrate target requested: ", StringComparison.Ordinal))
			{
				String value = BeforeParenthesis(step["Bitrate target requested: ".Length..]);
				return $"Débit cible demandé : {value} (conservé comme intention de conversion ; l'encodeur intégré reste sans perte par bloc).";
			}

			if (step.StartsWith("Size limit requested: ", StringComparison.Ordinal))
			{
				String value = BeforeParenthesis(step["Size limit requested: ".Length..]);
				return $"Taille cible demandée : {value} (conservée comme intention de conversion ; l'encodeur intégré reste sans perte par bloc).";
			}

			if (step == "Test mode: input and parameters validated; MVE writing skipped.")
			{
				return "Mode simulation : entrée et paramètres validés ; écriture MVE ignorée.";
			}

			if (step == "Wrote compressed Interplay MVE 0x11 video without audio and without external tools")
			{
				return "Écriture vidéo Interplay MVE 0x11 compressée sans audio et sans outil externe";
			}

			if (step == "Wrote compressed Interplay MVE 0x11 video with MVE DPCM audio without external tools")
			{
				return "Écriture vidéo Interplay MVE 0x11 compressée avec audio MVE DPCM et sans outil externe";
			}

			if (step.StartsWith("Compressed frame exceeded the MVE payload limit; downscaled video from ", StringComparison.Ordinal))
			{
				String value = step["Compressed frame exceeded the MVE payload limit; downscaled video from ".Length..].Replace(" to ", " à ", StringComparison.Ordinal).Replace(" and retried.", "", StringComparison.Ordinal);
				return $"Une image compressée dépassait la limite MVE ; vidéo réduite de {value}, puis nouvel essai.";
			}

			if (step.StartsWith("Adjusted video dimensions from ", StringComparison.Ordinal))
			{
				String value = step["Adjusted video dimensions from ".Length..].Replace(" to ", " à ", StringComparison.Ordinal).Replace(" so MVE blocks stay aligned to 8 pixels.", "", StringComparison.Ordinal);
				return $"Dimensions vidéo ajustées de {value} pour aligner les blocs MVE sur 8 pixels.";
			}

			return step;
		}

		private static String BeforeParenthesis(String value)
		{
			Int32 index = value.IndexOf(" (", StringComparison.Ordinal);
			return index < 0 ? value : value[..index];
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
			Dictionary<String, String> english = new(StringComparer.Ordinal)
			{
				["ToolkitTagline"] = "Fallout / Interplay MVE conversion toolkit",
				["MainMenuTitle"] = "What do you want to do?",
				["InspectAction"] = "Inspect an MVE",
				["ToVideoAction"] = "Convert MVE to video",
				["ToMveAction"] = "Convert video to MVE",
				["LanguageAction"] = "Change language",
				["QuitAction"] = "Quit",
				["LanguageTitle"] = "Language",
				["LanguageChanged"] = "Language changed to {0}.",
				["InputMvePath"] = "MVE file path",
				["SourceMve"] = "Source MVE",
				["TargetVideo"] = "Target video",
				["SourceVideo"] = "Source video (AVI or MP4)",
				["TargetMve"] = "Target MVE",
				["ToVideoStatus"] = "Converting MVE to video...",
				["ToMveStatus"] = "Converting video to MVE...",
				["VideoToMveHeader"] = "Video to MVE",
				["VideoToMveIntro"] = "Integrated MVE encoder\nAvailable options: AVI/MP4 source, test mode, palette, external audio, frame range, dithering, compression profile, bitrate/size hints.",
				["AudioTitle"] = "Audio",
				["AudioKeep"] = "Keep video audio",
				["AudioNone"] = "No audio (-ns)",
				["AudioExternal"] = "External audio (-w)",
				["AudioExternalPrompt"] = "External AVI/WAV PCM audio source",
				["PaletteTitle"] = "Palette",
				["PaletteVideo"] = "Video palette",
				["PaletteLoad"] = "Load ACT/PAL palette (-p)",
				["PaletteSave"] = "Save ACT palette",
				["PalettePrompt"] = "ACT/PAL palette",
				["PaletteSavePrompt"] = "Target ACT palette",
				["FrameRangeTitle"] = "Frame range",
				["FrameAll"] = "Convert everything",
				["FrameStartEnd"] = "Set start/end",
				["FrameStartLen"] = "Set start/len",
				["StartFrame"] = "Start frame (-start)",
				["EndFrame"] = "End frame (-end)",
				["FrameCount"] = "Frame count (-len)",
				["CompressionTitle"] = "Compression profile",
				["CompressionDefault"] = "Default",
				["CompressionHighQuality"] = "High quality",
				["CompressionOld"] = "Old",
				["LimitTitle"] = "Output limit",
				["LimitNone"] = "None",
				["LimitRate"] = "Target bitrate (-rate)",
				["LimitSize"] = "Target size (-size)",
				["RatePrompt"] = "KiB/s (-rate)",
				["SizePrompt"] = "MiB (-size)",
				["AudioSampleRatePrompt"] = "Audio sample rate in Hz",
				["QualityPrompt"] = "MP4/H.264 CRF quality (0 lossless, 18 visually lossless, 51 smallest)",
				["OverwriteConfirm"] = "Overwrite target file if it already exists?",
				["VideoOverrideConfirm"] = "Override detected width, height, and fps?",
				["WidthPrompt"] = "Width in pixels",
				["HeightPrompt"] = "Height in pixels",
				["FpsPrompt"] = "Frames per second",
				["DitherConfirm"] = "Enable Floyd-Steinberg (-dither)?",
				["TestConfirm"] = "Test mode only (-test)?",
				["Yes"] = "Yes",
				["No"] = "No",
				["FileNotFound"] = "File not found.",
				["InvalidVideoSource"] = "This command expects an AVI or MP4 video source. To convert an MVE to a video, choose the MVE to video menu.",
				["RequiredPath"] = "Path is required.",
				["RangeError"] = "Between {0} and {1}.",
				["UnknownCommand"] = "Unknown command: {0}",
				["UsageInspect"] = "  mveconverter inspect <input.mve>",
				["UsageToVideo"] = "  mveconverter to-video <input.mve> <output> [--format avi8|avi24|wav|mp4]",
				["UsageToMve"] = "  mveconverter to-mve <input.avi|mp4|m4v|mov> <output.mve> [options]",
				["FormatUnknown"] = "Unknown output format: {0}. Use avi8, avi24, wav, or mp4.",
				["FormatTitle"] = "Output format",
				["AutoFormat"] = "Automatic: {0}",
				["FormatMp4"] = "MP4/H.264 (system ffmpeg)",
				["FormatAvi8"] = "AVI 8 bpp indexed",
				["FormatAvi24"] = "AVI 24 bpp RGB",
				["FormatWav"] = "WAV audio only",
				["File"] = "File",
				["Chunks"] = "Chunks",
				["VideoChunks"] = "Video chunks",
				["AudioChunks"] = "Audio chunks",
				["MagicWords"] = "Magic words",
				["SummaryHeader"] = "MVE summary",
				["Type"] = "Type",
				["Count"] = "Count",
				["Done"] = "Done",
				["Step"] = "Step",
				["Output"] = "Output",
				["HelpTitle"] = "MveConverter",
				["HelpUsageHeader"] = "Usage:",
				["HelpCommandsHeader"] = "Commands:",
				["HelpOptionsHeader"] = "Options:",
				["HelpNotesHeader"] = "Notes:",
				["HelpMoreHeader"] = "Help:",
				["HelpExamplesHeader"] = "Examples:",
				["HelpGlobalMenu"] = "  mveconverter",
				["HelpGlobalUsage"] = "  mveconverter <command> [arguments] [options]",
				["HelpGlobalHelp"] = "  mveconverter /? | -? | -h | --help | help",
				["HelpInspectCommand"] = "  inspect    Inspect an MVE file.",
				["HelpToVideoCommand"] = "  to-video   Convert an MVE file to AVI, WAV, or MP4.",
				["HelpToMveCommand"] = "  to-mve     Convert an AVI/MP4/M4V/MOV video to MVE.",
				["HelpCommandHelp"] = "  mveconverter <command> /?     Show command help. Also accepts -?, -h, --help, and help.",
				["InspectHelpTitle"] = "Inspect MVE",
				["ToVideoHelpTitle"] = "Convert MVE to video",
				["ToMveHelpTitle"] = "Convert video to MVE",
				["HelpOptionHelp"] = "  /?, -?, -h, --help, help      Show help.",
				["HelpOverwriteOption"] = "  --no-overwrite                Refuse to replace an existing output file. Overwrite is enabled by default.",
				["HelpFormatOption"] = "  --format|-f <avi8|avi24|wav|mp4> Output format. If omitted, the extension is used.",
				["HelpQualityOption"] = "  --quality|--crf <0-51>        MP4/H.264 quality. 18 by default, lower is better/larger.",
				["HelpToMveTestOption"] = "  --test                       Read and prepare the conversion without writing the final MVE.",
				["HelpToMveAudioOptions"] = "  --no-audio|-ns               Disable audio. --audio-source|-w <avi|wav> uses external PCM audio.",
				["HelpToMveAudioRateOption"] = "  --audio-rate <Hz>            Output audio sample rate. Default: 22050.",
				["HelpToMvePaletteOptions"] = "  --palette|-p <act|pal>       Load a palette. --save-palette <act> writes the palette used.",
				["HelpToMveFrameOptions"] = "  --start <n>                  First frame. --end <n> final frame. --len <n> frame count.",
				["HelpToMveCompressionOptions"] = "  --old | --hq                 Select the legacy or high-quality compression profile.",
				["HelpToMveLimitOptions"] = "  --rate <KiB/s> | --size <MiB> Add an indicative output bitrate or size target.",
				["HelpToMveVideoOptions"] = "  --width <px> --height <px> --fps <value> Override auto-detected video properties. --dither enables Floyd-Steinberg.",
				["HelpAutoFormat"] = "  to-video auto-detects the output format from the extension when --format is omitted.",
				["HelpFfmpeg"] = "  MP4/H.264 requires ffmpeg in PATH: Windows winget install Gyan.FFmpeg; Debian/Ubuntu sudo apt install ffmpeg; macOS brew install ffmpeg.",
				["HelpDetect"] = "  Width, height, and fps are auto-detected when omitted. MP4/M4V/MOV input needs ffmpeg in PATH.",
				["HelpExampleInspect"] = "  mveconverter inspect intro.mve",
				["HelpExampleToVideo"] = "  mveconverter to-video intro.mve intro.avi",
				["HelpExampleToVideoMp4"] = "  mveconverter to-video intro.mve intro.mp4 --format mp4",
				["HelpExampleToMve"] = "  mveconverter to-mve intro.mp4 intro.mve",
				["HelpExampleToMveAdvanced"] = "  mveconverter to-mve intro.avi intro.mve --start 10 --len 120 --dither --hq"
			};

			return new Dictionary<CliLanguage, Dictionary<String, String>>
			{
				[CliLanguage.English] = english,
				[CliLanguage.French] = Merge(english, new Dictionary<String, String>(StringComparer.Ordinal)
				{
					["MainMenuTitle"] = "Que veux-tu faire ?",
					["ToolkitTagline"] = "Outil de conversion MVE Fallout / Interplay",
					["InspectAction"] = "Inspecter un MVE",
					["ToVideoAction"] = "Convertir MVE vers vidéo",
					["ToMveAction"] = "Convertir vidéo vers MVE",
					["LanguageAction"] = "Modifier la langue",
					["QuitAction"] = "Quitter",
					["LanguageTitle"] = "Langue",
					["LanguageChanged"] = "Langue changée en {0}.",
					["InputMvePath"] = "Chemin du fichier MVE",
					["SourceMve"] = "MVE source",
					["TargetVideo"] = "Vidéo cible",
					["SourceVideo"] = "Vidéo source (AVI ou MP4)",
					["TargetMve"] = "MVE cible",
					["ToVideoStatus"] = "Conversion MVE vers vidéo...",
					["ToMveStatus"] = "Conversion vidéo vers MVE...",
					["VideoToMveHeader"] = "Vidéo vers MVE",
					["VideoToMveIntro"] = "Encodeur MVE intégré\nOptions disponibles : source AVI/MP4, mode simulation, palette, audio externe, plage d'images, tramage, profil de compression, débit/taille indicatifs.",
					["AudioKeep"] = "Garder l'audio de la vidéo",
					["AudioNone"] = "Sans audio (-ns)",
					["AudioExternal"] = "Audio externe (-w)",
					["AudioExternalPrompt"] = "Source audio externe AVI/WAV PCM",
					["PaletteVideo"] = "Palette de la vidéo",
					["PaletteLoad"] = "Charger une palette ACT/PAL (-p)",
					["PaletteSave"] = "Sauver la palette ACT",
					["PalettePrompt"] = "Palette ACT/PAL",
					["PaletteSavePrompt"] = "Palette ACT cible",
					["FrameRangeTitle"] = "Plage d'images",
					["FrameAll"] = "Tout convertir",
					["FrameStartEnd"] = "Définir début/fin",
					["FrameStartLen"] = "Définir début/durée",
					["StartFrame"] = "Image de départ (-start)",
					["EndFrame"] = "Image de fin (-end)",
					["FrameCount"] = "Nombre d'images (-len)",
					["CompressionTitle"] = "Profil de compression",
					["CompressionDefault"] = "Défaut",
					["CompressionHighQuality"] = "Haute qualité",
					["CompressionOld"] = "Ancien",
					["LimitTitle"] = "Limite de sortie",
					["LimitNone"] = "Aucune",
					["LimitRate"] = "Débit cible (-rate)",
					["LimitSize"] = "Taille cible (-size)",
					["AudioSampleRatePrompt"] = "Fréquence audio en Hz",
					["QualityPrompt"] = "Qualité MP4/H.264 CRF (0 sans perte, 18 quasi sans perte, 51 plus petit)",
					["OverwriteConfirm"] = "Remplacer le fichier cible s'il existe déjà ?",
					["VideoOverrideConfirm"] = "Remplacer la largeur, la hauteur et les fps détectés ?",
					["WidthPrompt"] = "Largeur en pixels",
					["HeightPrompt"] = "Hauteur en pixels",
					["FpsPrompt"] = "Images par seconde",
					["DitherConfirm"] = "Activer le tramage Floyd-Steinberg (-dither) ?",
					["TestConfirm"] = "Mode test uniquement (-test) ?",
					["Yes"] = "Oui",
					["No"] = "Non",
					["FileNotFound"] = "Fichier introuvable.",
					["InvalidVideoSource"] = "Cette commande attend une vidéo AVI ou MP4 source. Pour convertir un MVE vers une vidéo, choisis le menu MVE vers vidéo.",
					["RequiredPath"] = "Chemin obligatoire.",
					["RangeError"] = "Entre {0} et {1}.",
					["UnknownCommand"] = "Commande inconnue: {0}",
					["UsageInspect"] = "  mveconverter inspect <entrée.mve>",
					["UsageToVideo"] = "  mveconverter to-video <entrée.mve> <sortie> [--format avi8|avi24|wav|mp4]",
					["UsageToMve"] = "  mveconverter to-mve <entrée.avi|mp4|m4v|mov> <sortie.mve> [options]",
					["FormatUnknown"] = "Format de sortie inconnu: {0}. Utilise avi8, avi24, wav ou mp4.",
					["FormatTitle"] = "Format de sortie",
					["AutoFormat"] = "Automatique: {0}",
					["FormatMp4"] = "MP4/H.264 (ffmpeg système)",
					["FormatAvi8"] = "AVI 8 bpp palettisé",
					["FormatWav"] = "WAV audio seul",
					["File"] = "Fichier",
					["Chunks"] = "Blocs",
					["VideoChunks"] = "Chunks vidéo",
					["AudioChunks"] = "Chunks audio",
					["MagicWords"] = "Signature",
					["SummaryHeader"] = "Résumé MVE",
					["Count"] = "Nombre",
					["Done"] = "Terminé",
					["Step"] = "Étape",
					["Output"] = "Sortie",
					["HelpTitle"] = "MveConverter",
					["HelpUsageHeader"] = "Utilisation :",
					["HelpCommandsHeader"] = "Commandes :",
					["HelpOptionsHeader"] = "Options :",
					["HelpNotesHeader"] = "Notes :",
					["HelpMoreHeader"] = "Aide :",
					["HelpExamplesHeader"] = "Exemples :",
					["HelpGlobalMenu"] = "  mveconverter",
					["HelpGlobalUsage"] = "  mveconverter <commande> [arguments] [options]",
					["HelpGlobalHelp"] = "  mveconverter /? | -? | -h | --help | help",
					["HelpInspectCommand"] = "  inspect    Inspecte un fichier MVE.",
					["HelpToVideoCommand"] = "  to-video   Convertit un fichier MVE vers AVI, WAV ou MP4.",
					["HelpToMveCommand"] = "  to-mve     Convertit une vidéo AVI/MP4/M4V/MOV vers MVE.",
					["HelpCommandHelp"] = "  mveconverter <commande> /?     Affiche l'aide de la commande. Accepte aussi -?, -h, --help et help.",
					["InspectHelpTitle"] = "Inspecter un MVE",
					["ToVideoHelpTitle"] = "Convertir MVE vers vidéo",
					["ToMveHelpTitle"] = "Convertir vidéo vers MVE",
					["HelpOptionHelp"] = "  /?, -?, -h, --help, help      Affiche l'aide.",
					["HelpOverwriteOption"] = "  --no-overwrite                Refuse de remplacer un fichier de sortie existant. Le remplacement est activé par défaut.",
					["HelpFormatOption"] = "  --format|-f <avi8|avi24|wav|mp4> Format de sortie. Si absent, l'extension est utilisée.",
					["HelpQualityOption"] = "  --quality|--crf <0-51>        Qualité MP4/H.264. 18 par défaut, plus bas donne un fichier plus grand et meilleur.",
					["HelpToMveTestOption"] = "  --test                       Lit et prépare la conversion sans écrire le MVE final.",
					["HelpToMveAudioOptions"] = "  --no-audio|-ns               Désactive l'audio. --audio-source|-w <avi|wav> utilise un audio PCM externe.",
					["HelpToMveAudioRateOption"] = "  --audio-rate <Hz>            Fréquence audio de sortie. Défaut : 22050.",
					["HelpToMvePaletteOptions"] = "  --palette|-p <act|pal>       Charge une palette. --save-palette <act> écrit la palette utilisée.",
					["HelpToMveFrameOptions"] = "  --start <n>                  Première image. --end <n> dernière image. --len <n> nombre d'images.",
					["HelpToMveCompressionOptions"] = "  --old | --hq                 Sélectionne le profil de compression ancien ou haute qualité.",
					["HelpToMveLimitOptions"] = "  --rate <Kio/s> | --size <Mio> Ajoute une cible indicative de débit ou de taille.",
					["HelpToMveVideoOptions"] = "  --width <px> --height <px> --fps <valeur> Remplace les propriétés détectées. --dither active le tramage Floyd-Steinberg.",
					["HelpAutoFormat"] = "  to-video détecte automatiquement le format de sortie depuis l'extension quand --format est absent.",
					["HelpFfmpeg"] = "  MP4/H.264 nécessite ffmpeg dans PATH : Windows winget install Gyan.FFmpeg ; Debian/Ubuntu sudo apt install ffmpeg ; macOS brew install ffmpeg.",
					["HelpDetect"] = "  Largeur, hauteur et fps sont détectés automatiquement si absents. Les entrées MP4/M4V/MOV nécessitent ffmpeg dans PATH.",
					["HelpExampleInspect"] = "  mveconverter inspect intro.mve",
					["HelpExampleToVideo"] = "  mveconverter to-video intro.mve intro.avi",
					["HelpExampleToVideoMp4"] = "  mveconverter to-video intro.mve intro.mp4 --format mp4",
					["HelpExampleToMve"] = "  mveconverter to-mve intro.mp4 intro.mve",
					["HelpExampleToMveAdvanced"] = "  mveconverter to-mve intro.avi intro.mve --start 10 --len 120 --dither --hq"
				}),
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