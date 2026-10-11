namespace MveTools.Media
{
	using System;
	using System.Collections.Generic;

	internal static class VideoEditing
	{
		public const Int32 MveBlockSize = 8;

		public static IndexedVideo WithAudio(IndexedVideo video, PcmAudioTrack? audio)
		{
			return new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, video.Frames, audio);
		}

		public static IndexedVideo WithFrameRate(IndexedVideo video, Double frameRate)
		{
			return new IndexedVideo(video.Width, video.Height, frameRate, video.Palette, video.Frames, video.Audio);
		}

		/// <summary>Keeps frames <c>[startFrame, endFrame]</c> (inclusive, optionally limited to <paramref name="frameCount"/>) and the matching audio.</summary>
		public static IndexedVideo Slice(IndexedVideo video, Int32? startFrame, Int32? endFrame, Int32? frameCount)
		{
			Int32 start = Math.Max(0, startFrame ?? 0);
			Int32 end = video.Frames.Count - 1;
			if (endFrame.HasValue)
			{
				end = Math.Min(end, endFrame.Value);
			}

			if (frameCount.HasValue)
			{
				end = Math.Min(end, start + frameCount.Value - 1);
			}

			if (start == 0 && end == video.Frames.Count - 1)
			{
				return video;
			}

			if (start > end || start >= video.Frames.Count)
			{
				// Parameter name kept for message compatibility with earlier releases.
				throw new ArgumentOutOfRangeException("options", "The selected frame range is empty.");
			}

			List<IndexedVideoFrame> frames = [];
			for (Int32 i = start; i <= end; i++)
			{
				frames.Add(video.Frames[i]);
			}

			PcmAudioTrack? audio = video.Audio is null ? null : AudioEditing.Slice(video.Audio, video.FrameRate, start, frames.Count);
			return new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, frames, audio);
		}

		public static Int32 AlignDownToBlock(Int32 value)
		{
			return Math.Max(MveBlockSize, value & ~(MveBlockSize - 1));
		}

		/// <summary>Nearest-neighbour resize that keeps the palette (indexed pixels cannot be blended).</summary>
		public static IndexedVideo Resize(IndexedVideo video, Int32 width, Int32 height)
		{
			List<IndexedVideoFrame> frames = [];
			foreach (IndexedVideoFrame frame in video.Frames)
			{
				Byte[] pixels = new Byte[width * height];
				for (Int32 y = 0; y < height; y++)
				{
					Int32 sourceY = Math.Min(frame.Height - 1, y * frame.Height / height);
					for (Int32 x = 0; x < width; x++)
					{
						Int32 sourceX = Math.Min(frame.Width - 1, x * frame.Width / width);
						pixels[y * width + x] = frame.Pixels[sourceY * frame.Width + sourceX];
					}
				}

				frames.Add(new IndexedVideoFrame(width, height, pixels));
			}

			return new IndexedVideo(width, height, video.FrameRate, video.Palette, frames, video.Audio);
		}
	}
}
