namespace MveTools.Cli.Commands
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using MveTools.Conversion;
	using Localization;

	internal static class ToVideoCommand
	{
		private static readonly ArgumentParser Parser = new(
		[
			OptionSpec.Text("--format", "-f"),
			OptionSpec.Integer("--quality", 0, 51, "--crf"),
			OptionSpec.Flag("--no-overwrite")
		]);

		internal static async Task<Int32> RunAsync(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintToVideoHelp();
				return 0;
			}

			ParsedArguments parsed = Parser.Parse(args, 1);
			if (!CliArguments.HasPositionals(parsed, 2))
			{
				return ConsoleOutput.Fail(AppText.Get("UsageToVideo"));
			}

			String input = parsed.Positionals[0];
			String output = parsed.Positionals[1];
			MveToVideoOptions options = new()
			{
				Standard = ReadVideoStandard(parsed.GetText("--format"), output),
				Quality = parsed.GetInteger("--quality") ?? 18,
				Overwrite = !parsed.Has("--no-overwrite")
			};

			ConversionResult result = await ConversionRunner.RunAsync(
				AppText.Get("ToVideoStatus"),
				(progress, cancellationToken) => new MveConverter().ConvertMveToVideoAsync(input, output, options, cancellationToken, progress)).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
			return 0;
		}

		internal static VideoStandard ReadVideoStandard(String? format, String outputPath)
		{
			String value = String.IsNullOrWhiteSpace(format) ? Path.GetExtension(outputPath).TrimStart('.') : format;
			return value.ToLowerInvariant() switch
			{
				"avi" or "avi8" or "indexed8" => VideoStandard.AviIndexed8,
				"avi24" or "rgb24" => VideoStandard.AviRgb24,
				"wav" or "audio" => VideoStandard.WavPcm,
				"mp4" or "h264" => VideoStandard.Mp4H264,
				_ => throw new UsageException(AppText.Format("FormatUnknown", value))
			};
		}
	}
}