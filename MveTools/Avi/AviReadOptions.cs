namespace MveTools.Avi
{
	using System;
	using Media;

	public sealed record AviReadOptions
	{
		public RgbColor[]? Palette { get; init; }

		public Boolean UseFloydSteinbergDither { get; init; }
	}
}