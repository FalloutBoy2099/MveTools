namespace MveTools.Inspection
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public sealed record MveFileInfo(
		String Path,
		Byte[] MagicWords,
		IReadOnlyList<MveChunkInfo> Chunks)
	{
		public Int32 VideoChunkCount => Chunks.Count(chunk => chunk.Type == (UInt16)MveChunkType.Video);

		public Int32 AudioChunkCount => Chunks.Count(chunk => chunk.Type is (UInt16)MveChunkType.InitializeAudio or (UInt16)MveChunkType.AudioOnly);
	}
}