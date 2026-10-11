namespace MveTools.IO
{
	using System;
	using System.IO;
	using System.Text;

	internal static class BinaryWriterExtensions
	{
		public static void WriteAscii(this BinaryWriter writer, String value)
		{
			writer.Write(Encoding.ASCII.GetBytes(value));
		}

		public static void WriteUInt16LittleEndian(this BinaryWriter writer, UInt16 value)
		{
			writer.Write(value);
		}

		public static void WriteInt16LittleEndian(this BinaryWriter writer, Int16 value)
		{
			writer.Write(value);
		}

		public static void WriteUInt32LittleEndian(this BinaryWriter writer, UInt32 value)
		{
			writer.Write(value);
		}

		public static void WriteUInt64LittleEndian(this BinaryWriter writer, UInt64 value)
		{
			writer.Write(value);
		}

		public static void WriteInt32LittleEndian(this BinaryWriter writer, Int32 value)
		{
			writer.Write(value);
		}
	}
}