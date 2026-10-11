namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Audio;
	using Avi;
	using Media;
	using Mp4;
	using Mve;

	public sealed class MveConverter
	{
		private readonly record struct SourceVideoReadResult(IndexedVideo Video, String Step);

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

		public Task<ConversionResult> ConvertVideoToMveAsync(
			String inputVideoPath,
			String outputMvePath,
			VideoToMveOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			options ??= new VideoToMveOptions();
			EnsureInputFile(inputVideoPath);
			if (!options.TestOnly)
			{
				EnsureOutputCanBeWritten(outputMvePath, options.Overwrite);
			}

			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				EnsureInputFile(options.AudioSourcePath, "audio source");
			}

			RgbColor[]? customPalette = null;
			if (!String.IsNullOrWhiteSpace(options.PalettePath))
			{
				EnsureInputFile(options.PalettePath, "palette");
				customPalette = PaletteFile.Read(options.PalettePath);
			}

			SourceVideoReadResult source = ReadSourceVideo(inputVideoPath, customPalette, options.UseFloydSteinbergDither);
			IndexedVideo video = source.Video;
			if (!String.IsNullOrWhiteSpace(options.AudioSourcePath))
			{
				video = WithAudio(video, ReadExternalAudio(options.AudioSourcePath));
			}

			video = SliceVideo(video, options);
			if (!options.IncludeAudio && video.Audio is not null)
			{
				video = new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, video.Frames);
			}
			else if (options.IncludeAudio && video.Audio is not null && video.Audio.SampleRate != options.AudioSampleRate)
			{
				video = WithAudio(video, ResampleAudio(video.Audio, options.AudioSampleRate));
			}

			if (options.Width.HasValue && video.Width != options.Width.Value)
			{
				throw new NotSupportedException("The integrated AVI reader does not rescale yet. Provide an AVI with the requested width.");
			}

			if (options.Height.HasValue && video.Height != options.Height.Value)
			{
				throw new NotSupportedException("The integrated AVI reader does not rescale yet. Provide an AVI with the requested height.");
			}

			if (options.FrameRate.HasValue && Math.Abs(video.FrameRate - options.FrameRate.Value) > 0.001d)
			{
				video = new IndexedVideo(video.Width, video.Height, options.FrameRate.Value, video.Palette, video.Frames, video.Audio);
			}

			if (!String.IsNullOrWhiteSpace(options.SavePalettePath))
			{
				PaletteFile.WriteAct(options.SavePalettePath, video.Palette);
			}

			List<String> steps =
			[
				source.Step,
				$"Detected source video profile: {video.Width}x{video.Height} at {video.FrameRate:0.###} fps",
				$"Selected frame range: {video.Frames.Count} frame(s)",
				options.IncludeAudio && video.Audio is not null
					? $"Selected audio: PCM {video.Audio.SampleRate} Hz, {video.Audio.BitsPerSample}-bit, {video.Audio.Channels} channel(s)"
					: "Selected audio: none",
				$"Compression profile: {options.CompressionMode}"
			];
			video = EnsureMveCompatibleDimensions(video, steps);

			if (options.BitRateKilobytesPerSecond.HasValue)
			{
				steps.Add($"Bitrate target requested: {options.BitRateKilobytesPerSecond.Value} KiB/s (recorded as conversion intent; integrated encoder stays lossless per block).");
			}

			if (options.SizeLimitMegabytes.HasValue)
			{
				steps.Add($"Size limit requested: {options.SizeLimitMegabytes.Value:0.###} MiB (recorded as conversion intent; integrated encoder stays lossless per block).");
			}

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

			ConversionResult result = new(
				outputMvePath,
				steps);
			return Task.FromResult(result);
		}

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
				catch (NotSupportedException ex) when (ex.Message.StartsWith("Compressed MVE frame is too large", StringComparison.Ordinal))
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

		private static IndexedVideo EnsureMveCompatibleDimensions(IndexedVideo video, List<String>? steps)
		{
			Int32 width = AlignDownTo8(video.Width);
			Int32 height = AlignDownTo8(video.Height);
			if (width == video.Width && height == video.Height)
			{
				return video;
			}

			IndexedVideo resized = ResizeVideo(video, width, height);
			steps?.Add($"Adjusted video dimensions from {video.Width}x{video.Height} to {resized.Width}x{resized.Height} so MVE blocks stay aligned to 8 pixels.");
			return resized;
		}

		private static IndexedVideo ResizeForMvePayloadLimit(IndexedVideo video)
		{
			Int32 width = AlignDownTo8(Math.Max(8, video.Width / 2));
			Int32 height = AlignDownTo8(Math.Max(8, video.Height / 2));
			if (width == video.Width && height == video.Height)
			{
				return video;
			}

			return ResizeVideo(video, width, height);
		}

		private static IndexedVideo ResizeVideo(IndexedVideo video, Int32 width, Int32 height)
		{
			List<IndexedVideoFrame> frames = [];
			foreach (IndexedVideoFrame frame in video.Frames)
			{
				Byte[] pixels = new Byte[width * height];
				for (Int32 y = 0; y < height; y++)
				{
					Int32 sourceY = Math.Min(frame.Height - 1, y * frame.Height / height);
					for (Int32 x = 0; x < width; x++)
					{
						Int32 sourceX = Math.Min(frame.Width - 1, x * frame.Width / width);
						pixels[y * width + x] = frame.Pixels[sourceY * frame.Width + sourceX];
					}
				}

				frames.Add(new IndexedVideoFrame(width, height, pixels));
			}

			return new IndexedVideo(width, height, video.FrameRate, video.Palette, frames, video.Audio);
		}

		private static Int32 AlignDownTo8(Int32 value)
		{
			return Math.Max(8, value & ~7);
		}

		private static IndexedVideo WithAudio(IndexedVideo video, PcmAudioTrack audio)
		{
			return new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, video.Frames, audio);
		}

		private static PcmAudioTrack ResampleAudio(PcmAudioTrack audio, Int32 sampleRate)
		{
			if (sampleRate <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sampleRate));
			}

			if (audio.SampleRate == sampleRate || audio.SampleCount == 0)
			{
				return audio;
			}

			Int32 targetSampleCount = Math.Max(1, (Int32)Math.Round(audio.SampleCount * sampleRate / (Double)audio.SampleRate));
			Byte[] samples = new Byte[targetSampleCount * audio.BlockAlign];
			for (Int32 targetSample = 0; targetSample < targetSampleCount; targetSample++)
			{
				Int32 sourceSample = Math.Min(audio.SampleCount - 1, (Int32)Math.Round(targetSample * audio.SampleRate / (Double)sampleRate));
				Buffer.BlockCopy(audio.Samples, sourceSample * audio.BlockAlign, samples, targetSample * audio.BlockAlign, audio.BlockAlign);
			}

			return new PcmAudioTrack(sampleRate, audio.Channels, audio.BitsPerSample, samples);
		}

		private static void EnsureMp4Quality(Int32 quality)
		{
			if (quality < 0 || quality > 51)
			{
				throw new ArgumentOutOfRangeException(nameof(quality), "MP4/H.264 quality must be a CRF value between 0 and 51.");
			}
		}

		private static PcmAudioTrack ReadExternalAudio(String path)
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

		private static SourceVideoReadResult ReadSourceVideo(String inputPath, RgbColor[]? customPalette, Boolean useFloydSteinbergDither)
		{
			AviReadOptions readOptions = new()
			{
				Palette = customPalette,
				UseFloydSteinbergDither = useFloydSteinbergDither
			};

			if (IsAvi(inputPath))
			{
				return new SourceVideoReadResult(
					new AviDibReader().Read(inputPath, readOptions),
					"Read AVI DIB and PCM audio with integrated reader");
			}

			if (!IsFfmpegVideoSource(inputPath))
			{
				throw new NotSupportedException("Video to MVE supports uncompressed AVI and FFmpeg-readable MP4/M4V/MOV sources.");
			}

			String tempDirectory = Path.Combine(Path.GetTempPath(), $"mvetools-{Guid.NewGuid():N}");
			Directory.CreateDirectory(tempDirectory);
			String tempAviPath = Path.Combine(tempDirectory, "source.avi");
			try
			{
				new SystemFfmpegVideoTranscoder().TranscodeToUncompressedAvi(inputPath, tempAviPath);
				return new SourceVideoReadResult(
					new AviDibReader().Read(tempAviPath, readOptions),
					"Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader");
			}
			finally
			{
				TryDeleteDirectory(tempDirectory);
			}
		}

		private static Boolean IsAvi(String path)
		{
			return String.Equals(Path.GetExtension(path), ".avi", StringComparison.OrdinalIgnoreCase);
		}

		private static Boolean IsFfmpegVideoSource(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			return extension is ".mp4" or ".m4v" or ".mov";
		}

		private static IndexedVideo SliceVideo(IndexedVideo video, VideoToMveOptions options)
		{
			Int32 start = Math.Max(0, options.StartFrame ?? 0);
			Int32 end = video.Frames.Count - 1;
			if (options.EndFrame.HasValue)
			{
				end = Math.Min(end, options.EndFrame.Value);
			}

			if (options.FrameCount.HasValue)
			{
				end = Math.Min(end, start + options.FrameCount.Value - 1);
			}

			if (start == 0 && end == video.Frames.Count - 1)
			{
				return video;
			}

			if (start > end || start >= video.Frames.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "The selected frame range is empty.");
			}

			List<IndexedVideoFrame> frames = [];
			for (Int32 i = start; i <= end; i++)
			{
				frames.Add(video.Frames[i]);
			}

			PcmAudioTrack? audio = video.Audio is null ? null : SliceAudio(video.Audio, video.FrameRate, start, frames.Count);
			return new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, frames, audio);
		}

		private static PcmAudioTrack SliceAudio(PcmAudioTrack audio, Double frameRate, Int32 startFrame, Int32 frameCount)
		{
			Int32 startSample = (Int32)Math.Round(startFrame * audio.SampleRate / frameRate);
			Int32 sampleCount = (Int32)Math.Round(frameCount * audio.SampleRate / frameRate);
			Int32 startByte = Math.Min(audio.Samples.Length, startSample * audio.BlockAlign);
			Int32 byteCount = Math.Min(audio.Samples.Length - startByte, sampleCount * audio.BlockAlign);
			Byte[] samples = new Byte[byteCount];
			Buffer.BlockCopy(audio.Samples, startByte, samples, 0, byteCount);
			return new PcmAudioTrack(audio.SampleRate, audio.Channels, audio.BitsPerSample, samples);
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
			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

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