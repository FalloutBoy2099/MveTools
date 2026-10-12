namespace MveTools.Media
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Errors;

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
				throw new ArgumentOutOfRangeException("options", "The selected frame range is empty.").Tag(MveErrorCode.FrameRangeEmpty);
			}

			Int32 count = end - start + 1;
			PcmAudioTrack? audio = video.Audio is null ? null : AudioEditing.Slice(video.Audio, video.FrameRate, start, count);
			return new IndexedVideo(video.Width, video.Height, video.FrameRate, video.Palette, new FrameView(video.Frames, start, count, null), audio);
		}

		public static Int32 AlignDownToBlock(Int32 value)
		{
			return Math.Max(MveBlockSize, value & ~(MveBlockSize - 1));
		}

		/// <summary>Nearest-neighbour resize that keeps the palette (indexed pixels cannot be blended). Frames are resized when they are read.</summary>
		public static IndexedVideo Resize(IndexedVideo video, Int32 width, Int32 height)
		{
			FrameView frames = new(video.Frames, 0, video.Frames.Count, frame => ResizeFrame(frame, width, height));
			return new IndexedVideo(width, height, video.FrameRate, video.Palette, frames, video.Audio);
		}

		private static IndexedVideoFrame ResizeFrame(IndexedVideoFrame frame, Int32 width, Int32 height)
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

			return new IndexedVideoFrame(width, height, pixels);
		}

		/// <summary>A window on another list of frames, optionally transforming each frame when it is read (the last one is cached).</summary>
		private sealed class FrameView : IReadOnlyList<IndexedVideoFrame>
		{
			private readonly IReadOnlyList<IndexedVideoFrame> source;
			private readonly Int32 start;
			private readonly Func<IndexedVideoFrame, IndexedVideoFrame>? map;
			private Int32 cachedIndex = -1;
			private IndexedVideoFrame? cachedFrame;

			public FrameView(IReadOnlyList<IndexedVideoFrame> source, Int32 start, Int32 count, Func<IndexedVideoFrame, IndexedVideoFrame>? map)
			{
				this.source = source;
				this.start = start;
				this.map = map;
				Count = count;
			}

			public Int32 Count { get; }

			public IndexedVideoFrame this[Int32 index]
			{
				get
				{
					if ((UInt32)index >= (UInt32)Count)
					{
						throw new ArgumentOutOfRangeException(nameof(index));
					}

					if (map is null)
					{
						return source[start + index];
					}

					if (index != cachedIndex || cachedFrame is null)
					{
						cachedFrame = map(source[start + index]);
						cachedIndex = index;
					}

					return cachedFrame;
				}
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