namespace MveTools.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Xunit;

	/// <summary>
	/// Compares a value with the one recorded in <c>golden.txt</c>.
	/// Set the <c>MVETOOLS_UPDATE_GOLDEN</c> environment variable to re-record the file.
	/// </summary>
	internal static class Golden
	{
		private static readonly Object Gate = new();
		private static readonly String GoldenPath = ResolveGoldenPath();

		public static void Check(String actual, String variant = "", [CallerMemberName] String test = "")
		{
			String key = variant.Length == 0 ? test : $"{test}.{variant}";
			String encoded = actual.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
			lock (Gate)
			{
				Dictionary<String, String> values = Load();
				if (Environment.GetEnvironmentVariable("MVETOOLS_UPDATE_GOLDEN") is not null)
				{
					values[key] = encoded;
					File.WriteAllLines(GoldenPath, values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
					return;
				}

				Assert.True(values.TryGetValue(key, out String? expected), $"No golden value recorded for '{key}'.");
				Assert.Equal(expected, encoded);
			}
		}

		private static Dictionary<String, String> Load()
		{
			Dictionary<String, String> values = new(StringComparer.Ordinal);
			if (!File.Exists(GoldenPath))
			{
				return values;
			}

			foreach (String line in File.ReadAllLines(GoldenPath))
			{
				Int32 separator = line.IndexOf('=', StringComparison.Ordinal);
				if (separator > 0)
				{
					values[line[..separator]] = line[(separator + 1)..];
				}
			}

			return values;
		}

		private static String ResolveGoldenPath([CallerFilePath] String sourcePath = "")
		{
			return Path.Combine(Path.GetDirectoryName(sourcePath) ?? ".", "golden.txt");
		}
	}
}
