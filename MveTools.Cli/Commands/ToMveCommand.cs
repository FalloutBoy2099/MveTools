namespace MveTools.Cli
{
	using System;
	using System.Threading.Tasks;
	using MveTools.Conversion;

	internal static class ToMveCommand
	{
		internal static async Task<Int32> RunAsync(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintToMveHelp();
				return 0;
			}

			if (args.Length < 3)
			{
				return ConsoleOutput.Fail(AppText.Get("UsageToMve"));
			}

			VideoToMveOptions options = ReadVideoToMveOptions(args);
			EnsureVideoSourcePath(args[1]);

			ConversionResult result = await new MveConverter().ConvertVideoToMveAsync(args[1], args[2], options).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
			return 0;
		}

		private static VideoToMveOptions ReadVideoToMveOptions(String[] args)
		{
			if (CliArguments.HasFlag(args, "--rate") && CliArguments.HasFlag(args, "--size"))
			{
				throw new ArgumentException("--rate and --size are mutually exclusive.");
			}

			if (CliArguments.HasFlag(args, "--end") && CliArguments.HasFlag(args, "--len"))
			{
				throw new ArgumentException("--end and --len are mutually exclusive.");
			}

			return new VideoToMveOptions
			{
				Overwrite = CliArguments.ReadOverwrite(args),
				TestOnly = CliArguments.HasFlag(args, "--test"),
				Width = CliArguments.ReadOptionalInt(args, "--width"),
				Height = CliArguments.ReadOptionalInt(args, "--height"),
				FrameRate = CliArguments.ReadOptionalDouble(args, "--fps"),
				AudioSampleRate = CliArguments.ReadOptionalInt(args, "--audio-rate") ?? CliArguments.ReadOptionalInt(args, "--audio-sample-rate") ?? 22050,
				IncludeAudio = !CliArguments.HasFlag(args, "--no-audio") && !CliArguments.HasFlag(args, "-ns"),
				AudioSourcePath = CliArguments.ReadOptionalString(args, "--audio-source") ?? CliArguments.ReadOptionalString(args, "-w"),
				PalettePath = CliArguments.ReadOptionalString(args, "--palette") ?? CliArguments.ReadOptionalString(args, "-p"),
				SavePalettePath = CliArguments.ReadOptionalString(args, "--save-palette"),
				UseFloydSteinbergDither = CliArguments.HasFlag(args, "--dither") || CliArguments.HasFlag(args, "-dither"),
				StartFrame = CliArguments.ReadOptionalInt(args, "--start") ?? CliArguments.ReadOptionalInt(args, "-start"),
				EndFrame = CliArguments.ReadOptionalInt(args, "--end") ?? CliArguments.ReadOptionalInt(args, "-end"),
				FrameCount = CliArguments.ReadOptionalInt(args, "--len") ?? CliArguments.ReadOptionalInt(args, "-len"),
				CompressionMode = ReadCompressionMode(args),
				BitRateKilobytesPerSecond = CliArguments.ReadOptionalInt(args, "--rate") ?? CliArguments.ReadOptionalInt(args, "-rate"),
				SizeLimitMegabytes = CliArguments.ReadOptionalDouble(args, "--size") ?? CliArguments.ReadOptionalDouble(args, "-size")
			};
		}

		private static MveCompressionMode ReadCompressionMode(String[] args)
		{
			if (CliArguments.HasFlag(args, "--old") || CliArguments.HasFlag(args, "-old"))
			{
				return MveCompressionMode.Old;
			}

			if (CliArguments.HasFlag(args, "--hq") || CliArguments.HasFlag(args, "-hq"))
			{
				return MveCompressionMode.HighQuality;
			}

			return MveCompressionMode.Default;
		}

		private static void EnsureVideoSourcePath(String path)
		{
			if (!MveConverter.IsSupportedVideoSource(path))
			{
				throw new NotSupportedException(AppText.Get("InvalidVideoSource"));
			}
		}
	}
}
