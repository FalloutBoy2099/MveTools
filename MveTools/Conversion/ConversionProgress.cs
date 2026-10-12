namespace MveTools.Conversion
{
	using System;

	public enum ConversionStage
	{
		/// <summary>Decoding the source MVE.</summary>
		ReadingMve,
		/// <summary>Writing the AVI, WAV or MP4 output.</summary>
		WritingVideo,
		/// <summary>Trial encodings used to reach a size or bit rate target.</summary>
		MeasuringSize,
		/// <summary>Writing the MVE output.</summary>
		EncodingMve
	}

	/// <param name="Stage">What the converter is doing.</param>
	/// <param name="Fraction">Progress of the stage, from 0 to 1.</param>
	public readonly record struct ConversionProgress(ConversionStage Stage, Double Fraction);
}