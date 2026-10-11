namespace MveTools.Mve
{
	using System;

	/// <summary>Block opcodes of the format 0x11 decoding map (one nibble per 8x8 block).</summary>
	internal static class MveBlockOpcodes
	{
		public const Byte CopyPrevious = 0x0;
		public const Byte CopySecondPrevious = 0x1;
		public const Byte MotionFromSecondPrevious = 0x2;
		public const Byte MotionFromCurrent = 0x3;
		public const Byte SmallMotionFromPrevious = 0x4;
		public const Byte MotionFromPrevious = 0x5;
		public const Byte Skip = 0x6;
		public const Byte TwoColor = 0x7;
		public const Byte TwoColorSplit = 0x8;
		public const Byte FourColor = 0x9;
		public const Byte FourColorSplit = 0xA;
		public const Byte Raw = 0xB;
		public const Byte SixteenColor = 0xC;
		public const Byte FourColorQuadrants = 0xD;
		public const Byte Solid = 0xE;
		public const Byte Dither = 0xF;
	}
}
