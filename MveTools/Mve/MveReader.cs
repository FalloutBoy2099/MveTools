namespace MveTools.Mve
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;
	using Inspection;
	using Media;

	public sealed class MveReader
	{
		public IndexedVideo Read(String path)
		{
			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new(stream);

			Byte[] headerBytes = reader.ReadBytes(20);
			if (!headerBytes.SequenceEqual(MveFormat.CreateHeaderBytes()))
			{
				throw new InvalidDataException("The file does not start with an Interplay MVE header.");
			}

			_ = reader.ReadBytes(6);

			Int32 width = 0;
			Int32 height = 0;
			RgbColor[] palette = PaletteFile.CreateGrayscale();
			List<IndexedVideoFrame> frames = [];
			using MemoryStream audioSamples = new();
			Byte[]? decodingMap = null;
			Byte[]? lastFrame = null;
			Byte[]? secondLastFrame = null;
			Int32 audioSampleRate = 0;
			Int16 audioChannels = 0;
			Int16 audioBits = 0;
			Boolean audioCompressed = false;
			Double frameRate = 15;

			while (stream.Position + 4 <= stream.Length)
			{
				UInt16 chunkLength = reader.ReadUInt16();
				UInt16 chunkType = reader.ReadUInt16();
				Int64 chunkEnd = stream.Position + chunkLength;

				while (stream.Position + 4 <= chunkEnd)
				{
					UInt16 opcodeLength = reader.ReadUInt16();
					Byte opcodeType = reader.ReadByte();
					Byte opcodeVersion = reader.ReadByte();
					Byte[] payload = reader.ReadBytes(opcodeLength);

					switch (opcodeType)
					{
						case MveOpcodes.CreateTimer:
							frameRate = ReadFrameRate(payload);
							break;
						case MveOpcodes.InitAudioBuffers:
							ReadAudioInit(payload, opcodeVersion, out audioSampleRate, out audioChannels, out audioBits, out audioCompressed);
							break;
						case MveOpcodes.AudioFrame:
							AppendAudioPayload(payload, audioSamples, audioChannels, audioBits, audioCompressed);
							break;
						case MveOpcodes.SilenceFrame:
							AppendSilencePayload(payload, audioSamples, audioChannels, audioBits);
							break;
						case MveOpcodes.InitVideoBuffers:
							if (payload.Length >= 4)
							{
								width = BitConverter.ToUInt16(payload, 0) * 8;
								height = BitConverter.ToUInt16(payload, 2) * 8;
							}
							break;
						case MveOpcodes.SetPalette:
							palette = ReadPalette(payload);
							break;
						case MveOpcodes.SetDecodingMap:
							decodingMap = payload;
							break;
						case MveOpcodes.VideoData06:
							if (chunkType == (UInt16)MveChunkType.Video)
							{
								IndexedVideoFrame frame = DecodeRawFrame(width, height, decodingMap, payload);
								frames.Add(frame);
								secondLastFrame = lastFrame;
								lastFrame = frame.Pixels;
							}
							break;
						case MveOpcodes.VideoData11:
							if (chunkType == (UInt16)MveChunkType.Video)
							{
								IndexedVideoFrame frame = DecodeFormat11Frame(width, height, decodingMap, payload, lastFrame, secondLastFrame);
								frames.Add(frame);
								secondLastFrame = lastFrame;
								lastFrame = frame.Pixels;
							}
							break;
					}
				}

				stream.Position = chunkEnd;
			}

			if (frames.Count == 0)
			{
				throw new NotSupportedException("No supported video frame was found.");
			}

			PcmAudioTrack? audio = audioSampleRate > 0 && audioChannels > 0 && audioSamples.Length > 0
				? new PcmAudioTrack(audioSampleRate, audioChannels, 16, audioSamples.ToArray())
				: null;

			return new IndexedVideo(width, height, frameRate, palette, frames, audio);
		}

		private static Double ReadFrameRate(Byte[] payload)
		{
			if (payload.Length != 6)
			{
				return 15;
			}

			UInt32 rate = BitConverter.ToUInt32(payload, 0);
			UInt16 subdivision = BitConverter.ToUInt16(payload, 4);
			UInt64 microseconds = (UInt64)rate * subdivision;
			return microseconds == 0 ? 15 : 1_000_000d / microseconds;
		}

		private static void ReadAudioInit(Byte[] payload, Byte version, out Int32 sampleRate, out Int16 channels, out Int16 bits, out Boolean compressed)
		{
			if (payload.Length < 6)
			{
				throw new InvalidDataException("Audio initialization payload is truncated.");
			}

			UInt16 flags = BitConverter.ToUInt16(payload, 2);
			sampleRate = BitConverter.ToUInt16(payload, 4);
			channels = (Int16)((flags & 1) + 1);
			bits = (Int16)((((flags >> 1) & 1) + 1) * 8);
			compressed = version == 1 && (flags & 0x04) != 0;
		}

		private static void AppendAudioPayload(Byte[] payload, Stream output, Int16 channels, Int16 bits, Boolean compressed)
		{
			if (channels <= 0 || bits <= 0)
			{
				return;
			}

			if (compressed)
			{
				Byte[] decoded = InterplayDpcmDecoder.DecodeToPcm16(payload, channels);
				output.Write(decoded);
				return;
			}

			if (payload.Length < 6)
			{
				throw new InvalidDataException("PCM audio payload is truncated.");
			}

			if (bits == 16)
			{
				output.Write(payload, 6, payload.Length - 6);
				return;
			}

			for (Int32 i = 6; i < payload.Length; i++)
			{
				Int16 sample = (Int16)((payload[i] - 128) << 8);
				output.WriteByte((Byte)(sample & 0xFF));
				output.WriteByte((Byte)((sample >> 8) & 0xFF));
			}
		}

		private static void AppendSilencePayload(Byte[] payload, Stream output, Int16 channels, Int16 bits)
		{
			if (channels <= 0)
			{
				return;
			}

			Int32 byteCount = payload.Length >= 4 ? BitConverter.ToInt32(payload, 0) : 0;
			if (byteCount <= 0)
			{
				return;
			}

			Int32 pcm16Bytes = bits == 8 ? byteCount * 2 : byteCount;
			output.Write(new Byte[pcm16Bytes]);
		}

		private static IndexedVideoFrame DecodeFormat11Frame(Int32 width, Int32 height, Byte[]? decodingMap, Byte[] payload, Byte[]? lastFrame, Byte[]? secondLastFrame)
		{
			if (decodingMap is null)
			{
				throw new InvalidDataException("Format 0x11 MVE frame is missing its decoding map.");
			}

			if (payload.Length < 14)
			{
				throw new InvalidDataException("Format 0x11 MVE video payload is missing its 14-byte preamble.");
			}

			Byte[] pixels = new Byte[width * height];
			using MemoryStream stream = new(payload, 14, payload.Length - 14);
			using BinaryReader reader = new(stream);
			MveBitReader bitReader = new(decodingMap);
			MveBlockDecoder blockDecoder = new(width, height, pixels, lastFrame, secondLastFrame, reader);

			for (Int32 y = 0; y < height; y += 8)
			{
				for (Int32 x = 0; x < width; x += 8)
				{
					Byte opcode = bitReader.ReadNibble();
					try
					{
						blockDecoder.DecodeBlock(opcode, x, y);
					}
					catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
					{
						throw new InvalidDataException($"Failed to decode MVE format 0x11 block ({x},{y}) opcode 0x{opcode:X}: {ex.Message}", ex);
					}
				}
			}

			return new IndexedVideoFrame(width, height, pixels);
		}

		private static IndexedVideoFrame DecodeRawFrame(Int32 width, Int32 height, Byte[]? decodingMap, Byte[] payload)
		{
			if (width <= 0 || height <= 0)
			{
				throw new InvalidDataException("Video dimensions were not initialized before video data.");
			}

			Int32 blocks = width / 8 * (height / 8);
			if (decodingMap is null || decodingMap.Length < blocks * 2)
			{
				throw new InvalidDataException("Raw MVE frame is missing its decoding map.");
			}

			Byte[] pixels = new Byte[width * height];
			Int32 sourceOffset = 0;
			Int32 mapOffset = 0;
			for (Int32 by = 0; by < height; by += 8)
			{
				for (Int32 bx = 0; bx < width; bx += 8)
				{
					UInt16 opcode = BitConverter.ToUInt16(decodingMap, mapOffset);
					mapOffset += 2;
					if (opcode != 0)
					{
						throw new NotSupportedException($"Only raw block opcode 0 is supported by the integrated decoder. Found 0x{opcode:X4}.");
					}

					for (Int32 y = 0; y < 8; y++)
					{
						if (sourceOffset + 8 > payload.Length)
						{
							throw new InvalidDataException("Raw MVE video payload is truncated.");
						}

						Buffer.BlockCopy(payload, sourceOffset, pixels, (by + y) * width + bx, 8);
						sourceOffset += 8;
					}
				}
			}

			return new IndexedVideoFrame(width, height, pixels);
		}

		private static RgbColor[] ReadPalette(Byte[] payload)
		{
			RgbColor[] palette = PaletteFile.CreateGrayscale();
			if (payload.Length < 4)
			{
				return palette;
			}

			Int32 first = BitConverter.ToUInt16(payload, 0);
			Int32 count = BitConverter.ToUInt16(payload, 2);
			Int32 offset = 4;
			for (Int32 i = 0; i < count && first + i < palette.Length && offset + 2 < payload.Length; i++)
			{
				Byte r = ExpandVga(payload[offset++]);
				Byte g = ExpandVga(payload[offset++]);
				Byte b = ExpandVga(payload[offset++]);
				palette[first + i] = new RgbColor(r, g, b);
			}

			return palette;
		}

		private static Byte ExpandVga(Byte value)
		{
			Int32 scaled = value << 2;
			scaled |= scaled >> 6 & 0x03;
			return (Byte)scaled;
		}
	}
}