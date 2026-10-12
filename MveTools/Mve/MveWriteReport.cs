namespace MveTools.Mve
{
	using System;

	/// <param name="BytesWritten">Size of the MVE data.</param>
	/// <param name="ReducedFrames">Number of frames whose blocks had to be reduced to a few colors to fit the MVE frame size limit.</param>
	/// <param name="FewestColors">The smallest number of colors per block used by any frame (0 when no frame was reduced).</param>
	public sealed record MveWriteReport(Int64 BytesWritten, Int32 ReducedFrames, Int32 FewestColors);
}