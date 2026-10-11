namespace MveTools.IO
{
	using System;

	internal sealed class RiffChunk
	{
		public RiffChunk(String id, Int64 dataOffset, Int32 size, String? listType)
		{
			Id = id;
			DataOffset = dataOffset;
			Size = size;
			ListType = listType;
		}

		public String Id { get; }

		public Int64 DataOffset { get; }

		public Int32 Size { get; }

		public String? ListType { get; }
	}
}