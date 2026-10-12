namespace MveTools.Mp4
{
	using System;
	using System.IO;
	using System.Threading;
	using Errors;
	using IO;

	public sealed class SystemFfmpegVideoTranscoder
	{
		public void TranscodeToUncompressedAvi(String inputPath, String outputAviPath, CancellationToken cancellationToken = default)
		{
			FileSystemHelper.EnsureParentDirectory(outputAviPath);

			FfmpegProcess.Run(
				[
					"-i", Path.GetFullPath(inputPath),
					"-c:v", "rawvideo",
					"-pix_fmt", "bgr24",
					"-c:a", "pcm_s16le",
					Path.GetFullPath(outputAviPath)
				],
				"MP4/video import",
				"FFmpeg video import",
				MveErrorCode.FfmpegMissingForImport,
				MveErrorCode.FfmpegImportFailed,
				cancellationToken);
		}
	}
}