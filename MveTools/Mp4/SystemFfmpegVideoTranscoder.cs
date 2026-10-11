namespace MveTools.Mp4
{
	using System;
	using IO;

	public sealed class SystemFfmpegVideoTranscoder
	{
		public void TranscodeToUncompressedAvi(String inputPath, String outputAviPath)
		{
			FileSystemHelper.EnsureParentDirectory(outputAviPath);

			FfmpegProcess.Run(
				[
					"-i", inputPath,
					"-c:v", "rawvideo",
					"-pix_fmt", "bgr24",
					"-c:a", "pcm_s16le",
					outputAviPath
				],
				"MP4/video import",
				"FFmpeg video import");
		}
	}
}