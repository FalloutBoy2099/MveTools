namespace MveTools.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Localization;

	internal enum OptionKind
	{
		Flag,
		Integer,
		Number,
		Text
	}

	/// <summary>One option of a command: its name, its other spellings and the values it accepts.</summary>
	internal sealed class OptionSpec
	{
		private OptionSpec(String name, OptionKind kind, String[] aliases, Double minimum, Double maximum, Boolean minimumIsExclusive)
		{
			Name = name;
			Kind = kind;
			Names = [name, .. aliases];
			Minimum = minimum;
			Maximum = maximum;
			MinimumIsExclusive = minimumIsExclusive;
		}

		public String Name { get; }

		public OptionKind Kind { get; }

		public IReadOnlyList<String> Names { get; }

		public Double Minimum { get; }

		public Double Maximum { get; }

		public Boolean MinimumIsExclusive { get; }

		public static OptionSpec Flag(String name, params String[] aliases)
		{
			return new OptionSpec(name, OptionKind.Flag, aliases, 0, 0, false);
		}

		public static OptionSpec Text(String name, params String[] aliases)
		{
			return new OptionSpec(name, OptionKind.Text, aliases, 0, 0, false);
		}

		public static OptionSpec Integer(String name, Int32 minimum, Int32 maximum, params String[] aliases)
		{
			return new OptionSpec(name, OptionKind.Integer, aliases, minimum, maximum, false);
		}

		/// <summary>A decimal number strictly greater than <paramref name="exclusiveMinimum"/>.</summary>
		public static OptionSpec Number(String name, Double exclusiveMinimum, params String[] aliases)
		{
			return new OptionSpec(name, OptionKind.Number, aliases, exclusiveMinimum, Double.MaxValue, true);
		}
	}

	/// <summary>The positional arguments and the option values found on a command line.</summary>
	internal sealed class ParsedArguments
	{
		private readonly Dictionary<String, Object> values;

		public ParsedArguments(IReadOnlyList<String> positionals, Dictionary<String, Object> values)
		{
			Positionals = positionals;
			this.values = values;
		}

		public IReadOnlyList<String> Positionals { get; }

		public Boolean Has(String name) => values.ContainsKey(name);

		public Int32? GetInteger(String name) => values.TryGetValue(name, out Object? value) ? (Int32)value : null;

		public Double? GetNumber(String name) => values.TryGetValue(name, out Object? value) ? (Double)value : null;

		public String? GetText(String name) => values.TryGetValue(name, out Object? value) ? (String)value : null;
	}

	/// <summary>
	/// Reads a command line strictly: unknown options, missing or invalid values, repeated options and conflicting options
	/// are errors instead of being ignored.
	/// </summary>
	internal sealed class ArgumentParser
	{
		private readonly Dictionary<String, OptionSpec> options = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<(String First, String Second, String MessageKey)> conflicts = [];

		public ArgumentParser(IEnumerable<OptionSpec> specs)
		{
			foreach (OptionSpec spec in specs)
			{
				foreach (String name in spec.Names)
				{
					options.Add(name, spec);
				}
			}
		}

		/// <summary>Declares two options that cannot be used together; <paramref name="messageKey"/> is the text shown when they are.</summary>
		public ArgumentParser Conflict(String first, String second, String messageKey)
		{
			conflicts.Add((first, second, messageKey));
			return this;
		}

		public ParsedArguments Parse(String[] args, Int32 startIndex)
		{
			List<String> positionals = [];
			Dictionary<String, Object> values = new(StringComparer.Ordinal);
			Boolean optionsEnded = false;
			for (Int32 index = startIndex; index < args.Length; index++)
			{
				String token = args[index];
				if (optionsEnded || token.Length < 2 || token[0] != '-')
				{
					positionals.Add(token);
					continue;
				}

				if (token == "--")
				{
					optionsEnded = true;
					continue;
				}

				if (!options.TryGetValue(token, out OptionSpec? spec))
				{
					throw new UsageException(AppText.Format("OptionUnknown", token));
				}

				if (values.ContainsKey(spec.Name))
				{
					throw new UsageException(AppText.Format("OptionRepeated", token));
				}

				if (spec.Kind == OptionKind.Flag)
				{
					values[spec.Name] = true;
					continue;
				}

				if (index + 1 >= args.Length)
				{
					throw new UsageException(AppText.Format("OptionNeedsValue", token));
				}

				index++;
				values[spec.Name] = ReadValue(spec, token, args[index]);
			}

			foreach ((String first, String second, String messageKey) in conflicts)
			{
				if (values.ContainsKey(first) && values.ContainsKey(second))
				{
					throw new UsageException(AppText.Get(messageKey));
				}
			}

			return new ParsedArguments(positionals, values);
		}

		private static Object ReadValue(OptionSpec spec, String token, String raw)
		{
			switch (spec.Kind)
			{
				case OptionKind.Integer:
					if (Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 integer)
						&& integer >= spec.Minimum && integer <= spec.Maximum)
					{
						return integer;
					}

					break;
				case OptionKind.Number:
					// A decimal comma is accepted as well as a decimal point.
					if (Double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out Double number)
						&& Double.IsFinite(number) && number > spec.Minimum)
					{
						return number;
					}

					break;
				default:
					if (!String.IsNullOrWhiteSpace(raw))
					{
						return raw;
					}

					break;
			}

			throw new UsageException(AppText.Format("OptionInvalidValue", token, raw));
		}
	}
}