namespace MveTools.Cli.Commands
{
	using System;
	using MveTools.Inspection;
	using Localization;

	internal static class InspectCommand
	{
		internal static Int32 Run(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintInspectHelp();
				return 0;
			}

			ParsedArguments parsed = new ArgumentParser([]).Parse(args, 1);
			if (!CliArguments.HasPositionals(parsed, 1))
			{
				return ConsoleOutput.Fail(AppText.Get("UsageInspect"));
			}

			MveFileInfo info = new MveFileInspector().Inspect(parsed.Positionals[0]);
			ConsoleOutput.WriteInspection(info);
			return 0;
		}
	}
}