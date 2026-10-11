namespace MveTools.Audio
{
	using System;
	using System.IO;
	using IO;
	using Media;

	public sealed class WavPcmWriter
	{
		public void Write(String path, PcmAudioTrack audio)
		{
			PcmAudioTrack pcm = audio.To16Bit();
			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using FileStream stream = File.Create(path);
			using BinaryWriter writer = new(stream);
			writer.WriteAscii("RIFF");
			writer.WriteInt32LittleEndian(36 + pcm.Samples.Length);
			writer.WriteAscii("WAVE");
			writer.WriteAscii("fmt ");
			writer.WriteInt32LittleEndian(16);
			writer.WriteUInt16LittleEndian(1);
			writer.WriteUInt16LittleEndian((UInt16)pcm.Channels);
			writer.WriteUInt32LittleEndian((UInt32)pcm.SampleRate);
			writer.WriteUInt32LittleEndian((UInt32)(pcm.SampleRate * pcm.BlockAlign));
			writer.WriteUInt16LittleEndian((UInt16)pcm.BlockAlign);
			writer.WriteUInt16LittleEndian((UInt16)pcm.BitsPerSample);
			writer.WriteAscii("data");
			writer.WriteInt32LittleEndian(pcm.Samples.Length);
			writer.Write(pcm.Samples);
		}
	}
}