namespace MveTools.Media
{
	using System;

	public sealed class IndexedVideoFrame
	{
		public IndexedVideoFrame(Int32 width, Int32 height, Byte[] pixels)
		{
			if (width <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width));
			}

			if (height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(height));
			}

			if (pixels.Length != width * height)
			{
				throw new ArgumentException("Pixel buffer size does not match frame dimensions.", nameof(pixels));
			}

			Width = width;
			Height = height;
			Pixels = pixels;
		}

		public Int32 Width { get; }

		public Int32 Height { get; }

		public Byte[] Pixels { get; }
	}
}