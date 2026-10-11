namespace MveTools.Inspection
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;

	public sealed class MveFileInspector
	{
		public const String HeaderText = "Interplay MVE File";
		public static readonly Byte[] ExpectedMagicWords = [0x1A, 0x00, 0x00, 0x01, 0x33, 0x11];

		public MveFileInfo Inspect(String path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException("MVE file does not exist.", path);
			}

			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new(stream);

			Byte[] headerBytes = reader.ReadBytes(20);
			Byte[] expectedHeader = System.Text.Encoding.ASCII.GetBytes(HeaderText + "\x1A\0");
			if (headerBytes.Length != 20 || !headerBytes.SequenceEqual(expectedHeader))
			{
				throw new InvalidDataException("The file does not start with an Interplay MVE header.");
			}

			Byte[] magicWords = reader.ReadBytes(6);
			if (magicWords.Length != 6)
			{
				throw new InvalidDataException("The MVE header is truncated.");
			}

			List<MveChunkInfo> chunks = [];
			while (stream.Position < stream.Length)
			{
				Int64 chunkOffset = stream.Position;
				if (stream.Length - stream.Position < 4)
				{
					throw new InvalidDataException($"Truncated chunk preamble at offset {chunkOffset}.");
				}

				UInt16 length = reader.ReadUInt16();
				UInt16 type = reader.ReadUInt16();
				Int64 dataStart = stream.Position;
				Int64 dataEnd = dataStart + length;
				if (dataEnd > stream.Length)
				{
					throw new InvalidDataException($"Chunk at offset {chunkOffset} exceeds file length.");
				}

				List<MveOpcodeInfo> opcodes = [];
				while (stream.Position < dataEnd)
				{
					Int64 opcodeOffset = stream.Position;
					if (dataEnd - stream.Position < 4)
					{
						throw new InvalidDataException($"Truncated opcode preamble at offset {opcodeOffset}.");
					}

					Byte[] opcodePreamble = reader.ReadBytes(4);
					UInt16 opcodeLength = BinaryPrimitives.ReadUInt16LittleEndian(opcodePreamble.AsSpan(0, 2));
					Byte opcodeType = opcodePreamble[2];
					Byte opcodeVersion = opcodePreamble[3];
					Int64 payloadEnd = stream.Position + opcodeLength;
					if (payloadEnd > dataEnd)
					{
						throw new InvalidDataException($"Opcode at offset {opcodeOffset} exceeds its containing chunk.");
					}

					opcodes.Add(new MveOpcodeInfo(opcodeOffset, opcodeLength, opcodeType, opcodeVersion));
					stream.Position = payloadEnd;
				}

				chunks.Add(new MveChunkInfo(chunkOffset, length, type, opcodes));
			}

			return new MveFileInfo(path, magicWords, chunks);
		}
	}
}