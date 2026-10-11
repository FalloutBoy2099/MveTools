namespace MveTools.Conversion
{
	using System;

	public sealed record VideoToMveOptions
	{
		public Boolean Overwrite { get; init; } = true;

		public Boolean TestOnly { get; init; }

		public Int32? Width { get; init; }

		public Int32? Height { get; init; }

		public Double? FrameRate { get; init; }

		public Int32 AudioSampleRate { get; init; } = 22050;

		public Boolean IncludeAudio { get; init; } = true;

		public String? AudioSourcePath { get; init; }

		public String? PalettePath { get; init; }

		public String? SavePalettePath { get; init; }

		public Boolean UseFloydSteinbergDither { get; init; }

		public Int32? StartFrame { get; init; }

		public Int32? EndFrame { get; init; }

		public Int32? FrameCount { get; init; }

		public MveCompressionMode CompressionMode { get; init; } = MveCompressionMode.Default;

		public Int32? BitRateKilobytesPerSecond { get; init; }

		public Double? SizeLimitMegabytes { get; init; }
	}
}