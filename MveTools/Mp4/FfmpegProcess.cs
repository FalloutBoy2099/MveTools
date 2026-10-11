namespace MveTools.Mp4
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;

	internal static class FfmpegProcess
	{
		private const String InstallHint = "Install it with: Windows: winget install Gyan.FFmpeg; Debian/Ubuntu: sudo apt install ffmpeg; macOS: brew install ffmpeg.";

		/// <summary>Runs the system <c>ffmpeg</c> quietly and throws if it is missing or fails.</summary>
		/// <param name="arguments">Arguments placed after the common non-interactive flags.</param>
		/// <param name="purpose">Subject used in messages, e.g. "MP4/H.264 export".</param>
		/// <param name="failureLabel">Prefix of the failure message, e.g. "FFmpeg MP4/H.264 encoding".</param>
		public static void Run(IEnumerable<String> arguments, String purpose, String failureLabel)
		{
			using Process process = new();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = "ffmpeg",
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};

			foreach (String argument in new[] { "-nostdin", "-y", "-hide_banner", "-loglevel", "error" })
			{
				process.StartInfo.ArgumentList.Add(argument);
			}

			foreach (String argument in arguments)
			{
				process.StartInfo.ArgumentList.Add(argument);
			}

			try
			{
				process.Start();
			}
			catch (Exception ex) when (ex is FileNotFoundException or Win32Exception)
			{
				throw new NotSupportedException($"{purpose} requires FFmpeg installed and available in PATH. {InstallHint}", ex);
			}

			String standardError = process.StandardError.ReadToEnd();
			String standardOutput = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				String details = String.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
				throw new InvalidOperationException($"{failureLabel} failed with exit code {process.ExitCode}. {details.Trim()}");
			}
		}
	}
}