namespace MveTools.Cli
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Threading.Tasks;
	using Spectre.Console;
	using MveTools.Conversion;
	using MveTools.Inspection;
	using Localization;

	internal static class InteractiveMenu
	{
		private enum MainMenuAction
		{
			Inspect,
			ToVideo,
			ToMve,
			Language,
			Quit
		}

		private enum AudioMode
		{
			Keep,
			None,
			External
		}

		private enum PaletteMode
		{
			Video,
			Load,
			Save
		}

		private enum FrameRangeMode
		{
			All,
			StartEnd,
			StartLength
		}

		private enum LimitMode
		{
			None,
			Rate,
			Size
		}

		private enum VideoFormatChoice
		{
			Automatic,
			Mp4,
			Avi8,
			Avi24,
			Wav
		}

		internal static async Task<Int32> RunAsync()
		{
			//AnsiConsole.Write(new FigletText("MVE Tools").Color(Color.Cyan1));
			AnsiConsole.MarkupLine($"[grey]{Markup.Escape(AppText.Get("ToolkitTagline"))}[/]");
			AnsiConsole.WriteLine();

			Boolean keepRunning = true;
			while (keepRunning)
			{
				MainMenuAction action = AnsiConsole.Prompt(
					new SelectionPrompt<MainMenuAction>()
						.Title(AppText.Get("MainMenuTitle"))
						.PageSize(8)
						.UseConverter(DescribeMainMenuAction)
						.AddChoices(MainMenuAction.Inspect, MainMenuAction.ToVideo, MainMenuAction.ToMve, MainMenuAction.Language, MainMenuAction.Quit));

				AnsiConsole.WriteLine();
				try
				{
					switch (action)
					{
						case MainMenuAction.Inspect:
							RunInteractiveInspect();
							break;
						case MainMenuAction.ToVideo:
							await RunInteractiveToVideoAsync().ConfigureAwait(false);
							break;
						case MainMenuAction.ToMve:
							await RunInteractiveToMveAsync().ConfigureAwait(false);
							break;
						case MainMenuAction.Language:
							RunInteractiveLanguage();
							break;
						default:
							keepRunning = false;
							break;
					}
				}
				catch (Exception ex)
				{
					// A failed or cancelled action goes back to the menu instead of ending the session.
					ConsoleOutput.WriteError(AppText.DescribeError(ex));
				}

				if (keepRunning)
				{
					AnsiConsole.WriteLine();
				}
			}

			return 0;
		}

		private static void RunInteractiveInspect()
		{
			String inputPath = PromptExistingPath(AppText.Get("InputMvePath"));
			MveFileInfo info = new MveFileInspector().Inspect(inputPath);
			ConsoleOutput.WriteInspection(info);
		}

		private static async Task RunInteractiveToVideoAsync()
		{
			String inputPath = PromptExistingPath(AppText.Get("SourceMve"));
			String defaultOutput = Path.ChangeExtension(inputPath, ".avi") ?? "output.avi";
			String outputPath = PromptOutputPath(AppText.Get("TargetVideo"), defaultOutput);
			VideoStandard standard = PromptVideoStandard(outputPath);
			Int32 quality = standard == VideoStandard.Mp4H264
				? PromptInt(AppText.Get("QualityPrompt"), 18, 0, 51)
				: 18;

			MveToVideoOptions options = new()
			{
				Standard = standard,
				Quality = quality,
				Overwrite = PromptBoolean(AppText.Get("OverwriteConfirm"), true)
			};

			ConversionResult result = await ConversionRunner.RunAsync(
				AppText.Get("ToVideoStatus"),
				(progress, cancellationToken) => new MveConverter().ConvertMveToVideoAsync(inputPath, outputPath, options, cancellationToken, progress)).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
		}

		private static async Task RunInteractiveToMveAsync()
		{
			AnsiConsole.Write(new Panel(new Markup(
				$"[cyan]{Markup.Escape(AppText.Get("VideoToMveIntro"))}[/]"))
				.Header(AppText.Get("VideoToMveHeader"))
				.BorderColor(Color.Cyan1));

			String inputPath = PromptExistingVideoPath(AppText.Get("SourceVideo"));
			String defaultOutput = Path.ChangeExtension(inputPath, ".mve") ?? "output.mve";
			String outputPath = PromptOutputPath(AppText.Get("TargetMve"), defaultOutput);
			Boolean overwrite = PromptBoolean(AppText.Get("OverwriteConfirm"), true);
			AudioMode audioMode = AnsiConsole.Prompt(
				new SelectionPrompt<AudioMode>()
					.Title(AppText.Get("AudioTitle"))
					.UseConverter(DescribeAudioMode)
					.AddChoices(AudioMode.Keep, AudioMode.None, AudioMode.External));

			String? audioSourcePath = audioMode == AudioMode.External
				? PromptExistingPath(AppText.Get("AudioExternalPrompt"))
				: null;
			Int32 audioSampleRate = audioMode == AudioMode.None
				? 22050
				: PromptInt(AppText.Get("AudioSampleRatePrompt"), 22050, 1, Int32.MaxValue);

			PaletteMode paletteMode = AnsiConsole.Prompt(
				new SelectionPrompt<PaletteMode>()
					.Title(AppText.Get("PaletteTitle"))
					.UseConverter(DescribePaletteMode)
					.AddChoices(PaletteMode.Video, PaletteMode.Load, PaletteMode.Save));

			String? palettePath = paletteMode == PaletteMode.Load
				? PromptExistingPath(AppText.Get("PalettePrompt"))
				: null;
			String? savePalettePath = paletteMode == PaletteMode.Save
				? PromptOutputPath(AppText.Get("PaletteSavePrompt"), Path.ChangeExtension(outputPath, ".act") ?? "palette.act")
				: null;

			FrameRangeMode frameRangeMode = AnsiConsole.Prompt(
				new SelectionPrompt<FrameRangeMode>()
					.Title(AppText.Get("FrameRangeTitle"))
					.UseConverter(DescribeFrameRangeMode)
					.AddChoices(FrameRangeMode.All, FrameRangeMode.StartEnd, FrameRangeMode.StartLength));

			Int32? startFrame = null;
			Int32? endFrame = null;
			Int32? frameCount = null;
			if (frameRangeMode != FrameRangeMode.All)
			{
				startFrame = PromptInt(AppText.Get("StartFrame"), 0, 0, Int32.MaxValue);
				if (frameRangeMode == FrameRangeMode.StartEnd)
				{
					endFrame = PromptInt(AppText.Get("EndFrame"), startFrame.Value, startFrame.Value, Int32.MaxValue);
				}
				else
				{
					frameCount = PromptInt(AppText.Get("FrameCount"), 1, 1, Int32.MaxValue);
				}
			}

			Boolean overrideVideoProperties = PromptBoolean(AppText.Get("VideoOverrideConfirm"), false);
			Int32? width = null;
			Int32? height = null;
			Double? frameRate = null;
			if (overrideVideoProperties)
			{
				width = PromptInt(AppText.Get("WidthPrompt"), 640, 1, Int32.MaxValue);
				height = PromptInt(AppText.Get("HeightPrompt"), 480, 1, Int32.MaxValue);
				frameRate = PromptDouble(AppText.Get("FpsPrompt"), 15, 0.001d, Double.MaxValue);
			}

			MveCompressionMode compressionMode = AnsiConsole.Prompt(
				new SelectionPrompt<MveCompressionMode>()
					.Title(AppText.Get("CompressionTitle"))
					.UseConverter(AppText.DescribeCompressionMode)
					.AddChoices(MveCompressionMode.Default, MveCompressionMode.HighQuality, MveCompressionMode.Old));

			LimitMode limitMode = AnsiConsole.Prompt(
				new SelectionPrompt<LimitMode>()
					.Title(AppText.Get("LimitTitle"))
					.UseConverter(DescribeLimitMode)
					.AddChoices(LimitMode.None, LimitMode.Rate, LimitMode.Size));

			VideoToMveOptions options = new()
			{
				IncludeAudio = audioMode != AudioMode.None,
				Width = width,
				Height = height,
				FrameRate = frameRate,
				AudioSourcePath = audioSourcePath,
				PalettePath = palettePath,
				SavePalettePath = savePalettePath,
				StartFrame = startFrame,
				EndFrame = endFrame,
				FrameCount = frameCount,
				CompressionMode = compressionMode,
				BitRateKilobytesPerSecond = limitMode == LimitMode.Rate ? PromptInt(AppText.Get("RatePrompt"), 300, 1, Int32.MaxValue) : null,
				SizeLimitMegabytes = limitMode == LimitMode.Size ? PromptDouble(AppText.Get("SizePrompt"), 20, 0.001d, Double.MaxValue) : null,
				UseFloydSteinbergDither = PromptBoolean(AppText.Get("DitherConfirm"), false),
				TestOnly = PromptBoolean(AppText.Get("TestConfirm"), false),
				AudioSampleRate = audioSampleRate,
				Overwrite = overwrite
			};

			ConversionResult result = await ConversionRunner.RunAsync(
				AppText.Get("ToMveStatus"),
				(progress, cancellationToken) => new MveConverter().ConvertVideoToMveAsync(inputPath, outputPath, options, cancellationToken, progress)).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
		}

		private static void RunInteractiveLanguage()
		{
			LanguageOption language = AnsiConsole.Prompt(
				new SelectionPrompt<LanguageOption>()
					.Title(AppText.Get("LanguageTitle"))
					.PageSize(12)
					.UseConverter(option => option.DisplayName)
					.AddChoices(AppText.Languages));
			AppText.SetLanguage(language.Language);
			AnsiConsole.MarkupLine($"[green]{Markup.Escape(AppText.Format("LanguageChanged", language.DisplayName))}[/]");
		}

		private static String PromptExistingPath(String label)
		{
			return AnsiConsole.Prompt(
				new TextPrompt<String>($"{label}:")
					.Validate(value => File.Exists(value)
						? ValidationResult.Success()
						: ValidationResult.Error(AppText.Get("FileNotFound"))));
		}

		private static String PromptExistingVideoPath(String label)
		{
			return AnsiConsole.Prompt(
				new TextPrompt<String>($"{label}:")
					.Validate(value =>
					{
						if (!File.Exists(value))
						{
							return ValidationResult.Error(AppText.Get("FileNotFound"));
						}

						return MveConverter.IsSupportedVideoSource(value)
							? ValidationResult.Success()
							: ValidationResult.Error(AppText.Get("InvalidVideoSource"));
					}));
		}

		private static String PromptOutputPath(String label, String defaultValue)
		{
			return AnsiConsole.Prompt(
				new TextPrompt<String>($"{label}:")
					.DefaultValue(defaultValue)
					.Validate(value => String.IsNullOrWhiteSpace(value)
						? ValidationResult.Error(AppText.Get("RequiredPath"))
						: ValidationResult.Success()));
		}

		private static Int32 PromptInt(String label, Int32 defaultValue, Int32 min, Int32 max)
		{
			return AnsiConsole.Prompt(
				new TextPrompt<Int32>($"{label}:")
					.DefaultValue(defaultValue)
					.Validate(value => value >= min && value <= max
						? ValidationResult.Success()
						: ValidationResult.Error(AppText.Format("RangeError", min, max))));
		}

		private static Double PromptDouble(String label, Double defaultValue, Double min, Double max)
		{
			return AnsiConsole.Prompt(
				new TextPrompt<Double>($"{label}:")
					.DefaultValue(defaultValue)
					.Validate(value => value >= min && value <= max
					? ValidationResult.Success()
					: ValidationResult.Error(AppText.Format("RangeError", min.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture)))));
		}

		private static Boolean PromptBoolean(String label, Boolean defaultValue)
		{
			Boolean choice = AnsiConsole.Prompt(
				new SelectionPrompt<Boolean>()
					.Title(label)
					.UseConverter(value => value ? AppText.Get("Yes") : AppText.Get("No"))
					.AddChoices(defaultValue, !defaultValue));
			return choice;
		}

		private static VideoStandard PromptVideoStandard(String outputPath)
		{
			VideoStandard automatic = ReadInteractiveVideoStandard(outputPath);
			VideoFormatChoice choice = AnsiConsole.Prompt(
				new SelectionPrompt<VideoFormatChoice>()
					.Title(AppText.Get("FormatTitle"))
					.UseConverter(format => DescribeVideoFormatChoice(format, automatic))
					.AddChoices(
						VideoFormatChoice.Automatic,
						VideoFormatChoice.Mp4,
						VideoFormatChoice.Avi8,
						VideoFormatChoice.Avi24,
						VideoFormatChoice.Wav));

			return choice switch
			{
				VideoFormatChoice.Mp4 => VideoStandard.Mp4H264,
				VideoFormatChoice.Avi8 => VideoStandard.AviIndexed8,
				VideoFormatChoice.Avi24 => VideoStandard.AviRgb24,
				VideoFormatChoice.Wav => VideoStandard.WavPcm,
				_ => automatic
			};
		}

		private static VideoStandard ReadInteractiveVideoStandard(String outputPath)
		{
			String extension = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
			return extension switch
			{
				"avi" => VideoStandard.AviIndexed8,
				"mp4" or "m4v" => VideoStandard.Mp4H264,
				"wav" => VideoStandard.WavPcm,
				_ => VideoStandard.AviIndexed8
			};
		}

		private static String DescribeMainMenuAction(MainMenuAction action)
		{
			return action switch
			{
				MainMenuAction.Inspect => AppText.Get("InspectAction"),
				MainMenuAction.ToVideo => AppText.Get("ToVideoAction"),
				MainMenuAction.ToMve => AppText.Get("ToMveAction"),
				MainMenuAction.Language => AppText.Get("LanguageAction"),
				MainMenuAction.Quit => AppText.Get("QuitAction"),
				_ => action.ToString()
			};
		}

		private static String DescribeAudioMode(AudioMode mode)
		{
			return mode switch
			{
				AudioMode.None => AppText.Get("AudioNone"),
				AudioMode.External => AppText.Get("AudioExternal"),
				_ => AppText.Get("AudioKeep")
			};
		}

		private static String DescribePaletteMode(PaletteMode mode)
		{
			return mode switch
			{
				PaletteMode.Load => AppText.Get("PaletteLoad"),
				PaletteMode.Save => AppText.Get("PaletteSave"),
				_ => AppText.Get("PaletteVideo")
			};
		}

		private static String DescribeFrameRangeMode(FrameRangeMode mode)
		{
			return mode switch
			{
				FrameRangeMode.StartEnd => AppText.Get("FrameStartEnd"),
				FrameRangeMode.StartLength => AppText.Get("FrameStartLen"),
				_ => AppText.Get("FrameAll")
			};
		}

		private static String DescribeLimitMode(LimitMode mode)
		{
			return mode switch
			{
				LimitMode.Rate => AppText.Get("LimitRate"),
				LimitMode.Size => AppText.Get("LimitSize"),
				_ => AppText.Get("LimitNone")
			};
		}

		private static String DescribeVideoFormatChoice(VideoFormatChoice choice, VideoStandard automatic)
		{
			return choice switch
			{
				VideoFormatChoice.Mp4 => AppText.DescribeVideoStandard(VideoStandard.Mp4H264),
				VideoFormatChoice.Avi8 => AppText.DescribeVideoStandard(VideoStandard.AviIndexed8),
				VideoFormatChoice.Avi24 => AppText.DescribeVideoStandard(VideoStandard.AviRgb24),
				VideoFormatChoice.Wav => AppText.DescribeVideoStandard(VideoStandard.WavPcm),
				_ => AppText.Format("AutoFormat", AppText.DescribeVideoStandard(automatic))
			};
		}
	}
}