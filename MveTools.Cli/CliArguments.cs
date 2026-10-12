namespace MveTools.Cli
{
	using System;
	using Localization;

	internal static class CliArguments
	{
		internal static Boolean IsHelpArgument(String value)
		{
			return String.Equals(value, "/?", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "-?", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
		}

		internal static Boolean ContainsHelpArgument(String[] args, Int32 startIndex)
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

		/// <summary>Whether the command has all its positional arguments; throws if it has too many.</summary>
		internal static Boolean HasPositionals(ParsedArguments parsed, Int32 expected)
		{
			if (parsed.Positionals.Count > expected)
			{
				throw new UsageException(AppText.Format("UnexpectedArgument", parsed.Positionals[expected]));
			}

			return parsed.Positionals.Count == expected;
		}
	}
}