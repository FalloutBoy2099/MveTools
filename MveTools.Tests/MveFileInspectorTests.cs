namespace MveTools.Tests
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using MveTools.Avi;
	using MveTools.Conversion;
	using MveTools.Inspection;
	using MveTools.Media;
	using MveTools.Mve;
	using Xunit;

	public class MveFileInspectorTests
	{
		[Fact]
		public void Inspect_reads_sample_mve_structure_when_fallout_sample_exists()
		{
			String sample = @"C:\Fallout2\F2_RPU_2.4.34_old\master.dat\art\cuts\iplogo.mve";
			if (!File.Exists(sample))
			{
				return;
			}

			MveFileInfo info = new MveFileInspector().Inspect(sample);

			Assert.Equal(MveFileInspector.ExpectedMagicWords, info.MagicWords);
			Assert.NotEmpty(info.Chunks);
			Assert.True(info.VideoChunkCount > 0);
			Assert.Contains(info.Chunks, chunk => chunk.Opcodes.Count > 0);

			IndexedVideo video = new MveReader().Read(sample);
			Assert.NotNull(video.Audio);
			Assert.Equal(22050, video.Audio.SampleRate);
			Assert.Equal(2, video.Audio.Channels);
			Assert.Equal(16, video.Audio.BitsPerSample);
			Assert.True(video.Audio.Samples.Length > 0);
		}

		[Fact]
		public async Task Integrated_converter_roundtrips_raw_profile_without_external_tools()
		{
			String workDir = Path.Combine(Path.GetTempPath(), "MveToolsTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(workDir);
			try
			{
				String inputAvi = Path.Combine(workDir, "input.avi");
				String outputMve = Path.Combine(workDir, "output.mve");
				String outputAvi = Path.Combine(workDir, "output.avi");

				IndexedVideo video = CreateTestVideo();
				new AviDibWriter().Write(inputAvi, video);

				MveConverter converter = new();
				await converter.ConvertVideoToMveAsync(inputAvi, outputMve);
				await converter.ConvertMveToVideoAsync(outputMve, outputAvi);

				IndexedVideo roundtrip = new AviDibReader().Read(outputAvi);
				Assert.Equal(video.Width, roundtrip.Width);
				Assert.Equal(video.Height, roundtrip.Height);
				Assert.Equal(video.Frames[0].Pixels, roundtrip.Frames[0].Pixels);
				Assert.True(new FileInfo(outputMve).Length > 0);
			}
			finally
			{
				Directory.Delete(workDir, recursive: true);
			}
		}

		private static IndexedVideo CreateTestVideo()
		{
			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				Byte value = (Byte)i;
				palette[i] = new RgbColor(value, value, value);
			}

			Byte[] pixels = new Byte[16 * 16];
			for (Int32 y = 0; y < 16; y++)
			{
				for (Int32 x = 0; x < 16; x++)
				{
					pixels[y * 16 + x] = (Byte)(x + y * 8);
				}
			}

			return new IndexedVideo(16, 16, 15, palette, [new IndexedVideoFrame(16, 16, pixels)]);
		}
	}
}