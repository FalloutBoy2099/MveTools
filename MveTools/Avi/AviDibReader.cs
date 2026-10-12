namespace MveTools.Avi
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using Errors;
	using Media;

	public sealed class AviDibReader
	{
		private const Int32 MaxDimension = 65535;
		private const Int64 MaxPixels = 100_000_000;

		private readonly record struct ChunkLocation(Int64 Offset, Int32 Size);

		/// <summary>Stream properties gathered from the <c>hdrl</c> list.</summary>
		private sealed class StreamInfo
		{
			public Int32 Width { get; set; }

			public Int32 Height { get; set; }

			public UInt16 BitCount { get; set; }

			public UInt32 Compression { get; set; }

			public RgbColor[]? FilePalette { get; set; }

			public Double FrameRate { get; set; } = 15;

			public Int32 AudioSampleRate { get; set; }

			public Int16 AudioChannels { get; set; }

			public Int16 AudioBits { get; set; }
		}

		public IndexedVideo Read(String path, AviReadOptions? options = null, CancellationToken cancellationToken = default)
		{
			options ??= new AviReadOptions();
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			using BinaryReader reader = new(stream);

			StreamInfo info = new();
			List<ChunkLocation> frameChunks = [];
			List<ChunkLocation> audioChunks = [];
			try
			{
				ReadContainer(reader, info, frameChunks, audioChunks, cancellationToken);
			}
			catch (EndOfStreamException ex)
			{
				throw new InvalidDataException("The AVI file is truncated.", ex).Tag(MveErrorCode.AviChunkOutOfRange, stream.Position);
			}

			ValidateHeader(info);
			if (frameChunks.Count == 0)
			{
				throw new InvalidDataException("The AVI file contains no video frames.").Tag(MveErrorCode.AviNoFrames);
			}

			FrameDecoder decoder = new(info, options);
			AviFrameList frames = new(path, decoder, frameChunks);
			PcmAudioTrack? audio = ReadAudio(reader, audioChunks, info);
			return new IndexedVideo(
				info.Width,
				info.Height,
				info.FrameRate,
				decoder.OutputPalette,
				options.LazyFrames ? frames : frames.Materialize(cancellationToken),
				audio);
		}

		private static void ReadContainer(BinaryReader reader, StreamInfo info, List<ChunkLocation> frameChunks, List<ChunkLocation> audioChunks, CancellationToken cancellationToken)
		{
			Stream stream = reader.BaseStream;
			if (stream.Length < 12 || ReadFourCc(reader) != "RIFF")
			{
				throw new InvalidDataException("The file is not a RIFF container.").Tag(MveErrorCode.NotRiffContainer);
			}

			_ = reader.ReadUInt32();
			if (ReadFourCc(reader) != "AVI ")
			{
				throw new InvalidDataException("The file is not an AVI container.").Tag(MveErrorCode.NotAviContainer);
			}

			while (stream.Position + 8 <= stream.Length)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String id = ReadFourCc(reader);
				Int32 size = ReadChunkSize(reader);
				Int64 dataOffset = stream.Position;

				if (id == "LIST")
				{
					String listType = ReadFourCc(reader);
					Int64 end = Math.Min(dataOffset + size, stream.Length);
					if (listType == "hdrl")
					{
						ReadHeaderList(reader, end, info);
					}
					else if (listType == "movi")
					{
						ReadMoviList(reader, end, frameChunks, audioChunks, cancellationToken);
					}
				}

				stream.Position = AlignWord(dataOffset + size);
			}
		}

		private static void ValidateHeader(StreamInfo info)
		{
			if (info.Width <= 0 || info.Height <= 0 || info.BitCount == 0)
			{
				throw new InvalidDataException("The AVI stream header is incomplete.").Tag(MveErrorCode.AviHeaderIncomplete);
			}

			if (info.Width > MaxDimension || info.Height > MaxDimension || (Int64)info.Width * info.Height > MaxPixels)
			{
				throw new InvalidDataException($"The AVI frame size {info.Width}x{info.Height} is not supported.").Tag(MveErrorCode.AviInvalidDimensions, info.Width, info.Height);
			}

			if (info.BitCount is not 8 and not 24 and not 32)
			{
				throw new NotSupportedException($"Only uncompressed 8, 24, and 32 bpp DIB AVI files are supported. Found {info.BitCount} bpp.").Tag(MveErrorCode.UnsupportedBitDepth, info.BitCount);
			}

			if (info.Compression != 0)
			{
				throw new NotSupportedException($"Only uncompressed DIB AVI files are supported. Found compression 0x{info.Compression:X8}.").Tag(MveErrorCode.AviCompressionNotSupported, info.Compression);
			}
		}

		private static void ReadHeaderList(BinaryReader reader, Int64 end, StreamInfo info)
		{
			while (reader.BaseStream.Position + 8 <= end)
			{
				String id = ReadFourCc(reader);
				Int32 size = ReadChunkSize(reader);
				Int64 dataOffset = reader.BaseStream.Position;

				if (id == "LIST")
				{
					String listType = ReadFourCc(reader);
					if (listType == "strl")
					{
						ReadStreamList(reader, Math.Min(dataOffset + size, reader.BaseStream.Length), info);
					}
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static void ReadStreamList(BinaryReader reader, Int64 end, StreamInfo info)
		{
			String? streamType = null;
			while (reader.BaseStream.Position + 8 <= end)
			{
				String id = ReadFourCc(reader);
				Int32 size = ReadChunkSize(reader);
				Int64 dataOffset = reader.BaseStream.Position;

				if (id == "strh" && size >= 8)
				{
					streamType = ReadFourCc(reader);
					_ = ReadFourCc(reader);
					if (streamType == "vids" && size >= 28)
					{
						reader.BaseStream.Position = dataOffset + 20;
						UInt32 scale = reader.ReadUInt32();
						UInt32 rate = reader.ReadUInt32();
						if (scale != 0)
						{
							info.FrameRate = rate / (Double)scale;
						}
					}
				}
				else if (id == "strf" && streamType == "vids" && size >= 16)
				{
					ReadVideoFormat(reader, dataOffset, size, info);
				}
				else if (id == "strf" && streamType == "auds" && size >= 16)
				{
					ReadAudioFormat(reader, info);
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static void ReadVideoFormat(BinaryReader reader, Int64 dataOffset, Int32 size, StreamInfo info)
		{
			UInt32 headerSize = reader.ReadUInt32();
			info.Width = reader.ReadInt32();
			info.Height = Math.Abs(reader.ReadInt32());
			_ = reader.ReadUInt16();
			info.BitCount = reader.ReadUInt16();
			info.Compression = size >= 20 ? reader.ReadUInt32() : 0;
			reader.BaseStream.Position = dataOffset + headerSize;

			if (info.BitCount != 8)
			{
				return;
			}

			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length && reader.BaseStream.Position + 4 <= dataOffset + size; i++)
			{
				Byte b = reader.ReadByte();
				Byte g = reader.ReadByte();
				Byte r = reader.ReadByte();
				_ = reader.ReadByte();
				palette[i] = new RgbColor(r, g, b);
			}

			info.FilePalette = palette;
		}

		private static void ReadAudioFormat(BinaryReader reader, StreamInfo info)
		{
			UInt16 formatTag = reader.ReadUInt16();
			if (formatTag != 1)
			{
				throw new NotSupportedException($"Only PCM AVI audio is supported. Found format tag 0x{formatTag:X4}.").Tag(MveErrorCode.UnsupportedAviAudioFormat, formatTag);
			}

			info.AudioChannels = (Int16)reader.ReadUInt16();
			info.AudioSampleRate = (Int32)reader.ReadUInt32();
			_ = reader.ReadUInt32();
			_ = reader.ReadUInt16();
			info.AudioBits = (Int16)reader.ReadUInt16();
		}

		private static void ReadMoviList(BinaryReader reader, Int64 end, List<ChunkLocation> frameChunks, List<ChunkLocation> audioChunks, CancellationToken cancellationToken)
		{
			while (reader.BaseStream.Position + 8 <= end)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String id = ReadFourCc(reader);
				Int32 size = ReadChunkSize(reader);
				Int64 dataOffset = reader.BaseStream.Position;
				Boolean isVideo = id.EndsWith("db", StringComparison.Ordinal) || id.EndsWith("dc", StringComparison.Ordinal);
				Boolean isAudio = id.EndsWith("wb", StringComparison.Ordinal);
				if ((isVideo || isAudio) && dataOffset + size > reader.BaseStream.Length)
				{
					throw new InvalidDataException($"The AVI chunk at offset {dataOffset} exceeds the file length.").Tag(MveErrorCode.AviChunkOutOfRange, dataOffset);
				}

				if (isVideo)
				{
					frameChunks.Add(new ChunkLocation(dataOffset, size));
				}
				else if (isAudio)
				{
					audioChunks.Add(new ChunkLocation(dataOffset, size));
				}

				reader.BaseStream.Position = AlignWord(dataOffset + size);
			}
		}

		private static PcmAudioTrack? ReadAudio(BinaryReader reader, List<ChunkLocation> audioChunks, StreamInfo info)
		{
			if (audioChunks.Count == 0 || info.AudioSampleRate <= 0 || info.AudioChannels <= 0 || info.AudioBits <= 0)
			{
				return null;
			}

			if (info.AudioChannels is not 1 and not 2 || info.AudioBits is not 8 and not 16)
			{
				throw new NotSupportedException($"Only 8/16-bit mono or stereo PCM AVI audio is supported. Found {info.AudioBits}-bit, {info.AudioChannels} channel(s).").Tag(MveErrorCode.AviAudioLayoutNotSupported, info.AudioChannels, info.AudioBits);
			}

			using MemoryStream samples = new();
			foreach ((Int64 offset, Int32 size) in audioChunks)
			{
				reader.BaseStream.Position = offset;
				samples.Write(reader.ReadBytes(size));
			}

			return new PcmAudioTrack(info.AudioSampleRate, info.AudioChannels, info.AudioBits, samples.ToArray());
		}

		private static Int32 ReadChunkSize(BinaryReader reader)
		{
			Int32 size = reader.ReadInt32();
			if (size < 0)
			{
				throw new InvalidDataException($"The AVI chunk at offset {reader.BaseStream.Position - 4} has an invalid size.").Tag(MveErrorCode.AviChunkOutOfRange, reader.BaseStream.Position - 4);
			}

			return size;
		}

		private static String ReadFourCc(BinaryReader reader)
		{
			Byte[] bytes = reader.ReadBytes(4);
			if (bytes.Length < 4)
			{
				throw new EndOfStreamException();
			}

			return Encoding.ASCII.GetString(bytes);
		}

		private static Int64 AlignWord(Int64 value) => (value + 1) & ~1L;

		private static Int32 Align4(Int32 value) => (value + 3) & ~3;

		/// <summary>Converts the raw bytes of one frame to indexed pixels in the output palette.</summary>
		private sealed class FrameDecoder
		{
			private readonly StreamInfo info;
			private readonly Boolean dither;
			private readonly RgbColor[]? filePalette;
			private readonly Boolean remapIndexed;
			private readonly PaletteMatcher matcher;

			public FrameDecoder(StreamInfo info, AviReadOptions options)
			{
				this.info = info;
				dither = options.UseFloydSteinbergDither;
				filePalette = info.BitCount == 8 ? info.FilePalette ?? PaletteFile.CreateGrayscale() : null;
				OutputPalette = options.Palette ?? filePalette ?? PaletteFile.CreateGrayscale();
				remapIndexed = filePalette is not null && !filePalette.SequenceEqual(OutputPalette);
				matcher = new PaletteMatcher(OutputPalette);
			}

			public RgbColor[] OutputPalette { get; }

			public IndexedVideoFrame Decode(Byte[] data, Int32 frameIndex)
			{
				Int32 bytesPerPixel = info.BitCount / 8;
				Int32 stride = Align4(info.Width * bytesPerPixel);
				if (data.Length < (Int64)stride * info.Height)
				{
					throw new InvalidDataException($"The AVI frame {frameIndex} is truncated.").Tag(MveErrorCode.AviFrameTruncated, frameIndex);
				}

				if (info.BitCount != 8)
				{
					return DecodeRgb(data, bytesPerPixel, stride);
				}

				if (!remapIndexed)
				{
					return DecodeIndexed(data, stride);
				}

				return DecodeRgb(ExpandIndexedToBgr(data, stride), 3, Align4(info.Width * 3));
			}

			private IndexedVideoFrame DecodeIndexed(Byte[] data, Int32 stride)
			{
				Byte[] pixels = new Byte[info.Width * info.Height];
				for (Int32 y = 0; y < info.Height; y++)
				{
					Int32 sourceOffset = (info.Height - 1 - y) * stride;
					Buffer.BlockCopy(data, sourceOffset, pixels, y * info.Width, info.Width);
				}

				return new IndexedVideoFrame(info.Width, info.Height, pixels);
			}

			/// <summary>Expands bottom-up 8 bpp rows to bottom-up 24 bpp rows using the palette stored in the file.</summary>
			private Byte[] ExpandIndexedToBgr(Byte[] data, Int32 stride)
			{
				Int32 bgrStride = Align4(info.Width * 3);
				Byte[] bgr = new Byte[bgrStride * info.Height];
				for (Int32 row = 0; row < info.Height; row++)
				{
					for (Int32 x = 0; x < info.Width; x++)
					{
						RgbColor color = filePalette![data[row * stride + x]];
						Int32 target = row * bgrStride + x * 3;
						bgr[target] = color.B;
						bgr[target + 1] = color.G;
						bgr[target + 2] = color.R;
					}
				}

				return bgr;
			}

			private IndexedVideoFrame DecodeRgb(Byte[] data, Int32 bytesPerPixel, Int32 stride)
			{
				return dither ? DecodeRgbWithDither(data, bytesPerPixel, stride) : DecodeRgbNearest(data, bytesPerPixel, stride);
			}

			private IndexedVideoFrame DecodeRgbNearest(Byte[] data, Int32 bytesPerPixel, Int32 stride)
			{
				Byte[] pixels = new Byte[info.Width * info.Height];
				for (Int32 y = 0; y < info.Height; y++)
				{
					Int32 sourceRow = (info.Height - 1 - y) * stride;
					for (Int32 x = 0; x < info.Width; x++)
					{
						Int32 source = sourceRow + x * bytesPerPixel;
						pixels[y * info.Width + x] = matcher.FindNearest(data[source + 2], data[source + 1], data[source]);
					}
				}

				return new IndexedVideoFrame(info.Width, info.Height, pixels);
			}

			private IndexedVideoFrame DecodeRgbWithDither(Byte[] data, Int32 bytesPerPixel, Int32 stride)
			{
				Int32 width = info.Width;
				Int32 height = info.Height;
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
						Byte index = matcher.FindNearest(Clamp(red[offset]), Clamp(green[offset]), Clamp(blue[offset]));
						pixels[offset] = index;
						RgbColor chosen = OutputPalette[index];
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
		}

		/// <summary>The frames of an AVI file, decoded from the file each time one is requested (the last one is cached).</summary>
		private sealed class AviFrameList : IReadOnlyList<IndexedVideoFrame>
		{
			private readonly String path;
			private readonly FrameDecoder decoder;
			private readonly List<ChunkLocation> chunks;
			private Int32 cachedIndex = -1;
			private IndexedVideoFrame? cachedFrame;

			public AviFrameList(String path, FrameDecoder decoder, List<ChunkLocation> chunks)
			{
				this.path = path;
				this.decoder = decoder;
				this.chunks = chunks;
			}

			public Int32 Count => chunks.Count;

			public IndexedVideoFrame this[Int32 index]
			{
				get
				{
					if (index == cachedIndex && cachedFrame is not null)
					{
						return cachedFrame;
					}

					ChunkLocation location = chunks[index];
					Byte[] data = new Byte[location.Size];
					using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						if (location.Offset + location.Size > stream.Length)
						{
							throw new InvalidDataException($"The AVI frame {index} is truncated.").Tag(MveErrorCode.AviFrameTruncated, index);
						}

						stream.Position = location.Offset;
						stream.ReadExactly(data);
					}

					IndexedVideoFrame frame = decoder.Decode(data, index);
					cachedIndex = index;
					cachedFrame = frame;
					return frame;
				}
			}

			public IReadOnlyList<IndexedVideoFrame> Materialize(CancellationToken cancellationToken)
			{
				using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				List<IndexedVideoFrame> frames = new(chunks.Count);
				for (Int32 index = 0; index < chunks.Count; index++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ChunkLocation location = chunks[index];
					Byte[] data = new Byte[location.Size];
					stream.Position = location.Offset;
					stream.ReadExactly(data);
					frames.Add(decoder.Decode(data, index));
				}

				return frames;
			}

			public IEnumerator<IndexedVideoFrame> GetEnumerator()
			{
				for (Int32 index = 0; index < Count; index++)
				{
					yield return this[index];
				}
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}