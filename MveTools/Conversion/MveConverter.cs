namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Audio;
	using Avi;
	using Errors;
	using IO;
	using Media;
	using Mp4;
	using Mve;

	public sealed class MveConverter
	{
		/// <summary>Largest per-channel color difference tried when a size or bit rate target needs lossy frame copies.</summary>
		private const Int32 MaxTolerance = 32;

		/// <summary>The same limit for <see cref="MveCompressionMode.HighQuality"/>: the picture may only change slightly.</summary>
		private const Int32 MaxHighQualityTolerance = 12;

		private const Double BytesPerMebibyte = 1024d * 1024d;

		/// <summary>Whether <paramref name="path"/> has an extension accepted as a video-to-MVE source.</summary>
		public static Boolean IsSupportedVideoSource(String path)
		{
			return VideoSourceReader.IsAvi(path) || VideoSourceReader.IsFfmpegVideoSource(path);
		}

		public Task<ConversionResult> ConvertMveToVideoAsync(
			String inputMvePath,
			String outputVideoPath,
			MveToVideoOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<ConversionProgress>? progress = null)
		{
			return Task.Run(() => ConvertMveToVideo(inputMvePath, outputVideoPath, options, cancellationToken, progress), cancellationToken);
		}

		public Task<ConversionResult> ConvertVideoToMveAsync(
			String inputVideoPath,
			String outputMvePath,
			VideoToMveOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<ConversionProgress>? progress = null)
		{
			return Task.Run(() => ConvertVideoToMve(inputVideoPath, outputMvePath, options, cancellationToken, progress), cancellationToken);
		}

		public ConversionResult ConvertMveToVideo(
			String inputMvePath,
			String outputVideoPath,
			MveToVideoOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<ConversionProgress>? progress = null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			options ??= new MveToVideoOptions();
			EnsureInputFile(inputMvePath, "input", MveErrorCode.InputFileMissing);
			EnsureOutputCanBeWritten(outputVideoPath, options.Overwrite);
			EnsureMp4Quality(options.Quality);
			if (options.Standard is not (VideoStandard.AviIndexed8 or VideoStandard.AviRgb24 or VideoStandard.WavPcm or VideoStandard.Mp4H264))
			{
				throw new NotSupportedException($"Unsupported output video standard: {options.Standard}.").Tag(MveErrorCode.UnsupportedOutputStandard, options.Standard);
			}

			IndexedVideo video = new MveReader().Read(inputMvePath, cancellationToken, StageProgress.For(progress, ConversionStage.ReadingMve));
			if (options.Standard == VideoStandard.WavPcm && video.Audio is null)
			{
				throw new NotSupportedException("This MVE does not contain a supported audio track to export as WAV.").Tag(MveErrorCode.NoAudioForWav);
			}

			IProgress<Double>? writeProgress = StageProgress.For(progress, ConversionStage.WritingVideo);
			ConversionMessage writeMessage;
			try
			{
				writeMessage = options.Standard switch
				{
					VideoStandard.AviIndexed8 => WriteAviIndexed(outputVideoPath, video, cancellationToken, writeProgress),
					VideoStandard.AviRgb24 => WriteAviRgb24(outputVideoPath, video, cancellationToken, writeProgress),
					VideoStandard.WavPcm => WriteWav(outputVideoPath, video),
					_ => WriteMp4H264(outputVideoPath, video, options.Quality, cancellationToken)
				};
			}
			catch
			{
				TryDeleteFile(outputVideoPath);
				throw;
			}

			EnsureProducedFile(outputVideoPath);
			return new ConversionResult(outputVideoPath, [new ConversionMessage(ConversionMessageCode.ReadMve), writeMessage]);
		}

		public ConversionResult ConvertVideoToMve(
			String inputVideoPath,
			String outputMvePath,
			VideoToMveOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<ConversionProgress>? progress = null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			options ??= new VideoToMveOptions();
			RgbColor[]? customPalette = ValidateInputs(inputVideoPath, outputMvePath, options);

			using VideoSourceReader.Result source = VideoSourceReader.ReadVideo(inputVideoPath, customPalette, options.UseFloydSteinbergDither, cancellationToken);
			IndexedVideo video = ApplySelection(source.Video, options, cancellationToken);
			video = ApplyFrameRate(video, options);

			if (!String.IsNullOrWhiteSpace(options.SavePalettePath))
			{
				PaletteFile.WriteAct(options.SavePalettePath, video.Palette);
			}

			List<ConversionMessage> steps = DescribeSelection(source.Message, video, options);
			video = ApplyRequestedSize(video, options, steps);
			video = EnsureMveCompatibleDimensions(video, steps);
			Int64? targetBytes = DescribeTarget(video, options, steps);

			if (options.TestOnly)
			{
				steps.Add(new ConversionMessage(ConversionMessageCode.TestMode));
			}
			else
			{
				video = WriteMve(outputMvePath, video, options, targetBytes, steps, cancellationToken, progress);
				EnsureProducedFile(outputMvePath);
				steps.Add(new ConversionMessage(video.Audio is null ? ConversionMessageCode.WroteMveNoAudio : ConversionMessageCode.WroteMveAudio));
			}

			return new ConversionResult(outputMvePath, steps);
		}

		private static ConversionMessage WriteAviIndexed(String outputPath, IndexedVideo video, CancellationToken cancellationToken, IProgress<Double>? progress)
		{
			new AviDibWriter().Write(outputPath, video, cancellationToken, progress);
			return new ConversionMessage(ConversionMessageCode.WroteAvi8);
		}

		private static ConversionMessage WriteAviRgb24(String outputPath, IndexedVideo video, CancellationToken cancellationToken, IProgress<Double>? progress)
		{
			new AviDibWriter().WriteRgb24(outputPath, video, cancellationToken, progress);
			return new ConversionMessage(ConversionMessageCode.WroteAvi24);
		}

		private static ConversionMessage WriteWav(String outputPath, IndexedVideo video)
		{
			new WavPcmWriter().Write(outputPath, video.Audio!);
			return new ConversionMessage(ConversionMessageCode.WroteWav);
		}

		private static ConversionMessage WriteMp4H264(String outputPath, IndexedVideo video, Int32 quality, CancellationToken cancellationToken)
		{
			new SystemFfmpegMp4Writer().Write(outputPath, video, quality, cancellationToken);
			return new ConversionMessage(ConversionMessageCode.WroteMp4, quality);
		}

		/// <summary>Checks every file the conversion needs and returns the custom palette, if any.</summary>
		private static RgbColor[]? ValidateInputs(String inputVideoPath, String outputMvePath, VideoToMveOptions options)
		{
			EnsureInputFile(inputVideoPath, "input", MveErrorCode.InputFileMissing);
			if (!options.TestOnly)
			{
				EnsureOutputCanBeWritten(outputMvePath, options.Overwrite);
				if (IsSameFile(inputVideoPath, outputMvePath))
				{
					throw new IOException("The output file must be different from the input file.").Tag(MveErrorCode.OutputIsInput);
				}
			}

			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				EnsureInputFile(options.AudioSourcePath, "audio source", MveErrorCode.AudioSourceFileMissing);
			}

			if (String.IsNullOrWhiteSpace(options.PalettePath))
			{
				return null;
			}

			EnsureInputFile(options.PalettePath, "palette", MveErrorCode.PaletteFileMissing);
			return PaletteFile.Read(options.PalettePath);
		}

		/// <summary>Applies the external audio, the frame range and the audio on/off/sample-rate options.</summary>
		private static IndexedVideo ApplySelection(IndexedVideo video, VideoToMveOptions options, CancellationToken cancellationToken)
		{
			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				video = VideoEditing.WithAudio(video, VideoSourceReader.ReadExternalAudio(options.AudioSourcePath, cancellationToken));
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

		private static IndexedVideo ApplyFrameRate(IndexedVideo video, VideoToMveOptions options)
		{
			return options.FrameRate.HasValue && Math.Abs(video.FrameRate - options.FrameRate.Value) > 0.001d
				? VideoEditing.WithFrameRate(video, options.FrameRate.Value)
				: video;
		}

		/// <summary>Rescales to the requested width and/or height (each one left unchanged when not requested).</summary>
		private static IndexedVideo ApplyRequestedSize(IndexedVideo video, VideoToMveOptions options, List<ConversionMessage> steps)
		{
			Int32 width = options.Width ?? video.Width;
			Int32 height = options.Height ?? video.Height;
			if (width <= 0 || height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "The requested width and height must be positive.");
			}

			if (width == video.Width && height == video.Height)
			{
				return video;
			}

			steps.Add(new ConversionMessage(ConversionMessageCode.Rescaled, video.Width, video.Height, width, height));
			return VideoEditing.Resize(video, width, height);
		}

		private static List<ConversionMessage> DescribeSelection(ConversionMessage sourceMessage, IndexedVideo video, VideoToMveOptions options)
		{
			List<ConversionMessage> steps =
			[
				sourceMessage,
				new ConversionMessage(ConversionMessageCode.DetectedProfile, video.Width, video.Height, video.FrameRate),
				new ConversionMessage(ConversionMessageCode.FrameRange, video.Frames.Count)
			];

			steps.Add(options.IncludeAudio && video.Audio is not null
				? new ConversionMessage(ConversionMessageCode.AudioPcm, video.Audio.SampleRate, video.Audio.BitsPerSample, video.Audio.Channels)
				: new ConversionMessage(ConversionMessageCode.AudioNone));
			steps.Add(new ConversionMessage(ConversionMessageCode.CompressionProfile, options.CompressionMode));
			return steps;
		}

		/// <summary>Adds the size or bit rate request to the steps and returns the size, in bytes, the MVE file should not exceed.</summary>
		private static Int64? DescribeTarget(IndexedVideo video, VideoToMveOptions options, List<ConversionMessage> steps)
		{
			Double? limit = null;
			if (options.BitRateKilobytesPerSecond.HasValue)
			{
				Double seconds = video.Frames.Count / video.FrameRate;
				Double bytes = options.BitRateKilobytesPerSecond.Value * 1024d * seconds;
				steps.Add(new ConversionMessage(ConversionMessageCode.BitrateTarget, options.BitRateKilobytesPerSecond.Value, seconds, bytes / BytesPerMebibyte));
				limit = bytes;
			}

			if (options.SizeLimitMegabytes.HasValue)
			{
				steps.Add(new ConversionMessage(ConversionMessageCode.SizeLimit, options.SizeLimitMegabytes.Value));
				Double bytes = options.SizeLimitMegabytes.Value * BytesPerMebibyte;
				limit = limit.HasValue ? Math.Min(limit.Value, bytes) : bytes;
			}

			return limit.HasValue ? Math.Max(1, (Int64)limit.Value) : null;
		}

		/// <summary>
		/// Writes the MVE. When a size target is set, the largest difference tolerated between a block and the same block of an
		/// earlier frame is raised until the file fits. When a frame cannot be made small enough even with fewer colors per block,
		/// the video is halved in size and the writing is retried.
		/// </summary>
		private static IndexedVideo WriteMve(
			String outputMvePath,
			IndexedVideo source,
			VideoToMveOptions options,
			Int64? targetBytes,
			List<ConversionMessage> steps,
			CancellationToken cancellationToken,
			IProgress<ConversionProgress>? progress)
		{
			IndexedVideo current = source;
			while (true)
			{
				try
				{
					MveEncoderOptions encoder = new() { AllowMotion = options.CompressionMode != MveCompressionMode.Old };
					List<ConversionMessage> targetSteps = [];
					if (targetBytes.HasValue)
					{
						encoder = encoder with { Tolerance = FindTolerance(current, encoder, options.CompressionMode, targetBytes.Value, targetSteps, cancellationToken, progress) };
					}

					MveWriteReport report = new MveWriter().Write(outputMvePath, current, encoder, cancellationToken, StageProgress.For(progress, ConversionStage.EncodingMve));
					steps.AddRange(targetSteps);
					if (report.ReducedFrames > 0)
					{
						steps.Add(new ConversionMessage(ConversionMessageCode.ColorReduction, report.ReducedFrames, report.FewestColors));
					}

					return current;
				}
				catch (MveFrameTooLargeException)
				{
					IndexedVideo resized = ResizeForMvePayloadLimit(current);
					if (resized.Width == current.Width && resized.Height == current.Height)
					{
						throw;
					}

					steps.Add(new ConversionMessage(ConversionMessageCode.Downscaled, current.Width, current.Height, resized.Width, resized.Height));
					current = resized;
				}
			}
		}

		/// <summary>Finds the smallest tolerance that brings the file under the target (the largest allowed one if none does).</summary>
		private static Int32 FindTolerance(
			IndexedVideo video,
			MveEncoderOptions encoder,
			MveCompressionMode mode,
			Int64 targetBytes,
			List<ConversionMessage> steps,
			CancellationToken cancellationToken,
			IProgress<ConversionProgress>? progress)
		{
			MveWriter writer = new();
			IProgress<Double>? measureProgress = StageProgress.For(progress, ConversionStage.MeasuringSize);
			Double targetMebibytes = targetBytes / BytesPerMebibyte;

			Int64 Measure(Int32 tolerance)
			{
				return writer.Measure(video, encoder with { Tolerance = tolerance }, cancellationToken, measureProgress).BytesWritten;
			}

			Int64 exactSize = Measure(0);
			if (exactSize <= targetBytes)
			{
				steps.Add(new ConversionMessage(ConversionMessageCode.TargetMetLossless, targetMebibytes, exactSize / BytesPerMebibyte));
				return 0;
			}

			Int32 maxTolerance = mode == MveCompressionMode.HighQuality ? MaxHighQualityTolerance : MaxTolerance;
			Int64 smallest = Measure(maxTolerance);
			if (smallest > targetBytes)
			{
				// Out of reach. A larger tolerance does not always give a smaller file, so keep the best of a few trials.
				Int32 bestTolerance = smallest <= exactSize ? maxTolerance : 0;
				Int64 bestSize = Math.Min(smallest, exactSize);
				foreach (Int32 trial in new[] { maxTolerance / 4, maxTolerance / 2 })
				{
					Int64 size = Measure(trial);
					if (size < bestSize)
					{
						bestSize = size;
						bestTolerance = trial;
					}
				}

				steps.Add(new ConversionMessage(ConversionMessageCode.TargetNotReached, targetMebibytes, bestSize / BytesPerMebibyte, bestTolerance));
				return bestTolerance;
			}

			Int32 low = 0;
			Int32 high = maxTolerance;
			Int64 highSize = smallest;
			while (high - low > 1)
			{
				Int32 middle = (low + high) / 2;
				Int64 size = Measure(middle);
				if (size <= targetBytes)
				{
					high = middle;
					highSize = size;
				}
				else
				{
					low = middle;
				}
			}

			steps.Add(new ConversionMessage(ConversionMessageCode.TargetMetLossy, targetMebibytes, high, highSize / BytesPerMebibyte));
			return high;
		}

		private static IndexedVideo EnsureMveCompatibleDimensions(IndexedVideo video, List<ConversionMessage> steps)
		{
			Int32 width = VideoEditing.AlignDownToBlock(video.Width);
			Int32 height = VideoEditing.AlignDownToBlock(video.Height);
			if (width == video.Width && height == video.Height)
			{
				return video;
			}

			IndexedVideo resized = VideoEditing.Resize(video, width, height);
			steps.Add(new ConversionMessage(ConversionMessageCode.AdjustedDimensions, video.Width, video.Height, resized.Width, resized.Height));
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
				throw new ArgumentOutOfRangeException(nameof(quality), "MP4/H.264 quality must be a CRF value between 0 and 51.").Tag(MveErrorCode.Mp4QualityOutOfRange);
			}
		}

		private static void EnsureInputFile(String path, String label, MveErrorCode code)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"The {label} file does not exist.", path).Tag(code, path);
			}
		}

		private static void EnsureOutputCanBeWritten(String path, Boolean overwrite)
		{
			FileSystemHelper.EnsureParentDirectory(path);

			if (!overwrite && File.Exists(path))
			{
				throw new IOException($"Output already exists: {path}").Tag(MveErrorCode.OutputExists, path);
			}
		}

		private static void EnsureProducedFile(String path)
		{
			FileInfo file = new(path);
			if (!file.Exists || file.Length == 0)
			{
				throw new FileNotFoundException($"The expected output file was not produced: {path}", path).Tag(MveErrorCode.OutputNotProduced, path);
			}
		}

		private static Boolean IsSameFile(String first, String second)
		{
			StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return String.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
		}

		private static void TryDeleteFile(String path)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		/// <summary>Turns the 0..1 progress of a writer or reader into progress of a conversion stage.</summary>
		private sealed class StageProgress : IProgress<Double>
		{
			private readonly IProgress<ConversionProgress> target;
			private readonly ConversionStage stage;

			private StageProgress(IProgress<ConversionProgress> target, ConversionStage stage)
			{
				this.target = target;
				this.stage = stage;
			}

			public static IProgress<Double>? For(IProgress<ConversionProgress>? target, ConversionStage stage)
			{
				return target is null ? null : new StageProgress(target, stage);
			}

			public void Report(Double value)
			{
				target.Report(new ConversionProgress(stage, value));
			}
		}
	}
}