namespace MveTools.Avi
{
	using System;
	using Media;

	public sealed record AviReadOptions
	{
		/// <summary>
		/// Palette to use for the result. For 24/32 bpp sources the colors are matched to it; for 8 bpp sources the
		/// pixels are remapped from the file palette to the closest colors of this palette.
		/// </summary>
		public RgbColor[]? Palette { get; init; }

		public Boolean UseFloydSteinbergDither { get; init; }

		/// <summary>
		/// When true the frames are decoded from the file each time they are accessed instead of being loaded in memory.
		/// The file must then stay available and unchanged while the returned video is in use.
		/// </summary>
		public Boolean LazyFrames { get; init; }
	}
}