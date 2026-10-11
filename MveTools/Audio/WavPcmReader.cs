namespace MveTools.Audio
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Media;

	public sealed class WavPcmReader
	{
		public PcmAudioTrack Read(String path)
		{
			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new(stream);

			if (ReadFourCc(reader) != "RIFF")
			{
				throw new InvalidDataException("The file is not a RIFF container.");
			}

			_ = reader.ReadUInt32();
			if (ReadFourCc(reader) != "WAVE")
			{
				throw new InvalidDataException("The file is not a WAVE container.");
			}

			UInt16 formatTag = 0;
			Int16 channels = 0;
			Int32 sampleRate = 0;
			Int16 bits = 0;
			List<Byte> samples = [];

			while (stream.Position + 8 <= stream.Length)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = stream.Position;
				if (id == "fmt ")
				{
					formatTag = reader.ReadUInt16();
					channels = (Int16)reader.ReadUInt16();
					sampleRate = (Int32)reader.ReadUInt32();
					_ = reader.ReadUInt32();
					_ = reader.ReadUInt16();
					bits = (Int16)reader.ReadUInt16();
				}
				else if (id == "data")
				{
					samples.AddRange(reader.ReadBytes(size));
				}

				stream.Position = AlignWord(dataOffset + size);
			}

			if (formatTag != 1)
			{
				throw new NotSupportedException($"Only PCM WAV audio is supported. Found format tag 0x{formatTag:X4}.");
			}

			return new PcmAudioTrack(sampleRate, channels, bits, [.. samples]);
		}

		private static String ReadFourCc(BinaryReader reader)
		{
			return Encoding.ASCII.GetString(reader.ReadBytes(4));
		}

		private static Int64 AlignWord(Int64 value) => (value + 1) & ~1L;
	}
}