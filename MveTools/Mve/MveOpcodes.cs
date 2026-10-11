namespace MveTools.Mve
{
	using System;

	internal static class MveOpcodes
	{
		public const Byte EndOfStream = 0x00;
		public const Byte EndOfChunk = 0x01;
		public const Byte CreateTimer = 0x02;
		public const Byte InitAudioBuffers = 0x03;
		public const Byte StartStopAudio = 0x04;
		public const Byte InitVideoBuffers = 0x05;
		public const Byte VideoData06 = 0x06;
		public const Byte SendBuffer = 0x07;
		public const Byte AudioFrame = 0x08;
		public const Byte SilenceFrame = 0x09;
		public const Byte InitVideoMode = 0x0A;
		public const Byte SetPalette = 0x0C;
		public const Byte SetDecodingMap = 0x0F;
		public const Byte VideoData11 = 0x11;
	}
}