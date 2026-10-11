namespace MveTools.Cli
{
	using System;
	using System.Globalization;
	using System.Linq;

	internal static class CliArguments
	{
		internal static Boolean HasFlag(String[] args, String name)
		{
			return args.Any(arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
		}

		internal static Int32? ReadOptionalInt(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0 && index + 1 < args.Length && Int32.TryParse(args[index + 1], out Int32 value) ? value : null;
		}

		internal static Double? ReadOptionalDouble(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0
				&& index + 1 < args.Length
				&& Double.TryParse(args[index + 1], CultureInfo.InvariantCulture, out Double value)
					? value
					: null;
		}

		internal static String? ReadOptionalString(String[] args, String name)
		{
			Int32 index = Array.FindIndex(args, arg => String.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
		}

		internal static Boolean ReadOverwrite(String[] args)
		{
			if (HasFlag(args, "--no-overwrite"))
			{
				return false;
			}

			return true;
		}

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
	}
}