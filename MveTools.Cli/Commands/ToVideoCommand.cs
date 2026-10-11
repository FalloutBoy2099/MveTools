namespace MveTools.Cli.Commands
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using MveTools.Conversion;
	using Localization;

	internal static class ToVideoCommand
	{
		internal static async Task<Int32> RunAsync(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintToVideoHelp();
				return 0;
			}

			if (args.Length < 3)
			{
				return ConsoleOutput.Fail(AppText.Get("UsageToVideo"));
			}

			MveToVideoOptions options = ReadMveToVideoOptions(args, args[2]);
			ConversionResult result = await new MveConverter().ConvertMveToVideoAsync(args[1], args[2], options).ConfigureAwait(false);
			ConsoleOutput.WriteConversionResult(result);
			return 0;
		}

		private static MveToVideoOptions ReadMveToVideoOptions(String[] args, String outputPath)
		{
			return new MveToVideoOptions
			{
				Standard = ReadVideoStandard(args, outputPath),
				Quality = CliArguments.ReadOptionalInt(args, "--quality") ?? CliArguments.ReadOptionalInt(args, "--crf") ?? 18,
				Overwrite = CliArguments.ReadOverwrite(args)
			};
		}

		internal static VideoStandard ReadVideoStandard(String[] args, String outputPath)
		{
			String? value = CliArguments.ReadOptionalString(args, "--format") ?? CliArguments.ReadOptionalString(args, "-f");
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
	}
}