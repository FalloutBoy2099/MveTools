namespace MveTools.Media
{
	using System;

	internal static class AudioEditing
	{
		/// <summary>Nearest-sample resampling, kept simple on purpose (the MVE encoder re-quantizes to DPCM anyway).</summary>
		public static PcmAudioTrack Resample(PcmAudioTrack audio, Int32 sampleRate)
		{
			if (sampleRate <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sampleRate));
			}

			if (audio.SampleRate == sampleRate || audio.SampleCount == 0)
			{
				return audio;
			}

			Int32 targetSampleCount = Math.Max(1, (Int32)Math.Round(audio.SampleCount * sampleRate / (Double)audio.SampleRate));
			Byte[] samples = new Byte[targetSampleCount * audio.BlockAlign];
			for (Int32 targetSample = 0; targetSample < targetSampleCount; targetSample++)
			{
				Int32 sourceSample = Math.Min(audio.SampleCount - 1, (Int32)Math.Round(targetSample * audio.SampleRate / (Double)sampleRate));
				Buffer.BlockCopy(audio.Samples, sourceSample * audio.BlockAlign, samples, targetSample * audio.BlockAlign, audio.BlockAlign);
			}

			return new PcmAudioTrack(sampleRate, audio.Channels, audio.BitsPerSample, samples);
		}

		/// <summary>Returns the audio that matches <paramref name="frameCount"/> video frames starting at <paramref name="startFrame"/>.</summary>
		public static PcmAudioTrack Slice(PcmAudioTrack audio, Double frameRate, Int32 startFrame, Int32 frameCount)
		{
			Int32 startSample = (Int32)Math.Round(startFrame * audio.SampleRate / frameRate);
			Int32 sampleCount = (Int32)Math.Round(frameCount * audio.SampleRate / frameRate);
			Int32 startByte = Math.Min(audio.Samples.Length, startSample * audio.BlockAlign);
			Int32 byteCount = Math.Min(audio.Samples.Length - startByte, sampleCount * audio.BlockAlign);
			Byte[] samples = new Byte[byteCount];
			Buffer.BlockCopy(audio.Samples, startByte, samples, 0, byteCount);
			return new PcmAudioTrack(audio.SampleRate, audio.Channels, audio.BitsPerSample, samples);
		}
	}
}
