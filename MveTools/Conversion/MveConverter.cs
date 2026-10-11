namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Audio;
	using Avi;
	using IO;
	using Media;
	using Mp4;
	using Mve;

	public sealed class MveConverter
	{
		/// <summary>Whether <paramref name="path"/> has an extension accepted as a video-to-MVE source.</summary>
		public static Boolean IsSupportedVideoSource(String path)
		{
			return VideoSourceReader.IsAvi(path) || VideoSourceReader.IsFfmpegVideoSource(path);
		}

		public Task<ConversionResult> ConvertMveToVideoAsync(
			String inputMvePath,
			String outputVideoPath,
			MveToVideoOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			options ??= new MveToVideoOptions();
			EnsureInputFile(inputMvePath);
			EnsureOutputCanBeWritten(outputVideoPath, options.Overwrite);
			EnsureMp4Quality(options.Quality);

			IndexedVideo video = new MveReader().Read(inputMvePath);
			String writeStep = options.Standard switch
			{
				VideoStandard.AviIndexed8 => WriteAviIndexed(outputVideoPath, video),
				VideoStandard.AviRgb24 => WriteAviRgb24(outputVideoPath, video),
				VideoStandard.WavPcm => WriteWav(outputVideoPath, video),
				VideoStandard.Mp4H264 => WriteMp4H264(outputVideoPath, video, options.Quality),
				_ => throw new NotSupportedException($"Unsupported output video standard: {options.Standard}.")
			};
			EnsureProducedFile(outputVideoPath);

			ConversionResult result = new(
				outputVideoPath,
				[
					"Read MVE with integrated 8 bpp video and audio decoder",
					writeStep
				]);
			return Task.FromResult(result);
		}

		public Task<ConversionResult> ConvertVideoToMveAsync(
			String inputVideoPath,
			String outputMvePath,
			VideoToMveOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			options ??= new VideoToMveOptions();
			RgbColor[]? customPalette = ValidateInputs(inputVideoPath, outputMvePath, options);

			VideoSourceReader.Result source = VideoSourceReader.ReadVideo(inputVideoPath, customPalette, options.UseFloydSteinbergDither);
			IndexedVideo video = ApplySelection(source.Video, options);
			video = ApplyOverrides(video, options);

			if (!String.IsNullOrWhiteSpace(options.SavePalettePath))
			{
				PaletteFile.WriteAct(options.SavePalettePath, video.Palette);
			}

			List<String> steps = DescribeSelection(source.Step, video, options);
			video = EnsureMveCompatibleDimensions(video, steps);
			AddConversionIntentSteps(steps, options);

			if (options.TestOnly)
			{
				steps.Add("Test mode: input and parameters validated; MVE writing skipped.");
			}
			else
			{
				video = WriteMveWithSizeFallback(outputMvePath, video, steps);
				EnsureProducedFile(outputMvePath);
				steps.Add(video.Audio is null
					? "Wrote compressed Interplay MVE 0x11 video without audio and without external tools"
					: "Wrote compressed Interplay MVE 0x11 video with MVE DPCM audio without external tools");
			}

			return Task.FromResult(new ConversionResult(outputMvePath, steps));
		}

		private static String WriteAviIndexed(String outputPath, IndexedVideo video)
		{
			new AviDibWriter().Write(outputPath, video);
			return "Wrote AVI DIB indexed 8 bpp with PCM audio without external tools";
		}

		private static String WriteAviRgb24(String outputPath, IndexedVideo video)
		{
			new AviDibWriter().WriteRgb24(outputPath, video);
			return "Wrote AVI DIB RGB 24 bpp with PCM audio without external tools";
		}

		private static String WriteWav(String outputPath, IndexedVideo video)
		{
			if (video.Audio is null)
			{
				throw new NotSupportedException("This MVE does not contain a supported audio track to export as WAV.");
			}

			new WavPcmWriter().Write(outputPath, video.Audio);
			return "Wrote WAV PCM 16-bit audio without external tools";
		}

		private static String WriteMp4H264(String outputPath, IndexedVideo video, Int32 quality)
		{
			new SystemFfmpegMp4Writer().Write(outputPath, video, quality);
			return $"Wrote MP4/H.264 with system FFmpeg using CRF {quality}";
		}

		/// <summary>Checks every file the conversion needs and returns the custom palette, if any.</summary>
		private static RgbColor[]? ValidateInputs(String inputVideoPath, String outputMvePath, VideoToMveOptions options)
		{
			EnsureInputFile(inputVideoPath);
			if (!options.TestOnly)
			{
				EnsureOutputCanBeWritten(outputMvePath, options.Overwrite);
			}

			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				EnsureInputFile(options.AudioSourcePath, "audio source");
			}

			if (String.IsNullOrWhiteSpace(options.PalettePath))
			{
				return null;
			}

			EnsureInputFile(options.PalettePath, "palette");
			return PaletteFile.Read(options.PalettePath);
		}

		/// <summary>Applies the external audio, the frame range and the audio on/off/sample-rate options.</summary>
		private static IndexedVideo ApplySelection(IndexedVideo video, VideoToMveOptions options)
		{
			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				video = VideoEditing.WithAudio(video, VideoSourceReader.ReadExternalAudio(options.AudioSourcePath));
			}

			video = VideoEditing.Slice(video, options.StartFrame, options.EndFrame, options.FrameCount);
			if (video.Audio is null)
			{
				return video;
			}

			if (!options.IncludeAudio)
			{
				return VideoEditing.WithAudio(video, null);
			}

			return video.Audio.SampleRate != options.AudioSampleRate
				? VideoEditing.WithAudio(video, AudioEditing.Resample(video.Audio, options.AudioSampleRate))
				: video;
		}

		/// <summary>Validates the requested width/height (no rescaling yet) and applies the frame-rate override.</summary>
		private static IndexedVideo ApplyOverrides(IndexedVideo video, VideoToMveOptions options)
		{
			if (options.Width.HasValue && video.Width != options.Width.Value)
			{
				throw new NotSupportedException("The integrated AVI reader does not rescale yet. Provide an AVI with the requested width.");
			}

			if (options.Height.HasValue && video.Height != options.Height.Value)
			{
				throw new NotSupportedException("The integrated AVI reader does not rescale yet. Provide an AVI with the requested height.");
			}

			return options.FrameRate.HasValue && Math.Abs(video.FrameRate - options.FrameRate.Value) > 0.001d
				? VideoEditing.WithFrameRate(video, options.FrameRate.Value)
				: video;
		}

		private static List<String> DescribeSelection(String sourceStep, IndexedVideo video, VideoToMveOptions options)
		{
			return
			[
				sourceStep,
				$"Detected source video profile: {video.Width}x{video.Height} at {video.FrameRate:0.###} fps",
				$"Selected frame range: {video.Frames.Count} frame(s)",
				options.IncludeAudio && video.Audio is not null
					? $"Selected audio: PCM {video.Audio.SampleRate} Hz, {video.Audio.BitsPerSample}-bit, {video.Audio.Channels} channel(s)"
					: "Selected audio: none",
				$"Compression profile: {options.CompressionMode}"
			];
		}

		private static void AddConversionIntentSteps(List<String> steps, VideoToMveOptions options)
		{
			if (options.BitRateKilobytesPerSecond.HasValue)
			{
				steps.Add($"Bitrate target requested: {options.BitRateKilobytesPerSecond.Value} KiB/s (recorded as conversion intent; integrated encoder stays lossless per block).");
			}

			if (options.SizeLimitMegabytes.HasValue)
			{
				steps.Add($"Size limit requested: {options.SizeLimitMegabytes.Value:0.###} MiB (recorded as conversion intent; integrated encoder stays lossless per block).");
			}
		}

		/// <summary>Writes the MVE, halving the video size until every compressed frame fits in the 16-bit payload limit.</summary>
		private static IndexedVideo WriteMveWithSizeFallback(String outputMvePath, IndexedVideo source, List<String> steps)
		{
			IndexedVideo current = source;
			while (true)
			{
				try
				{
					new MveWriter().Write(outputMvePath, current);
					return current;
				}
				catch (MveFrameTooLargeException)
				{
					if (File.Exists(outputMvePath))
					{
						File.Delete(outputMvePath);
					}

					IndexedVideo resized = ResizeForMvePayloadLimit(current);
					if (resized.Width == current.Width && resized.Height == current.Height)
					{
						throw;
					}

					steps.Add($"Compressed frame exceeded the MVE payload limit; downscaled video from {current.Width}x{current.Height} to {resized.Width}x{resized.Height} and retried.");
					current = resized;
				}
			}
		}

		private static IndexedVideo EnsureMveCompatibleDimensions(IndexedVideo video, List<String> steps)
		{
			Int32 width = VideoEditing.AlignDownToBlock(video.Width);
			Int32 height = VideoEditing.AlignDownToBlock(video.Height);
			if (width == video.Width && height == video.Height)
			{
				return video;
			}

			IndexedVideo resized = VideoEditing.Resize(video, width, height);
			steps.Add($"Adjusted video dimensions from {video.Width}x{video.Height} to {resized.Width}x{resized.Height} so MVE blocks stay aligned to 8 pixels.");
			return resized;
		}

		private static IndexedVideo ResizeForMvePayloadLimit(IndexedVideo video)
		{
			Int32 width = VideoEditing.AlignDownToBlock(Math.Max(VideoEditing.MveBlockSize, video.Width / 2));
			Int32 height = VideoEditing.AlignDownToBlock(Math.Max(VideoEditing.MveBlockSize, video.Height / 2));
			return width == video.Width && height == video.Height
				? video
				: VideoEditing.Resize(video, width, height);
		}

		private static void EnsureMp4Quality(Int32 quality)
		{
			if (quality < 0 || quality > 51)
			{
				throw new ArgumentOutOfRangeException(nameof(quality), "MP4/H.264 quality must be a CRF value between 0 and 51.");
			}
		}

		private static void EnsureInputFile(String path, String label = "input")
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"The {label} file does not exist.", path);
			}
		}

		private static void EnsureOutputCanBeWritten(String path, Boolean overwrite)
		{
			FileSystemHelper.EnsureParentDirectory(path);

			if (!overwrite && File.Exists(path))
			{
				throw new IOException($"Output already exists: {path}");
			}
		}

		private static void EnsureProducedFile(String path)
		{
			FileInfo file = new(path);
			if (!file.Exists || file.Length == 0)
			{
				throw new FileNotFoundException($"The expected output file was not produced: {path}", path);
			}
		}
	}
}