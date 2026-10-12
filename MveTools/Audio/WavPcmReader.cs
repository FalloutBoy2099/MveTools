namespace MveTools.Audio
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Errors;
	using Media;

	public sealed class WavPcmReader
	{
		public PcmAudioTrack Read(String path)
		{
			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new(stream);

			if (ReadFourCc(reader) != "RIFF")
			{
				throw new InvalidDataException("The file is not a RIFF container.").Tag(MveErrorCode.NotRiffContainer);
			}

			_ = reader.ReadUInt32();
			if (ReadFourCc(reader) != "WAVE")
			{
				throw new InvalidDataException("The file is not a WAVE container.").Tag(MveErrorCode.NotWaveContainer);
			}

			UInt16 formatTag = 0;
			Int16 channels = 0;
			Int32 sampleRate = 0;
			Int16 bits = 0;
			using MemoryStream samples = new();

			while (stream.Position + 8 <= stream.Length)
			{
				String id = ReadFourCc(reader);
				Int32 size = reader.ReadInt32();
				Int64 dataOffset = stream.Position;
				if (size < 0)
				{
					throw new InvalidDataException($"The WAV chunk at offset {dataOffset - 4} has an invalid size.").Tag(MveErrorCode.WavChunkInvalid, dataOffset - 4);
				}

				if (id == "fmt " && size >= 16)
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
					// A data chunk that claims more than the file holds (streamed WAV files) is read up to the end of the file.
					samples.Write(reader.ReadBytes((Int32)Math.Min(size, stream.Length - dataOffset)));
				}

				stream.Position = AlignWord(dataOffset + size);
			}

			if (formatTag != 1)
			{
				throw new NotSupportedException($"Only PCM WAV audio is supported. Found format tag 0x{formatTag:X4}.").Tag(MveErrorCode.UnsupportedWavFormat, formatTag);
			}

			if (sampleRate <= 0 || channels is not 1 and not 2 || bits is not 8 and not 16)
			{
				throw new NotSupportedException($"Only 8/16-bit mono or stereo PCM WAV audio is supported. Found {bits}-bit, {channels} channel(s), {sampleRate} Hz.").Tag(MveErrorCode.WavAudioLayoutNotSupported, channels, bits, sampleRate);
			}

			return new PcmAudioTrack(sampleRate, channels, bits, samples.ToArray());
		}

		private static String ReadFourCc(BinaryReader reader)
		{
			Byte[] bytes = reader.ReadBytes(4);
			if (bytes.Length < 4)
			{
				throw new InvalidDataException("The file is not a RIFF container.").Tag(MveErrorCode.NotRiffContainer);
			}

			return Encoding.ASCII.GetString(bytes);
		}

		private static Int64 AlignWord(Int64 value) => (value + 1) & ~1L;
	}
}