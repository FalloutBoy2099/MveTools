namespace MveTools.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using MveTools.Audio;
	using MveTools.Avi;
	using MveTools.Conversion;
	using MveTools.Inspection;
	using MveTools.Media;
	using MveTools.Mve;
	using Xunit;

	/// <summary>
	/// Pins the exact binary output of the writers and the pipeline behaviour of the converter
	/// so structural refactoring cannot silently change produced files.
	/// </summary>
	public sealed class CharacterizationTests : IDisposable
	{
		private readonly String workDir = Path.Combine(Path.GetTempPath(), "MveToolsTests", Guid.NewGuid().ToString("N"));

		public CharacterizationTests()
		{
			Directory.CreateDirectory(workDir);
		}

		public void Dispose()
		{
			Directory.Delete(workDir, recursive: true);
		}

		[Fact]
		public void MveWriter_output_is_stable_with_audio()
		{
			String path = Path.Combine(workDir, "a.mve");
			new MveWriter().Write(path, TestMedia.CreatePatternVideo(64, 48, 8, withAudio: true));

			Golden.Check(TestMedia.Sha256(File.ReadAllBytes(path)));
		}

		[Fact]
		public void MveWriter_output_is_stable_without_audio()
		{
			String path = Path.Combine(workDir, "b.mve");
			new MveWriter().Write(path, TestMedia.CreatePatternVideo(80, 56, 10, withAudio: false, frameRate: 12.5));

			Golden.Check(TestMedia.Sha256(File.ReadAllBytes(path)));
		}

		[Fact]
		public void MveReader_decodes_written_stream_back_to_source_frames()
		{
			IndexedVideo source = TestMedia.CreatePatternVideo(64, 48, 8, withAudio: true);
			String path = Path.Combine(workDir, "c.mve");
			new MveWriter().Write(path, source);

			IndexedVideo decoded = new MveReader().Read(path);

			Assert.Equal(source.Frames.Count, decoded.Frames.Count);
			for (Int32 i = 0; i < source.Frames.Count; i++)
			{
				Assert.Equal(source.Frames[i].Pixels, decoded.Frames[i].Pixels);
			}

			Assert.Equal(15, decoded.FrameRate, 3);
			Assert.NotNull(decoded.Audio);
			Golden.Check(TestMedia.Sha256(decoded));
		}

		[Fact]
		public void Pattern_video_exercises_every_block_encoding_mode()
		{
			String path = Path.Combine(workDir, "coverage.mve");
			new MveWriter().Write(path, TestMedia.CreatePatternVideo(64, 48, 8, withAudio: false));

			SortedSet<Int32> used = [];
			Byte[] file = File.ReadAllBytes(path);
			Int32 offset = 26;
			while (offset + 4 <= file.Length)
			{
				Int32 length = BitConverter.ToUInt16(file, offset);
				Int32 type = BitConverter.ToUInt16(file, offset + 2);
				Int32 position = offset + 4;
				while (type == 3 && position + 4 <= offset + 4 + length)
				{
					Int32 opcodeLength = BitConverter.ToUInt16(file, position);
					if (file[position + 2] == 0x0F)
					{
						for (Int32 i = 0; i < opcodeLength; i++)
						{
							_ = used.Add(file[position + 4 + i] & 0x0F);
							_ = used.Add(file[position + 4 + i] >> 4);
						}
					}

					position += 4 + opcodeLength;
				}

				offset += 4 + length;
			}

			Int32[] expected = [0x0, 0x1, 0x4, 0x5, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF];
			Assert.Empty(expected.Except(used));
		}

		[Fact]
		public void MveFileInspector_reads_generated_stream()
		{
			String path = Path.Combine(workDir, "d.mve");
			new MveWriter().Write(path, TestMedia.CreatePatternVideo(64, 48, 4, withAudio: true));

			MveFileInfo info = new MveFileInspector().Inspect(path);

			Assert.Equal(MveFileInspector.ExpectedMagicWords, info.MagicWords);
			Assert.Equal(4, info.VideoChunkCount);
			Golden.Check($"{info.Chunks.Count}/{info.AudioChunkCount}");
		}

		[Fact]
		public void AviDibWriter_output_is_stable()
		{
			IndexedVideo video = TestMedia.CreatePatternVideo(48, 40, 3, withAudio: true);
			String indexed = Path.Combine(workDir, "indexed.avi");
			String rgb = Path.Combine(workDir, "rgb.avi");
			new AviDibWriter().Write(indexed, video);
			new AviDibWriter().WriteRgb24(rgb, video);

			Golden.Check(TestMedia.Sha256(File.ReadAllBytes(indexed)), "indexed");
			Golden.Check(TestMedia.Sha256(File.ReadAllBytes(rgb)), "rgb");
		}

		[Fact]
		public void AviDibReader_roundtrips_indexed_and_quantizes_rgb()
		{
			IndexedVideo video = TestMedia.CreatePatternVideo(48, 40, 3, withAudio: true);
			String indexed = Path.Combine(workDir, "indexed.avi");
			String rgb = Path.Combine(workDir, "rgb.avi");
			new AviDibWriter().Write(indexed, video);
			new AviDibWriter().WriteRgb24(rgb, video);

			IndexedVideo fromIndexed = new AviDibReader().Read(indexed);
			IndexedVideo fromRgb = new AviDibReader().Read(rgb, new AviReadOptions { Palette = video.Palette });
			IndexedVideo dithered = new AviDibReader().Read(rgb, new AviReadOptions { Palette = video.Palette, UseFloydSteinbergDither = true });

			Assert.Equal(TestMedia.Sha256(video), TestMedia.Sha256(fromIndexed));
			Golden.Check(TestMedia.Sha256(fromRgb), "rgb");
			Golden.Check(TestMedia.Sha256(dithered), "dither");
		}

		[Fact]
		public void WavPcmWriter_output_is_stable_and_readable()
		{
			PcmAudioTrack audio = TestMedia.CreateAudio(22050, 2, 3000);
			String path = Path.Combine(workDir, "a.wav");
			new WavPcmWriter().Write(path, audio);

			PcmAudioTrack read = new WavPcmReader().Read(path);

			Assert.Equal(audio.Samples, read.Samples);
			Golden.Check(TestMedia.Sha256(File.ReadAllBytes(path)));
		}

		[Fact]
		public async Task Converter_video_to_mve_pipeline_is_stable()
		{
			String input = Path.Combine(workDir, "in.avi");
			new AviDibWriter().Write(input, TestMedia.Crop(TestMedia.CreatePatternVideo(72, 56, 12, withAudio: true), 70, 50));

			List<String> report = [];
			Dictionary<String, VideoToMveOptions> scenarios = new()
			{
				["default"] = new VideoToMveOptions(),
				["slice"] = new VideoToMveOptions { StartFrame = 2, FrameCount = 5, AudioSampleRate = 11025 },
				["end"] = new VideoToMveOptions { StartFrame = 1, EndFrame = 3, FrameRate = 10, CompressionMode = MveCompressionMode.HighQuality, BitRateKilobytesPerSecond = 300 },
				["noaudio"] = new VideoToMveOptions { IncludeAudio = false, SizeLimitMegabytes = 2.5, CompressionMode = MveCompressionMode.Old }
			};

			foreach ((String name, VideoToMveOptions scenario) in scenarios)
			{
				String output = Path.Combine(workDir, name + ".mve");
				String palettePath = Path.Combine(workDir, name + ".act");
				ConversionResult result = await new MveConverter().ConvertVideoToMveAsync(input, output, scenario with { SavePalettePath = palettePath });
				report.Add(name);
				report.AddRange(result.Steps);
				report.Add(TestMedia.Sha256(File.ReadAllBytes(output)));
				report.Add(TestMedia.Sha256(File.ReadAllBytes(palettePath)));
			}

			Golden.Check(String.Join("\n", report));
		}

		[Fact]
		public async Task Converter_test_mode_does_not_write_output()
		{
			String input = Path.Combine(workDir, "in.avi");
			new AviDibWriter().Write(input, TestMedia.CreatePatternVideo(64, 48, 3, withAudio: false));
			String output = Path.Combine(workDir, "never.mve");

			ConversionResult result = await new MveConverter().ConvertVideoToMveAsync(input, output, new VideoToMveOptions { TestOnly = true });

			Assert.False(File.Exists(output));
			Golden.Check(String.Join("\n", result.Steps));
		}

		[Fact]
		public async Task Converter_downscales_when_compressed_frame_exceeds_payload_limit()
		{
			Byte[] pixels = new Byte[256 * 256];
			UInt32 state = 99;
			for (Int32 i = 0; i < pixels.Length; i++)
			{
				state ^= state << 13;
				state ^= state >> 17;
				state ^= state << 5;
				pixels[i] = (Byte)state;
			}

			IndexedVideo noisy = new(256, 256, 15, TestMedia.CreatePalette(), [new IndexedVideoFrame(256, 256, pixels)]);
			String input = Path.Combine(workDir, "noise.avi");
			String output = Path.Combine(workDir, "noise.mve");
			new AviDibWriter().Write(input, noisy);

			ConversionResult result = await new MveConverter().ConvertVideoToMveAsync(input, output);

			Golden.Check(String.Join("\n", result.Steps) + "\n" + TestMedia.Sha256(File.ReadAllBytes(output)));
		}

		[Fact]
		public void MveWriter_reports_oversized_frames_with_a_dedicated_exception()
		{
			Byte[] pixels = new Byte[256 * 256];
			UInt32 state = 99;
			for (Int32 i = 0; i < pixels.Length; i++)
			{
				state ^= state << 13;
				state ^= state >> 17;
				state ^= state << 5;
				pixels[i] = (Byte)state;
			}

			IndexedVideo noisy = new(256, 256, 15, TestMedia.CreatePalette(), [new IndexedVideoFrame(256, 256, pixels)]);

			MveFrameTooLargeException ex = Assert.Throws<MveFrameTooLargeException>(() => new MveWriter().Write(Path.Combine(workDir, "x.mve"), noisy));
			Assert.IsAssignableFrom<NotSupportedException>(ex);
			Assert.StartsWith("Compressed MVE frame is too large", ex.Message, StringComparison.Ordinal);
			Assert.True(ex.PayloadLength > UInt16.MaxValue);
		}

		[Fact]
		public async Task Converter_mve_to_video_outputs_are_stable()
		{
			String mve = Path.Combine(workDir, "src.mve");
			new MveWriter().Write(mve, TestMedia.CreatePatternVideo(64, 48, 6, withAudio: true));

			List<String> report = [];
			foreach ((String name, VideoStandard standard) in new[] { ("avi8.avi", VideoStandard.AviIndexed8), ("avi24.avi", VideoStandard.AviRgb24), ("audio.wav", VideoStandard.WavPcm) })
			{
				String output = Path.Combine(workDir, name);
				ConversionResult result = await new MveConverter().ConvertMveToVideoAsync(mve, output, new MveToVideoOptions { Standard = standard });
				report.AddRange(result.Steps);
				report.Add(TestMedia.Sha256(File.ReadAllBytes(output)));
			}

			Golden.Check(String.Join("\n", report));
		}

		[Fact]
		public async Task Converter_rejects_invalid_requests()
		{
			String input = Path.Combine(workDir, "in.avi");
			new AviDibWriter().Write(input, TestMedia.CreatePatternVideo(64, 48, 3, withAudio: false));
			String output = Path.Combine(workDir, "out.mve");
			MveConverter converter = new();

			await Assert.ThrowsAsync<FileNotFoundException>(() => converter.ConvertVideoToMveAsync(Path.Combine(workDir, "missing.avi"), output));
			await Assert.ThrowsAsync<NotSupportedException>(() => converter.ConvertVideoToMveAsync(input, output, new VideoToMveOptions { Width = 128 }));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => converter.ConvertVideoToMveAsync(input, output, new VideoToMveOptions { StartFrame = 50 }));

			await converter.ConvertVideoToMveAsync(input, output);
			await Assert.ThrowsAsync<IOException>(() => converter.ConvertVideoToMveAsync(input, output, new VideoToMveOptions { Overwrite = false }));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => converter.ConvertMveToVideoAsync(output, Path.Combine(workDir, "o.mp4"), new MveToVideoOptions { Standard = VideoStandard.Mp4H264, Quality = 99 }));
		}
	}
}
