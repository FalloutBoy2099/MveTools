namespace MveTools.Mve
{
	using System;

	/// <summary>The compressed payload of one frame does not fit in a 16-bit MVE opcode length.</summary>
	public sealed class MveFrameTooLargeException : NotSupportedException
	{
		public MveFrameTooLargeException(Int32 payloadLength)
			: base($"Compressed MVE frame is too large ({payloadLength} bytes). More video compression modes are required for this frame.")
		{
			PayloadLength = payloadLength;
		}

		public Int32 PayloadLength { get; }
	}
}