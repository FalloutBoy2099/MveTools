namespace MveTools.Media
{
	using System;
	using System.Collections.Generic;

	public sealed class IndexedVideo
	{
		public IndexedVideo(Int32 width, Int32 height, Double frameRate, RgbColor[] palette, IReadOnlyList<IndexedVideoFrame> frames, PcmAudioTrack? audio = null)
		{
			if (palette.Length != 256)
			{
				throw new ArgumentException("Indexed videos require a 256-color palette.", nameof(palette));
			}

			if (frames.Count == 0)
			{
				throw new ArgumentException("At least one frame is required.", nameof(frames));
			}

			Width = width;
			Height = height;
			FrameRate = frameRate;
			Palette = palette;
			Frames = frames;
			Audio = audio;
		}

		public Int32 Width { get; }

		public Int32 Height { get; }

		public Double FrameRate { get; }

		public RgbColor[] Palette { get; }

		public IReadOnlyList<IndexedVideoFrame> Frames { get; }

		public PcmAudioTrack? Audio { get; }
	}
}