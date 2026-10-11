namespace MveTools.Mp4
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using Avi;
	using Media;

	public sealed class SystemFfmpegMp4Writer
	{
		private const String MissingFfmpegMessage =
			"MP4/H.264 export requires FFmpeg installed and available in PATH. " +
			"Install it with: Windows: winget install Gyan.FFmpeg; Debian/Ubuntu: sudo apt install ffmpeg; macOS: brew install ffmpeg.";

		public void Write(String path, IndexedVideo video, Int32 quality)
		{
			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			String tempDirectory = Path.Combine(Path.GetTempPath(), $"mvetools-{Guid.NewGuid():N}");
			Directory.CreateDirectory(tempDirectory);
			String tempAviPath = Path.Combine(tempDirectory, "source.avi");

			try
			{
				new AviDibWriter().WriteRgb24(tempAviPath, video);
				RunFfmpeg(tempAviPath, path, quality);
			}
			finally
			{
				TryDeleteDirectory(tempDirectory);
			}
		}

		private static void RunFfmpeg(String inputPath, String outputPath, Int32 quality)
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
			process.StartInfo.ArgumentList.Add("-nostdin");
			process.StartInfo.ArgumentList.Add("-y");
			process.StartInfo.ArgumentList.Add("-hide_banner");
			process.StartInfo.ArgumentList.Add("-loglevel");
			process.StartInfo.ArgumentList.Add("error");
			process.StartInfo.ArgumentList.Add("-i");
			process.StartInfo.ArgumentList.Add(inputPath);
			process.StartInfo.ArgumentList.Add("-c:v");
			process.StartInfo.ArgumentList.Add("libx264");
			process.StartInfo.ArgumentList.Add("-preset");
			process.StartInfo.ArgumentList.Add("medium");
			process.StartInfo.ArgumentList.Add("-crf");
			process.StartInfo.ArgumentList.Add(quality.ToString());
			process.StartInfo.ArgumentList.Add("-pix_fmt");
			process.StartInfo.ArgumentList.Add("yuv420p");
			process.StartInfo.ArgumentList.Add("-movflags");
			process.StartInfo.ArgumentList.Add("+faststart");
			process.StartInfo.ArgumentList.Add("-c:a");
			process.StartInfo.ArgumentList.Add("aac");
			process.StartInfo.ArgumentList.Add("-b:a");
			process.StartInfo.ArgumentList.Add("192k");
			process.StartInfo.ArgumentList.Add(outputPath);

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
				throw new InvalidOperationException($"FFmpeg MP4/H.264 encoding failed with exit code {process.ExitCode}. {details.Trim()}");
			}
		}

		private static void TryDeleteDirectory(String path)
		{
			try
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, true);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}
}