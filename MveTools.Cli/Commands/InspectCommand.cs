namespace MveTools.Cli
{
	using System;
	using MveTools.Inspection;

	internal static class InspectCommand
	{
		internal static Int32 Run(String[] args)
		{
			if (CliArguments.ContainsHelpArgument(args, 1))
			{
				HelpPrinter.PrintInspectHelp();
				return 0;
			}

			if (args.Length < 2)
			{
				return ConsoleOutput.Fail(AppText.Get("UsageInspect"));
			}

			MveFileInfo info = new MveFileInspector().Inspect(args[1]);
			ConsoleOutput.WriteInspection(info);
			return 0;
		}
	}
}
