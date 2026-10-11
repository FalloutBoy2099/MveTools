namespace MveTools.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Security.Cryptography;
	using MveTools.Media;

	internal static class TestMedia
	{
		public static RgbColor[] CreatePalette()
		{
			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				palette[i] = new RgbColor((Byte)i, (Byte)(i * 3), (Byte)(255 - i));
			}

			return palette;
		}

		/// <summary>
		/// Builds a deterministic video whose blocks cover every MVE block encoding path
		/// (solid, two/four/sixteen colors, dither, quadrants, motion, repeats, raw).
		/// </summary>
		public static IndexedVideo CreatePatternVideo(Int32 width, Int32 height, Int32 frameCount, Boolean withAudio, Double frameRate = 15)
		{
			List<IndexedVideoFrame> frames = [];
			Byte[]? previous = null;
			Byte[]? secondPrevious = null;
			for (Int32 index = 0; index < frameCount; index++)
			{
				Byte[] pixels = new Byte[width * height];
				Int32 blockIndex = 0;
				for (Int32 by = 0; by < height; by += 8)
				{
					for (Int32 bx = 0; bx < width; bx += 8)
					{
						Int32 kind = (blockIndex + index * 5) % 18;
						if (index > 0 && previous is not null && kind == 14)
						{
							CopyBlock(previous, pixels, width, height, bx, by, 0, 0);
						}
						else if (index > 1 && secondPrevious is not null && kind == 15)
						{
							CopyBlock(secondPrevious, pixels, width, height, bx, by, 0, 0);
						}
						else if (index > 0 && previous is not null && kind == 16)
						{
							CopyBlock(previous, pixels, width, height, bx, by, -3, 2);
						}
						else if (index > 0 && previous is not null && kind == 17)
						{
							CopyBlock(previous, pixels, width, height, bx, by, 20, -17);
						}
						else
						{
							FillBlock(pixels, width, bx, by, kind, new Rng((UInt32)(blockIndex * 7919 + index * 104729 + 1)));
						}

						blockIndex++;
					}
				}

				frames.Add(new IndexedVideoFrame(width, height, pixels));
				secondPrevious = previous;
				previous = pixels;
			}

			PcmAudioTrack? audio = withAudio ? CreateAudio(22050, 2, (Int32)(22050 * frameCount / frameRate)) : null;
			return new IndexedVideo(width, height, frameRate, CreatePalette(), frames, audio);
		}

		public static IndexedVideo Crop(IndexedVideo video, Int32 width, Int32 height)
		{
			List<IndexedVideoFrame> frames = [];
			foreach (IndexedVideoFrame frame in video.Frames)
			{
				Byte[] pixels = new Byte[width * height];
				for (Int32 y = 0; y < height; y++)
				{
					Array.Copy(frame.Pixels, y * frame.Width, pixels, y * width, width);
				}

				frames.Add(new IndexedVideoFrame(width, height, pixels));
			}

			return new IndexedVideo(width, height, video.FrameRate, video.Palette, frames, video.Audio);
		}

		public static PcmAudioTrack CreateAudio(Int32 sampleRate, Int16 channels, Int32 sampleCount)
		{
			Rng rng = new(12345);
			Byte[] samples = new Byte[sampleCount * channels * 2];
			for (Int32 i = 0; i < sampleCount; i++)
			{
				for (Int32 c = 0; c < channels; c++)
				{
					Int32 triangle = (i * (c + 3) * 40 % 20000) - 10000;
					Int32 value = triangle + (Int32)(rng.Next() % 2000) - 1000;
					Int32 offset = (i * channels + c) * 2;
					samples[offset] = (Byte)(value & 0xFF);
					samples[offset + 1] = (Byte)((value >> 8) & 0xFF);
				}
			}

			return new PcmAudioTrack(sampleRate, channels, 16, samples);
		}

		public static String Sha256(Byte[] data)
		{
			return Convert.ToHexString(SHA256.HashData(data));
		}

		public static String Sha256(IndexedVideo video)
		{
			using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			hash.AppendData(BitConverter.GetBytes(video.Width));
			hash.AppendData(BitConverter.GetBytes(video.Height));
			hash.AppendData(BitConverter.GetBytes(video.Frames.Count));
			foreach (IndexedVideoFrame frame in video.Frames)
			{
				hash.AppendData(frame.Pixels);
			}

			foreach (RgbColor color in video.Palette)
			{
				hash.AppendData([color.R, color.G, color.B]);
			}

			if (video.Audio is not null)
			{
				hash.AppendData(BitConverter.GetBytes(video.Audio.SampleRate));
				hash.AppendData(BitConverter.GetBytes(video.Audio.Channels));
				hash.AppendData(video.Audio.Samples);
			}

			return Convert.ToHexString(hash.GetHashAndReset());
		}

		private static void CopyBlock(Byte[] source, Byte[] target, Int32 width, Int32 height, Int32 bx, Int32 by, Int32 dx, Int32 dy)
		{
			Int32 sx = Math.Clamp(bx + dx, 0, width - 8);
			Int32 sy = Math.Clamp(by + dy, 0, height - 8);
			for (Int32 y = 0; y < 8; y++)
			{
				Array.Copy(source, (sy + y) * width + sx, target, (by + y) * width + bx, 8);
			}
		}

		private static void FillBlock(Byte[] pixels, Int32 width, Int32 bx, Int32 by, Int32 kind, Rng rng)
		{
			Byte[] c = [(Byte)rng.Next(), (Byte)rng.Next(), (Byte)rng.Next(), (Byte)rng.Next()];
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Byte value = kind switch
					{
						0 => c[0],
						1 => c[(Int32)(rng.Next() & 1)],
						2 => c[(y / 4) * 2 + x / 4],
						3 => ((x ^ y) & 1) == 0 ? c[0] : c[1],
						4 => (Byte)(c[(y / 2 + x / 2) % 4] + y / 2 * 4 + x / 2),
						5 => c[(Int32)(((y / 2) * 3 + (x / 2) * 5 + rng.Next() % 2) % 4)],
						6 => c[(Int32)((y * 3 + x / 2 * 5) % 4)],
						7 => c[(Int32)((y / 2 * 3 + x * 5) % 4)],
						8 => c[(Int32)(rng.Next() % 4)],
						9 => x < 4 ? c[(Int32)(rng.Next() & 1)] : c[2 + (Int32)(rng.Next() & 1)],
						10 => y < 4 ? c[(Int32)(rng.Next() & 1)] : c[2 + (Int32)(rng.Next() & 1)],
						11 => (Byte)(c[((y / 4) * 2 + x / 4) % 4] ^ (Byte)(rng.Next() & 1)),
						12 => (Byte)(c[(y / 4) * 2 + x / 4] + (Byte)(rng.Next() % 4)),
						_ => (Byte)rng.Next()
					};
					pixels[(by + y) * width + bx + x] = value;
				}
			}
		}

		private sealed class Rng
		{
			private UInt32 state;

			public Rng(UInt32 seed)
			{
				state = seed == 0 ? 0x9E3779B9u : seed;
			}

			public UInt32 Next()
			{
				state ^= state << 13;
				state ^= state >> 17;
				state ^= state << 5;
				return state;
			}
		}
	}
}
