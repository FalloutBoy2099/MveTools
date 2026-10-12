namespace MveTools.Mve
{
	using System;

	public sealed record MveEncoderOptions
	{
		/// <summary>
		/// Largest per-channel color difference (0 to 255) accepted when a block is replaced by the block at the same place in an
		/// earlier frame. 0 keeps the video exact; higher values give smaller files.
		/// </summary>
		public Int32 Tolerance { get; init; }

		/// <summary>Search earlier frames for moved copies of a block (slower, smaller files).</summary>
		public Boolean AllowMotion { get; init; } = true;

		/// <summary>
		/// Reduce the colors of the blocks of a frame that is too large for the MVE format, instead of failing.
		/// Frames that fit are never altered by this option.
		/// </summary>
		public Boolean AllowColorReduction { get; init; } = true;
	}
}