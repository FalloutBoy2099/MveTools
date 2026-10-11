namespace MveTools.Mp4
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;

	public sealed class SystemFfmpegVideoTranscoder
	{
		private const String MissingFfmpegMessage =
			"MP4/video import requires FFmpeg installed and available in PATH. " +
			"Install it with: Windows: winget install Gyan.FFmpeg; Debian/Ubuntu: sudo apt install ffmpeg; macOS: brew install ffmpeg.";

		public void TranscodeToUncompressedAvi(String inputPath, String outputAviPath)
		{
			String? directory = Path.GetDirectoryName(Path.GetFullPath(outputAviPath));
			if (!String.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using Process process = new();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = "ffmpeg",
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};
			process.StartInfo.ArgumentList.Add("-nostdin");
			process.StartInfo.ArgumentList.Add("-y");
			process.StartInfo.ArgumentList.Add("-hide_banner");
			process.StartInfo.ArgumentList.Add("-loglevel");
			process.StartInfo.ArgumentList.Add("error");
			process.StartInfo.ArgumentList.Add("-i");
			process.StartInfo.ArgumentList.Add(inputPath);
			process.StartInfo.ArgumentList.Add("-c:v");
			process.StartInfo.ArgumentList.Add("rawvideo");
			process.StartInfo.ArgumentList.Add("-pix_fmt");
			process.StartInfo.ArgumentList.Add("bgr24");
			process.StartInfo.ArgumentList.Add("-c:a");
			process.StartInfo.ArgumentList.Add("pcm_s16le");
			process.StartInfo.ArgumentList.Add(outputAviPath);

			try
			{
				process.Start();
			}
			catch (Exception ex) when (ex is FileNotFoundException or Win32Exception)
			{
				throw new NotSupportedException(MissingFfmpegMessage, ex);
			}

			String standardError = process.StandardError.ReadToEnd();
			String standardOutput = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				String details = String.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
				throw new InvalidOperationException($"FFmpeg video import failed with exit code {process.ExitCode}. {details.Trim()}");
			}
		}
	}
}