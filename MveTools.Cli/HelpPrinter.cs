namespace MveTools.Cli
{
	using System;

	internal static class HelpPrinter
	{
		internal static void PrintHelp()
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

		internal static void PrintInspectHelp()
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

		internal static void PrintToVideoHelp()
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

		internal static void PrintToMveHelp()
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
	}
}
