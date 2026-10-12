namespace MveTools.Cli
{
	using System;
	using System.Threading.Tasks;
	using Commands;
	using Localization;

	internal static class Program
	{
		public static async Task<Int32> Main(String[] args)
		{
			ConsoleOutput.ConfigureConsoleEncoding();
			AppText.SetLanguage(AppText.Current);
			try
			{
				if (args.Length == 0)
				{
					if (ConsoleOutput.IsInteractive())
					{
						return await InteractiveMenu.RunAsync().ConfigureAwait(false);
					}

					HelpPrinter.PrintHelp();
					return 0;
				}

				if (CliArguments.IsHelpArgument(args[0]))
				{
					HelpPrinter.PrintHelp();
					return 0;
				}

				return args[0].ToLowerInvariant() switch
				{
					"inspect" => InspectCommand.Run(args),
					"to-video" => await ToVideoCommand.RunAsync(args).ConfigureAwait(false),
					"to-mve" => await ToMveCommand.RunAsync(args).ConfigureAwait(false),
					_ => ConsoleOutput.Fail(AppText.Format("UnknownCommand", args[0]))
				};
			}
			catch (OperationCanceledException)
			{
				ConsoleOutput.WriteError(AppText.Get("Cancelled"));
				return 130;
			}
			catch (Exception ex)
			{
				ConsoleOutput.WriteError(AppText.DescribeError(ex));
				return 1;
			}
		}
	}
}