namespace MveTools.Conversion
{
	using System;

	public sealed record MveToVideoOptions
	{
		public VideoStandard Standard { get; init; } = VideoStandard.AviIndexed8;

		public Boolean Overwrite { get; init; } = true;

		public Int32 Quality { get; init; } = 18;
	}
}