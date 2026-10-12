namespace MveTools.Conversion
{
	using System;
	using System.IO;
	using System.Threading;
	using Audio;
	using Avi;
	using Errors;
	using IO;
	using Media;
	using Mp4;

	internal static class VideoSourceReader
	{
		/// <summary>The video read from a source file. The frames are decoded on demand, so the result must stay alive while the video is used.</summary>
		public sealed class Result : IDisposable
		{
			private readonly String? temporaryDirectory;

			public Result(IndexedVideo video, ConversionMessage message, String? temporaryDirectory = null)
			{
				Video = video;
				Message = message;
				this.temporaryDirectory = temporaryDirectory;
			}

			public IndexedVideo Video { get; }

			public ConversionMessage Message { get; }

			public void Dispose()
			{
				if (temporaryDirectory is not null)
				{
					FileSystemHelper.TryDeleteDirectory(temporaryDirectory);
				}
			}
		}

		public static Boolean IsAvi(String path)
		{
			return String.Equals(Path.GetExtension(path), ".avi", StringComparison.OrdinalIgnoreCase);
		}

		public static Boolean IsFfmpegVideoSource(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			return extension is ".mp4" or ".m4v" or ".mov";
		}

		public static Result ReadVideo(String inputPath, RgbColor[]? customPalette, Boolean useFloydSteinbergDither, CancellationToken cancellationToken)
		{
			AviReadOptions readOptions = new()
			{
				Palette = customPalette,
				UseFloydSteinbergDither = useFloydSteinbergDither,
				LazyFrames = true
			};

			if (IsAvi(inputPath))
			{
				return new Result(
					new AviDibReader().Read(inputPath, readOptions, cancellationToken),
					new ConversionMessage(ConversionMessageCode.ReadAvi));
			}

			if (!IsFfmpegVideoSource(inputPath))
			{
				throw new NotSupportedException("Video to MVE supports uncompressed AVI and FFmpeg-readable MP4/M4V/MOV sources.").Tag(MveErrorCode.VideoSourceNotSupported);
			}

			String tempDirectory = FileSystemHelper.CreateTempDirectory();
			try
			{
				String tempAviPath = Path.Combine(tempDirectory, "source.avi");
				new SystemFfmpegVideoTranscoder().TranscodeToUncompressedAvi(inputPath, tempAviPath, cancellationToken);
				return new Result(
					new AviDibReader().Read(tempAviPath, readOptions, cancellationToken),
					new ConversionMessage(ConversionMessageCode.ConvertedSource),
					tempDirectory);
			}
			catch
			{
				FileSystemHelper.TryDeleteDirectory(tempDirectory);
				throw;
			}
		}

		public static PcmAudioTrack ReadExternalAudio(String path, CancellationToken cancellationToken)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension == ".wav")
			{
				return new WavPcmReader().Read(path);
			}

			if (extension == ".avi")
			{
				IndexedVideo source = new AviDibReader().Read(path, new AviReadOptions { LazyFrames = true }, cancellationToken);
				return source.Audio ?? throw new NotSupportedException("The external AVI does not contain supported PCM audio.").Tag(MveErrorCode.ExternalAviWithoutAudio);
			}

			throw new NotSupportedException("Integrated external audio supports PCM WAV and uncompressed AVI PCM audio.").Tag(MveErrorCode.ExternalAudioNotSupported);
		}
	}
}