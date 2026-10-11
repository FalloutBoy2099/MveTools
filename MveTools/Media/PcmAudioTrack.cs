namespace MveTools.Media
{
	using System;

	public sealed class PcmAudioTrack
	{
		public PcmAudioTrack(Int32 sampleRate, Int16 channels, Int16 bitsPerSample, Byte[] samples)
		{
			if (sampleRate <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sampleRate));
			}

			if (channels is not 1 and not 2)
			{
				throw new ArgumentOutOfRangeException(nameof(channels));
			}

			if (bitsPerSample is not 8 and not 16)
			{
				throw new ArgumentOutOfRangeException(nameof(bitsPerSample));
			}

			SampleRate = sampleRate;
			Channels = channels;
			BitsPerSample = bitsPerSample;
			Samples = samples;
		}

		public Int32 SampleRate { get; }

		public Int16 Channels { get; }

		public Int16 BitsPerSample { get; }

		public Byte[] Samples { get; }

		public Int32 BlockAlign => Channels * BitsPerSample / 8;

		public Int32 SampleCount => BlockAlign == 0 ? 0 : Samples.Length / BlockAlign;

		public PcmAudioTrack To16Bit()
		{
			if (BitsPerSample == 16)
			{
				return this;
			}

			Byte[] converted = new Byte[Samples.Length * 2];
			for (Int32 i = 0; i < Samples.Length; i++)
			{
				Int16 sample = (Int16)((Samples[i] - 128) << 8);
				converted[i * 2] = (Byte)(sample & 0xFF);
				converted[i * 2 + 1] = (Byte)((sample >> 8) & 0xFF);
			}

			return new PcmAudioTrack(SampleRate, Channels, 16, converted);
		}
	}
}