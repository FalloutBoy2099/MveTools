namespace MveTools.Mve
{
	using System;
	using System.IO;

	/// <summary>A write-only stream that only counts the bytes it receives.</summary>
	internal sealed class CountingStream : Stream
	{
		private Int64 length;

		public override Boolean CanRead => false;

		public override Boolean CanSeek => false;

		public override Boolean CanWrite => true;

		public override Int64 Length => length;

		public override Int64 Position
		{
			get => length;
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
		}

		public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

		public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(Int64 value) => throw new NotSupportedException();

		public override void Write(Byte[] buffer, Int32 offset, Int32 count)
		{
			length += count;
		}

		public override void Write(ReadOnlySpan<Byte> buffer)
		{
			length += buffer.Length;
		}
	}
}