namespace MveTools.Media
{
	using System;

	public readonly record struct RgbColor(Byte R, Byte G, Byte B)
	{
		public static RgbColor Black { get; } = new(0, 0, 0);
	}
}