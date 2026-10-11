namespace MveTools.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using Spectre.Console;
	using MveTools.Conversion;
	using MveTools.Inspection;
	using Localization;

	internal static class ConsoleOutput
	{
		internal static Boolean IsInteractive()
		{
			return !Console.IsInputRedirected && !Console.IsOutputRedirected;
		}

		internal static void WriteError(String message)
		{
			if (IsInteractive())
			{
				AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
			}
			else
			{
				Console.Error.WriteLine(message);
			}
		}

		internal static Int32 Fail(String message)
		{
			WriteError(message);
			return 1;
		}

		internal static void WriteInspection(MveFileInfo info)
		{
			if (!IsInteractive())
			{
				Console.WriteLine($"{AppText.Get("File")}: {info.Path}");
				Console.WriteLine($"{AppText.Get("Chunks")}: {info.Chunks.Count}");
				Console.WriteLine($"{AppText.Get("VideoChunks")}: {info.VideoChunkCount}");
				Console.WriteLine($"{AppText.Get("AudioChunks")}: {info.AudioChunkCount}");
				Console.WriteLine($"{AppText.Get("MagicWords")}: {Convert.ToHexString(info.MagicWords)}");
				foreach (IGrouping<String, MveChunkInfo> group in ChunkGroups(info))
				{
					Console.WriteLine($"{group.Key}: {group.Count()}");
				}

				return;
			}

			Grid grid = new();
			grid.AddColumn();
			grid.AddColumn();
			grid.AddRow(AppText.Get("File"), Markup.Escape(info.Path));
			grid.AddRow(AppText.Get("Chunks"), info.Chunks.Count.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("VideoChunks"), info.VideoChunkCount.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("AudioChunks"), info.AudioChunkCount.ToString(CultureInfo.InvariantCulture));
			grid.AddRow(AppText.Get("MagicWords"), Convert.ToHexString(info.MagicWords));
			AnsiConsole.Write(new Panel(grid).Header(AppText.Get("SummaryHeader")).BorderColor(Color.Cyan1));

			Table table = new Table().Border(TableBorder.Rounded);
			table.AddColumn(AppText.Get("Type"));
			table.AddColumn(new TableColumn(AppText.Get("Count")).RightAligned());
			foreach (IGrouping<String, MveChunkInfo> group in ChunkGroups(info))
			{
				table.AddRow(Markup.Escape(group.Key), group.Count().ToString(CultureInfo.InvariantCulture));
			}

			AnsiConsole.Write(table);
		}

		private static IEnumerable<IGrouping<String, MveChunkInfo>> ChunkGroups(MveFileInfo info)
		{
			return info.Chunks
				.GroupBy(chunk => chunk.KnownType?.ToString() ?? $"Unknown 0x{chunk.Type:X4}")
				.OrderBy(group => group.Key);
		}

		internal static void WriteConversionResult(ConversionResult result)
		{
			if (!IsInteractive())
			{
				Console.WriteLine($"{AppText.Get("Output")}: {result.OutputPath}");
				foreach (String step in result.Steps)
				{
					Console.WriteLine(AppText.LocalizeStep(step));
				}

				return;
			}

			AnsiConsole.MarkupLine($"[green]{Markup.Escape(AppText.Get("Done"))}[/] [grey]{Markup.Escape(result.OutputPath)}[/]");

			Table table = new Table().Border(TableBorder.Rounded);
			table.AddColumn(AppText.Get("Step"));
			foreach (String step in result.Steps)
			{
				table.AddRow(Markup.Escape(AppText.LocalizeStep(step)));
			}

			AnsiConsole.Write(table);
		}

		internal static void ConfigureConsoleEncoding()
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					SetConsoleCP(65001);
					SetConsoleOutputCP(65001);
				}

				Console.InputEncoding = Encoding.UTF8;
				Console.OutputEncoding = Encoding.UTF8;
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean SetConsoleCP(UInt32 codePageId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean SetConsoleOutputCP(UInt32 codePageId);
	}
}