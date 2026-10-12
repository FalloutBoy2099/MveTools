namespace MveTools.Cli.Commands
{
	using System;
	using System.Threading.Tasks;
	using MveTools.Conversion;
	using Localization;

	internal static class ToMveCommand
	{
		private static readonly ArgumentParser Parser = new ArgumentParser(
		[
			OptionSpec.Flag("--test"),
			OptionSpec.Flag("--no-audio", "-ns"),
			OptionSpec.Text("--audio-source", "-w"),
			OptionSpec.Integer("--audio-rate", 1, Int32.MaxValue, "--audio-sample-rate"),
			OptionSpec.Text("--palette", "-p"),
			OptionSpec.Text("--save-palette"),
			OptionSpec.Integer("--start", 0, Int32.MaxValue, "-start"),
			OptionSpec.Integer("--end", 0, Int32.MaxValue, "-end"),
			OptionSpec.Integer("--len", 1, Int32.MaxValue, "-len"),
			OptionSpec.Flag("--old", "-old"),
			OptionSpec.Flag("--hq", "-hq"),
			OptionSpec.Integer("--rate", 1, Int32.MaxValue, "-rate"),
			OptionSpec.Number("--size", 0, "-size"),
			OptionSpec.Integer("--width", 1, Int32.MaxValue),
			OptionSpec.Integer("--height", 1, Int32.MaxValue),
			OptionSpec.Number("--fps", 0),
			OptionSpec.Flag("--dither", "-dither"),
			OptionSpec.Flag("--no-overwrite")
		])
			.Conflict("--rate", "--size", "RateSizeExclusive")
			.Conflict("--end", "--len", "EndLenExclusive")
			.Conflict("--old", "--hq", "OldHqExclusive");

		internal static async Task<Int32> RunAsync(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintToMveHelp();
				return 0;
			}

			ParsedArguments parsed = Parser.Parse(args, 1);
			if (!CliArguments.HasPositionals(parsed, 2))
			{
				return ConsoleOutput.Fail(AppText.Get("UsageToMve"));
			}

			String input = parsed.Positionals[0];
			String output = parsed.Positionals[1];
			EnsureVideoSourcePath(input);
			VideoToMveOptions options = ReadOptions(parsed);

			ConversionResult result = await ConversionRunner.RunAsync(
				AppText.Get("ToMveStatus"),
				(progress, cancellationToken) => new MveConverter().ConvertVideoToMveAsync(input, output, options, cancellationToken, progress)).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
			return 0;
		}

		private static VideoToMveOptions ReadOptions(ParsedArguments parsed)
		{
			return new VideoToMveOptions
			{
				Overwrite = !parsed.Has("--no-overwrite"),
				TestOnly = parsed.Has("--test"),
				Width = parsed.GetInteger("--width"),
				Height = parsed.GetInteger("--height"),
				FrameRate = parsed.GetNumber("--fps"),
				AudioSampleRate = parsed.GetInteger("--audio-rate") ?? 22050,
				IncludeAudio = !parsed.Has("--no-audio"),
				AudioSourcePath = parsed.GetText("--audio-source"),
				PalettePath = parsed.GetText("--palette"),
				SavePalettePath = parsed.GetText("--save-palette"),
				UseFloydSteinbergDither = parsed.Has("--dither"),
				StartFrame = parsed.GetInteger("--start"),
				EndFrame = parsed.GetInteger("--end"),
				FrameCount = parsed.GetInteger("--len"),
				CompressionMode = parsed.Has("--old") ? MveCompressionMode.Old : parsed.Has("--hq") ? MveCompressionMode.HighQuality : MveCompressionMode.Default,
				BitRateKilobytesPerSecond = parsed.GetInteger("--rate"),
				SizeLimitMegabytes = parsed.GetNumber("--size")
			};
		}

		private static void EnsureVideoSourcePath(String path)
		{
			if (!MveConverter.IsSupportedVideoSource(path))
			{
				throw new UsageException(AppText.Get("InvalidVideoSource"));
			}
		}
	}
}