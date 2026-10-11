namespace MveTools.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using Spectre.Console;
	using MveTools.Conversion;
	using MveTools.Inspection;

	internal static class Program
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

		public static async Task<Int32> Main(String[] args)
		{
			ConfigureConsoleEncoding();
			AppText.SetLanguage(AppText.Current);
			try
			{
				if (args.Length == 0)
				{
					if (IsInteractive())
					{
						return await RunMenuAsync().ConfigureAwait(false);
					}

					PrintHelp();
					return 0;
				}

				if (IsHelpArgument(args[0]))
				{
					PrintHelp();
					return 0;
				}

				return args[0].ToLowerInvariant() switch
				{
					"inspect" => Inspect(args),
					"to-video" => await ConvertToVideoAsync(args).ConfigureAwait(false),
					"to-mve" => await ConvertToMveAsync(args).ConfigureAwait(false),
					_ => Fail(AppText.Format("UnknownCommand", args[0]))
				};
			}
			catch (Exception ex)
			{
				WriteError(ex.Message);
				return 1;
			}
		}

		private static async Task<Int32> RunMenuAsync()
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
			WriteInspection(info);
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

			ConversionResult result = await RunWithStatusAsync(
				AppText.Get("ToVideoStatus"),
				() => new MveConverter().ConvertMveToVideoAsync(inputPath, outputPath, options)).ConfigureAwait(false);
			WriteConversionResult(result);
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

			ConversionResult result = await RunWithStatusAsync(
				AppText.Get("ToMveStatus"),
				() => new MveConverter().ConvertVideoToMveAsync(inputPath, outputPath, options)).ConfigureAwait(false);
			WriteConversionResult(result);
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

		private static Int32 Inspect(String[] args)
		{
			if (ContainsHelpArgument(args, 1))
			{
				PrintInspectHelp();
				return 0;
			}

			if (args.Length < 2)
			{
				return Fail(AppText.Get("UsageInspect"));
			}

			MveFileInfo info = new MveFileInspector().Inspect(args[1]);
			WriteInspection(info);
			return 0;
		}

		private static async Task<Int32> ConvertToVideoAsync(String[] args)
		{
			if (ContainsHelpArgument(args, 1))
			{
				PrintToVideoHelp();
				return 0;
			}

			if (args.Length < 3)
			{
				return Fail(AppText.Get("UsageToVideo"));
			}

			MveToVideoOptions options = ReadMveToVideoOptions(args, args[2]);
			ConversionResult result = await new MveConverter().ConvertMveToVideoAsync(args[1], args[2], options).ConfigureAwait(false);
			WriteConversionResult(result);
			return 0;
		}

		private static async Task<Int32> ConvertToMveAsync(String[] args)
		{
			if (ContainsHelpArgument(args, 1))
			{
				PrintToMveHelp();
				return 0;
			}

			if (args.Length < 3)
			{
				return Fail(AppText.Get("UsageToMve"));
			}

			VideoToMveOptions options = ReadVideoToMveOptions(args);
			EnsureVideoSourcePath(args[1]);

			ConversionResult result = await new MveConverter().ConvertVideoToMveAsync(args[1], args[2], options).ConfigureAwait(false);
			WriteConversionResult(result);
			return 0;
		}

		private static Task<ConversionResult> RunWithStatusAsync(String label, Func<Task<ConversionResult>> action)
		{
			return AnsiConsole.Status()
				.Spinner(Spinner.Known.Dots)
				.SpinnerStyle(Style.Parse("cyan"))
				.StartAsync(label, _ => action());
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

						return IsSupportedVideoSource(value)
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

		private static Int32? ReadOptionalInt(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0 && index + 1 < args.Length && Int32.TryParse(args[index + 1], out Int32 value) ? value : null;
		}

		private static Double? ReadOptionalDouble(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0
				&& index + 1 < args.Length
				&& Double.TryParse(args[index + 1], CultureInfo.InvariantCulture, out Double value)
					? value
					: null;
		}

		private static VideoToMveOptions ReadVideoToMveOptions(String[] args)
		{
			if (HasFlag(args, "--rate") && HasFlag(args, "--size"))
			{
				throw new ArgumentException("--rate and --size are mutually exclusive.");
			}

			if (HasFlag(args, "--end") && HasFlag(args, "--len"))
			{
				throw new ArgumentException("--end and --len are mutually exclusive.");
			}

			return new VideoToMveOptions
			{
				Overwrite = ReadOverwrite(args),
				TestOnly = HasFlag(args, "--test"),
				Width = ReadOptionalInt(args, "--width"),
				Height = ReadOptionalInt(args, "--height"),
				FrameRate = ReadOptionalDouble(args, "--fps"),
				AudioSampleRate = ReadOptionalInt(args, "--audio-rate") ?? ReadOptionalInt(args, "--audio-sample-rate") ?? 22050,
				IncludeAudio = !HasFlag(args, "--no-audio") && !HasFlag(args, "-ns"),
				AudioSourcePath = ReadOptionalString(args, "--audio-source") ?? ReadOptionalString(args, "-w"),
				PalettePath = ReadOptionalString(args, "--palette") ?? ReadOptionalString(args, "-p"),
				SavePalettePath = ReadOptionalString(args, "--save-palette"),
				UseFloydSteinbergDither = HasFlag(args, "--dither") || HasFlag(args, "-dither"),
				StartFrame = ReadOptionalInt(args, "--start") ?? ReadOptionalInt(args, "-start"),
				EndFrame = ReadOptionalInt(args, "--end") ?? ReadOptionalInt(args, "-end"),
				FrameCount = ReadOptionalInt(args, "--len") ?? ReadOptionalInt(args, "-len"),
				CompressionMode = ReadCompressionMode(args),
				BitRateKilobytesPerSecond = ReadOptionalInt(args, "--rate") ?? ReadOptionalInt(args, "-rate"),
				SizeLimitMegabytes = ReadOptionalDouble(args, "--size") ?? ReadOptionalDouble(args, "-size")
			};
		}

		private static MveToVideoOptions ReadMveToVideoOptions(String[] args, String outputPath)
		{
			return new MveToVideoOptions
			{
				Standard = ReadVideoStandard(args, outputPath),
				Quality = ReadOptionalInt(args, "--quality") ?? ReadOptionalInt(args, "--crf") ?? 18,
				Overwrite = ReadOverwrite(args)
			};
		}

		private static void EnsureVideoSourcePath(String path)
		{
			if (!IsSupportedVideoSource(path))
			{
				throw new NotSupportedException(AppText.Get("InvalidVideoSource"));
			}
		}

		private static Boolean IsSupportedVideoSource(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			return extension is ".avi" or ".mp4" or ".m4v" or ".mov";
		}

		private static MveCompressionMode ReadCompressionMode(String[] args)
		{
			if (HasFlag(args, "--old") || HasFlag(args, "-old"))
			{
				return MveCompressionMode.Old;
			}

			if (HasFlag(args, "--hq") || HasFlag(args, "-hq"))
			{
				return MveCompressionMode.HighQuality;
			}

			return MveCompressionMode.Default;
		}

		private static Boolean HasFlag(String[] args, String name)
		{
			return args.Any(arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
		}

		private static Boolean ReadOverwrite(String[] args)
		{
			if (HasFlag(args, "--no-overwrite"))
			{
				return false;
			}

			return true;
		}

		private static VideoStandard ReadVideoStandard(String[] args, String outputPath)
		{
			String? value = ReadOptionalString(args, "--format") ?? ReadOptionalString(args, "-f");
			if (String.IsNullOrWhiteSpace(value))
			{
				value = Path.GetExtension(outputPath).TrimStart('.');
			}

			return value.ToLowerInvariant() switch
			{
				"avi" or "avi8" or "indexed8" => VideoStandard.AviIndexed8,
				"avi24" or "rgb24" => VideoStandard.AviRgb24,
				"wav" or "audio" => VideoStandard.WavPcm,
				"mp4" or "h264" => VideoStandard.Mp4H264,
				_ => throw new NotSupportedException(AppText.Format("FormatUnknown", value))
			};
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

		private static String? ReadOptionalString(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
		}

		private static void WriteInspection(MveFileInfo info)
		{
			if (!IsInteractive())
			{
				Console.WriteLine($"{AppText.Get("File")}: {info.Path}");
				Console.WriteLine($"{AppText.Get("Chunks")}: {info.Chunks.Count}");
				Console.WriteLine($"{AppText.Get("VideoChunks")}: {info.VideoChunkCount}");
				Console.WriteLine($"{AppText.Get("AudioChunks")}: {info.AudioChunkCount}");
				Console.WriteLine($"{AppText.Get("MagicWords")}: {Convert.ToHexString(info.MagicWords)}");
				foreach (IGrouping<String, MveChunkInfo> group in ChunkGroups(info))
				{
					Console.WriteLine($"{group.Key}: {group.Count()}");
				}

				return;
			}

			Grid grid = new();
			grid.AddColumn();
			grid.AddColumn();
			grid.AddRow(AppText.Get("File"), Markup.Escape(info.Path));
			grid.AddRow(AppText.Get("Chunks"), info.Chunks.Count.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("VideoChunks"), info.VideoChunkCount.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("AudioChunks"), info.AudioChunkCount.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("MagicWords"), Convert.ToHexString(info.MagicWords));
			AnsiConsole.Write(new Panel(grid).Header(AppText.Get("SummaryHeader")).BorderColor(Color.Cyan1));

			Table table = new Table().Border(TableBorder.Rounded);
			table.AddColumn(AppText.Get("Type"));
			table.AddColumn(new TableColumn(AppText.Get("Count")).RightAligned());
			foreach (IGrouping<String, MveChunkInfo> group in ChunkGroups(info))
			{
				table.AddRow(Markup.Escape(group.Key), group.Count().ToString(CultureInfo.InvariantCulture));
			}

			AnsiConsole.Write(table);
		}

		private static IEnumerable<IGrouping<String, MveChunkInfo>> ChunkGroups(MveFileInfo info)
		{
			return info.Chunks
				.GroupBy(chunk => chunk.KnownType?.ToString() ?? $"Unknown 0x{chunk.Type:X4}")
				.OrderBy(group => group.Key);
		}

		private static void WriteConversionResult(ConversionResult result)
		{
			if (!IsInteractive())
			{
				Console.WriteLine($"{AppText.Get("Output")}: {result.OutputPath}");
				foreach (String step in result.Steps)
				{
					Console.WriteLine(AppText.LocalizeStep(step));
				}

				return;
			}

			AnsiConsole.MarkupLine($"[green]{Markup.Escape(AppText.Get("Done"))}[/] [grey]{Markup.Escape(result.OutputPath)}[/]");

			Table table = new Table().Border(TableBorder.Rounded);
			table.AddColumn(AppText.Get("Step"));
			foreach (String step in result.Steps)
			{
				table.AddRow(Markup.Escape(AppText.LocalizeStep(step)));
			}

			AnsiConsole.Write(table);
		}

		private static Int32 Fail(String message)
		{
			WriteError(message);
			return 1;
		}

		private static void WriteError(String message)
		{
			if (IsInteractive())
			{
				AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
			}
			else
			{
				Console.Error.WriteLine(message);
			}
		}

		private static void PrintHelp()
		{
			WriteHelpLine("HelpTitle");
			Console.WriteLine();
			WriteHelpLine("HelpUsageHeader");
			WriteHelpLine("HelpGlobalMenu");
			WriteHelpLine("HelpGlobalUsage");
			WriteHelpLine("HelpGlobalHelp");
			Console.WriteLine();
			WriteHelpLine("HelpCommandsHeader");
			WriteHelpLine("HelpInspectCommand");
			WriteHelpLine("HelpToVideoCommand");
			WriteHelpLine("HelpToMveCommand");
			Console.WriteLine();
			WriteHelpLine("HelpMoreHeader");
			WriteHelpLine("HelpCommandHelp");
			WriteHelpLine("HelpFfmpeg");
			Console.WriteLine();
			WriteHelpLine("HelpExamplesHeader");
			WriteHelpLine("HelpExampleInspect");
			WriteHelpLine("HelpExampleToVideo");
			WriteHelpLine("HelpExampleToMve");
		}

		private static void PrintInspectHelp()
		{
			WriteHelpLine("InspectHelpTitle");
			Console.WriteLine();
			WriteHelpLine("HelpUsageHeader");
			WriteHelpLine("UsageInspect");
			Console.WriteLine();
			WriteHelpLine("HelpOptionsHeader");
			WriteHelpLine("HelpOptionHelp");
			Console.WriteLine();
			WriteHelpLine("HelpExamplesHeader");
			WriteHelpLine("HelpExampleInspect");
		}

		private static void PrintToVideoHelp()
		{
			WriteHelpLine("ToVideoHelpTitle");
			Console.WriteLine();
			WriteHelpLine("HelpUsageHeader");
			WriteHelpLine("UsageToVideo");
			Console.WriteLine();
			WriteHelpLine("HelpOptionsHeader");
			WriteHelpLine("HelpFormatOption");
			WriteHelpLine("HelpQualityOption");
			WriteHelpLine("HelpOverwriteOption");
			WriteHelpLine("HelpOptionHelp");
			Console.WriteLine();
			WriteHelpLine("HelpNotesHeader");
			WriteHelpLine("HelpAutoFormat");
			WriteHelpLine("HelpFfmpeg");
			Console.WriteLine();
			WriteHelpLine("HelpExamplesHeader");
			WriteHelpLine("HelpExampleToVideo");
			WriteHelpLine("HelpExampleToVideoMp4");
		}

		private static void PrintToMveHelp()
		{
			WriteHelpLine("ToMveHelpTitle");
			Console.WriteLine();
			WriteHelpLine("HelpUsageHeader");
			WriteHelpLine("UsageToMve");
			Console.WriteLine();
			WriteHelpLine("HelpOptionsHeader");
			WriteHelpLine("HelpToMveTestOption");
			WriteHelpLine("HelpToMveAudioOptions");
			WriteHelpLine("HelpToMveAudioRateOption");
			WriteHelpLine("HelpToMvePaletteOptions");
			WriteHelpLine("HelpToMveFrameOptions");
			WriteHelpLine("HelpToMveCompressionOptions");
			WriteHelpLine("HelpToMveLimitOptions");
			WriteHelpLine("HelpToMveVideoOptions");
			WriteHelpLine("HelpOverwriteOption");
			WriteHelpLine("HelpOptionHelp");
			Console.WriteLine();
			WriteHelpLine("HelpNotesHeader");
			WriteHelpLine("HelpDetect");
			WriteHelpLine("HelpFfmpeg");
			Console.WriteLine();
			WriteHelpLine("HelpExamplesHeader");
			WriteHelpLine("HelpExampleToMve");
			WriteHelpLine("HelpExampleToMveAdvanced");
		}

		private static void WriteHelpLine(String key)
		{
			Console.WriteLine(AppText.Get(key));
		}

		private static Boolean ContainsHelpArgument(String[] args, Int32 startIndex)
		{
			for (Int32 index = startIndex; index < args.Length; index++)
			{
				if (IsHelpArgument(args[index]))
				{
					return true;
				}
			}

			return false;
		}

		private static Boolean IsHelpArgument(String value)
		{
			return String.Equals(value, "/?", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "-?", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
		}

		private static Boolean IsInteractive()
		{
			return !Console.IsInputRedirected && !Console.IsOutputRedirected;
		}

		private static void ConfigureConsoleEncoding()
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					SetConsoleCP(65001);
					SetConsoleOutputCP(65001);
				}

				Console.InputEncoding = Encoding.UTF8;
				Console.OutputEncoding = Encoding.UTF8;
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean SetConsoleCP(UInt32 codePageId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean SetConsoleOutputCP(UInt32 codePageId);

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