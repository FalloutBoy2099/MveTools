namespace MveTools.Mve
{
	using System;
	using System.IO;

	internal sealed class MveBitReader
	{
		private readonly Byte[] data;
		private Int32 bitOffset;

		public MveBitReader(Byte[] data)
		{
			this.data = data;
		}

		public Int32 BitsLeft => data.Length * 8 - bitOffset;

		public Byte ReadNibble()
		{
			if (BitsLeft < 4)
			{
				throw new InvalidDataException("MVE decoding map is truncated.");
			}

			Int32 byteIndex = bitOffset / 8;
			Boolean lowNibble = (bitOffset & 4) == 0;
			bitOffset += 4;
			return lowNibble
				? (Byte)(data[byteIndex] & 0x0F)
				: (Byte)(data[byteIndex] >> 4);
		}
	}
}