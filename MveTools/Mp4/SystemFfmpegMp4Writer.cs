namespace MveTools.Mp4
{
	using System;
	using System.Globalization;
	using System.IO;
	using Avi;
	using IO;
	using Media;

	public sealed class SystemFfmpegMp4Writer
	{
		public void Write(String path, IndexedVideo video, Int32 quality)
		{
			FileSystemHelper.EnsureParentDirectory(path);

			String tempDirectory = FileSystemHelper.CreateTempDirectory();
			String tempAviPath = Path.Combine(tempDirectory, "source.avi");

			try
			{
				new AviDibWriter().WriteRgb24(tempAviPath, video);
				FfmpegProcess.Run(
					[
						"-i", tempAviPath,
						"-c:v", "libx264",
						"-preset", "medium",
						"-crf", quality.ToString(CultureInfo.InvariantCulture),
						"-pix_fmt", "yuv420p",
						"-movflags", "+faststart",
						"-c:a", "aac",
						"-b:a", "192k",
						path
					],
					"MP4/H.264 export",
					"FFmpeg MP4/H.264 encoding");
			}
			finally
			{
				FileSystemHelper.TryDeleteDirectory(tempDirectory);
			}
		}
	}
}