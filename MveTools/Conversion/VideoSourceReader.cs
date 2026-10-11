namespace MveTools.Conversion
{
	using System;
	using System.IO;
	using Audio;
	using Avi;
	using IO;
	using Media;
	using Mp4;

	internal static class VideoSourceReader
	{
		public readonly record struct Result(IndexedVideo Video, String Step);

		public static Boolean IsAvi(String path)
		{
			return String.Equals(Path.GetExtension(path), ".avi", StringComparison.OrdinalIgnoreCase);
		}

		public static Boolean IsFfmpegVideoSource(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			return extension is ".mp4" or ".m4v" or ".mov";
		}

		public static Result ReadVideo(String inputPath, RgbColor[]? customPalette, Boolean useFloydSteinbergDither)
		{
			AviReadOptions readOptions = new()
			{
				Palette = customPalette,
				UseFloydSteinbergDither = useFloydSteinbergDither
			};

			if (IsAvi(inputPath))
			{
				return new Result(
					new AviDibReader().Read(inputPath, readOptions),
					"Read AVI DIB and PCM audio with integrated reader");
			}

			if (!IsFfmpegVideoSource(inputPath))
			{
				throw new NotSupportedException("Video to MVE supports uncompressed AVI and FFmpeg-readable MP4/M4V/MOV sources.");
			}

			String tempDirectory = FileSystemHelper.CreateTempDirectory();
			String tempAviPath = Path.Combine(tempDirectory, "source.avi");
			try
			{
				new SystemFfmpegVideoTranscoder().TranscodeToUncompressedAvi(inputPath, tempAviPath);
				return new Result(
					new AviDibReader().Read(tempAviPath, readOptions),
					"Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader");
			}
			finally
			{
				FileSystemHelper.TryDeleteDirectory(tempDirectory);
			}
		}

		public static PcmAudioTrack ReadExternalAudio(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension == ".wav")
			{
				return new WavPcmReader().Read(path);
			}

			if (extension == ".avi")
			{
				IndexedVideo source = new AviDibReader().Read(path);
				return source.Audio ?? throw new NotSupportedException("The external AVI does not contain supported PCM audio.");
			}

			throw new NotSupportedException("Integrated external audio supports PCM WAV and uncompressed AVI PCM audio.");
		}
	}
}