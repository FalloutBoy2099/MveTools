namespace MveTools.Avi
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Media;

	public sealed class AviDibReader
	{
		public IndexedVideo Read(String path, AviReadOptions? options = null)
		{
			options ??= new AviReadOptions();
			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new(stream);

			if (ReadFourCc(reader) != "RIFF")
			{
				throw new InvalidDataException("The file is not a RIFF container.");
			}

			_ = reader.ReadUInt32();
			if (ReadFourCc(reader) != "AVI ")
			{
				throw new InvalidDataException("The file is not an AVI container.");
			}

			Int32 width = 0;
			Int32 height = 0;
			UInt16 bitCount = 0;
			RgbColor[] palette = options.Palette ?? CreateDefaultPalette();
			Double frameRate = 15;
			List<(Int64 Offset, Int32 Size)> frameChunks = [];
			List<(Int64 Offset, Int32 Size)> audioChunks = [];
			Int32 audioSampleRate = 0;
			Int16 audioChannels = 0;
			Int16 audioBits = 0;

			while (stream.Position + 8 <= stream.Length)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = stream.Position;

				if (id == "LIST")
				{
					String listType = ReadFourCc(reader);
					if (listType == "hdrl")
					{
						ReadHeaderList(reader, dataOffset + size, options.Palette is not null, ref width, ref height, ref bitCount, ref palette, ref frameRate, ref audioSampleRate, ref audioChannels, ref audioBits);
					}
					else if (listType == "movi")
					{
						ReadMoviList(reader, dataOffset + size, frameChunks, audioChunks);
					}
				}

				stream.Position = AlignWord(dataOffset + size);
			}

			if (width <= 0 || height <= 0 || bitCount == 0)
			{
				throw new InvalidDataException("The AVI stream header is incomplete.");
			}

			if (bitCount is not 8 and not 24 and not 32)
			{
				throw new NotSupportedException($"Only uncompressed 8, 24, and 32 bpp DIB AVI files are supported. Found {bitCount} bpp.");
			}

			List<IndexedVideoFrame> frames = [];
			foreach ((Int64 offset, Int32 size) in frameChunks)
			{
				stream.Position = offset;
				Byte[] data = reader.ReadBytes(size);
				frames.Add(bitCount == 8
					? DecodeIndexedFrame(data, width, height)
					: DecodeRgbFrame(data, width, height, bitCount, palette, options.UseFloydSteinbergDither));
			}

			PcmAudioTrack? audio = ReadAudio(reader, audioChunks, audioSampleRate, audioChannels, audioBits);
			return new IndexedVideo(width, height, frameRate, palette, frames, audio);
		}

		private static void ReadHeaderList(
			BinaryReader reader,
			Int64 end,
			Boolean hasCustomPalette,
			ref Int32 width,
			ref Int32 height,
			ref UInt16 bitCount,
			ref RgbColor[] palette,
			ref Double frameRate,
			ref Int32 audioSampleRate,
			ref Int16 audioChannels,
			ref Int16 audioBits)
		{
			while (reader.BaseStream.Position + 8 <= end)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = reader.BaseStream.Position;

				if (id == "LIST")
				{
					String listType = ReadFourCc(reader);
					if (listType == "strl")
					{
						ReadStreamList(reader, dataOffset + size, hasCustomPalette, ref width, ref height, ref bitCount, ref palette, ref frameRate, ref audioSampleRate, ref audioChannels, ref audioBits);
					}
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static void ReadStreamList(
			BinaryReader reader,
			Int64 end,
			Boolean hasCustomPalette,
			ref Int32 width,
			ref Int32 height,
			ref UInt16 bitCount,
			ref RgbColor[] palette,
			ref Double frameRate,
			ref Int32 audioSampleRate,
			ref Int16 audioChannels,
			ref Int16 audioBits)
		{
			String? streamType = null;
			while (reader.BaseStream.Position + 8 <= end)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = reader.BaseStream.Position;

				if (id == "strh")
				{
					streamType = ReadFourCc(reader);
					_ = ReadFourCc(reader);
					if (streamType == "vids")
					{
						reader.BaseStream.Position = dataOffset + 20;
						UInt32 scale = reader.ReadUInt32();
						UInt32 rate = reader.ReadUInt32();
						if (scale != 0)
						{
							frameRate = rate / (Double)scale;
						}
					}
				}
				else if (id == "strf" && streamType == "vids")
				{
					UInt32 headerSize = reader.ReadUInt32();
					width = reader.ReadInt32();
					height = Math.Abs(reader.ReadInt32());
					_ = reader.ReadUInt16();
					bitCount = reader.ReadUInt16();
					reader.BaseStream.Position = dataOffset + headerSize;

					if (bitCount == 8)
					{
						palette = new RgbColor[256];
						for (Int32 i = 0; i < palette.Length && reader.BaseStream.Position + 4 <= dataOffset + size; i++)
						{
							Byte b = reader.ReadByte();
							Byte g = reader.ReadByte();
							Byte r = reader.ReadByte();
							_ = reader.ReadByte();
							if (!hasCustomPalette)
							{
								palette[i] = new RgbColor(r, g, b);
							}
						}
					}
				}
				else if (id == "strf" && streamType == "auds")
				{
					UInt16 formatTag = reader.ReadUInt16();
					if (formatTag != 1)
					{
						throw new NotSupportedException($"Only PCM AVI audio is supported. Found format tag 0x{formatTag:X4}.");
					}

					audioChannels = (Int16)reader.ReadUInt16();
					audioSampleRate = (Int32)reader.ReadUInt32();
					_ = reader.ReadUInt32();
					_ = reader.ReadUInt16();
					audioBits = (Int16)reader.ReadUInt16();
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static void ReadMoviList(BinaryReader reader, Int64 end, List<(Int64 Offset, Int32 Size)> frameChunks, List<(Int64 Offset, Int32 Size)> audioChunks)
		{
			while (reader.BaseStream.Position + 8 <= end)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = reader.BaseStream.Position;
				if (id.EndsWith("db", StringComparison.Ordinal) || id.EndsWith("dc", StringComparison.Ordinal))
				{
					frameChunks.Add((dataOffset, size));
				}
				else if (id.EndsWith("wb", StringComparison.Ordinal))
				{
					audioChunks.Add((dataOffset, size));
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static PcmAudioTrack? ReadAudio(BinaryReader reader, List<(Int64 Offset, Int32 Size)> audioChunks, Int32 sampleRate, Int16 channels, Int16 bits)
		{
			if (audioChunks.Count == 0 || sampleRate <= 0 || channels <= 0 || bits <= 0)
			{
				return null;
			}

			using MemoryStream samples = new();
			foreach ((Int64 offset, Int32 size) in audioChunks)
			{
				reader.BaseStream.Position = offset;
				samples.Write(reader.ReadBytes(size));
			}

			return new PcmAudioTrack(sampleRate, channels, bits, samples.ToArray());
		}

		private static IndexedVideoFrame DecodeIndexedFrame(Byte[] data, Int32 width, Int32 height)
		{
			Int32 stride = Align4(width);
			Byte[] pixels = new Byte[width * height];
			for (Int32 y = 0; y < height; y++)
			{
				Int32 sourceOffset = (height - 1 - y) * stride;
				Buffer.BlockCopy(data, sourceOffset, pixels, y * width, width);
			}

			return new IndexedVideoFrame(width, height, pixels);
		}

		private static IndexedVideoFrame DecodeRgbFrame(Byte[] data, Int32 width, Int32 height, UInt16 bitCount, RgbColor[] palette, Boolean dither)
		{
			Int32 bytesPerPixel = bitCount / 8;
			Int32 stride = Align4(width * bytesPerPixel);
			Byte[] pixels = new Byte[width * height];
			if (dither)
			{
				return DecodeRgbFrameWithDither(data, width, height, bitCount, palette);
			}

			for (Int32 y = 0; y < height; y++)
			{
				Int32 sourceRow = (height - 1 - y) * stride;
				for (Int32 x = 0; x < width; x++)
				{
					Int32 source = sourceRow + x * bytesPerPixel;
					Byte b = data[source];
					Byte g = data[source + 1];
					Byte r = data[source + 2];
					pixels[y * width + x] = FindNearestPaletteIndex(palette, r, g, b);
				}
			}

			return new IndexedVideoFrame(width, height, pixels);
		}

		private static IndexedVideoFrame DecodeRgbFrameWithDither(Byte[] data, Int32 width, Int32 height, UInt16 bitCount, RgbColor[] palette)
		{
			Int32 bytesPerPixel = bitCount / 8;
			Int32 stride = Align4(width * bytesPerPixel);
			Double[] red = new Double[width * height];
			Double[] green = new Double[width * height];
			Double[] blue = new Double[width * height];
			for (Int32 y = 0; y < height; y++)
			{
				Int32 sourceRow = (height - 1 - y) * stride;
				for (Int32 x = 0; x < width; x++)
				{
					Int32 source = sourceRow + x * bytesPerPixel;
					Int32 offset = y * width + x;
					blue[offset] = data[source];
					green[offset] = data[source + 1];
					red[offset] = data[source + 2];
				}
			}

			Byte[] pixels = new Byte[width * height];
			for (Int32 y = 0; y < height; y++)
			{
				for (Int32 x = 0; x < width; x++)
				{
					Int32 offset = y * width + x;
					Byte index = FindNearestPaletteIndex(palette, Clamp(red[offset]), Clamp(green[offset]), Clamp(blue[offset]));
					pixels[offset] = index;
					RgbColor chosen = palette[index];
					Double er = red[offset] - chosen.R;
					Double eg = green[offset] - chosen.G;
					Double eb = blue[offset] - chosen.B;
					Diffuse(red, green, blue, width, height, x + 1, y, er, eg, eb, 7d / 16d);
					Diffuse(red, green, blue, width, height, x - 1, y + 1, er, eg, eb, 3d / 16d);
					Diffuse(red, green, blue, width, height, x, y + 1, er, eg, eb, 5d / 16d);
					Diffuse(red, green, blue, width, height, x + 1, y + 1, er, eg, eb, 1d / 16d);
				}
			}

			return new IndexedVideoFrame(width, height, pixels);
		}

		private static void Diffuse(Double[] red, Double[] green, Double[] blue, Int32 width, Int32 height, Int32 x, Int32 y, Double er, Double eg, Double eb, Double factor)
		{
			if (x < 0 || y < 0 || x >= width || y >= height)
			{
				return;
			}

			Int32 offset = y * width + x;
			red[offset] += er * factor;
			green[offset] += eg * factor;
			blue[offset] += eb * factor;
		}

		private static Byte Clamp(Double value)
		{
			return (Byte)Math.Clamp((Int32)Math.Round(value), 0, 255);
		}

		private static Byte FindNearestPaletteIndex(RgbColor[] palette, Byte r, Byte g, Byte b)
		{
			Int32 bestIndex = 0;
			Int32 bestDistance = Int32.MaxValue;
			for (Int32 i = 0; i < palette.Length; i++)
			{
				Int32 dr = palette[i].R - r;
				Int32 dg = palette[i].G - g;
				Int32 db = palette[i].B - b;
				Int32 distance = dr * dr + dg * dg + db * db;
				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestIndex = i;
				}
			}

			return (Byte)bestIndex;
		}

		private static RgbColor[] CreateDefaultPalette()
		{
			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				Byte value = (Byte)i;
				palette[i] = new RgbColor(value, value, value);
			}

			return palette;
		}

		private static String ReadFourCc(BinaryReader reader)
		{
			return Encoding.ASCII.GetString(reader.ReadBytes(4));
		}

		private static Int64 AlignWord(Int64 value) => (value + 1) & ~1L;

		private static Int32 Align4(Int32 value) => (value + 3) & ~3;
	}
}