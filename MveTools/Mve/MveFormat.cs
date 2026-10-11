namespace MveTools.Mve
{
	using System;
	using System.Text;

	internal static class MveFormat
	{
		public const String HeaderText = "Interplay MVE File";

		public static readonly Byte[] MagicWords = [0x1A, 0x00, 0x00, 0x01, 0x33, 0x11];

		public static Byte[] CreateHeaderBytes()
		{
			return Encoding.ASCII.GetBytes(HeaderText + "\x1A\0");
		}
	}
}
