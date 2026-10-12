namespace MveTools.Cli
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Spectre.Console;
	using MveTools.Conversion;
	using Localization;

	internal static class ConversionRunner
	{
		/// <summary>
		/// Runs a conversion. In a terminal a progress bar follows the stages; in both cases Ctrl+C cancels the conversion
		/// (the partial output is removed by the library) instead of killing the process.
		/// </summary>
		internal static async Task<ConversionResult> RunAsync(
			String label,
			Func<IProgress<ConversionProgress>?, CancellationToken, Task<ConversionResult>> convert)
		{
			using CancellationTokenSource cancellation = new();
			ConsoleCancelEventHandler onCancel = (_, e) =>
			{
				e.Cancel = true;
				cancellation.Cancel();
			};

			Console.CancelKeyPress += onCancel;
			try
			{
				if (!ConsoleOutput.IsInteractive())
				{
					return await convert(null, cancellation.Token).ConfigureAwait(false);
				}

				return await AnsiConsole.Progress()
					.AutoClear(false)
					.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn())
					.StartAsync(async context =>
					{
						ProgressTask task = context.AddTask(Markup.Escape(label), maxValue: 100);
						ConversionStage? currentStage = null;
						ImmediateProgress progress = new(report =>
						{
							if (currentStage != report.Stage)
							{
								currentStage = report.Stage;
								task.Description = Markup.Escape(DescribeStage(report.Stage));
							}

							task.Value = Math.Clamp(report.Fraction, 0, 1) * 100;
						});

						ConversionResult result = await convert(progress, cancellation.Token).ConfigureAwait(false);
						task.Value = 100;
						return result;
					}).ConfigureAwait(false);
			}
			finally
			{
				Console.CancelKeyPress -= onCancel;
			}
		}

		private static String DescribeStage(ConversionStage stage)
		{
			return AppText.Get("Stage" + stage);
		}

		/// <summary>Calls the handler on the thread that reports (unlike <see cref="Progress{T}"/>, which posts to another thread).</summary>
		private sealed class ImmediateProgress : IProgress<ConversionProgress>
		{
			private readonly Action<ConversionProgress> handler;

			public ImmediateProgress(Action<ConversionProgress> handler)
			{
				this.handler = handler;
			}

			public void Report(ConversionProgress value)
			{
				handler(value);
			}
		}
	}
}