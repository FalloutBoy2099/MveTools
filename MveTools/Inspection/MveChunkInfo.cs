namespace MveTools.Inspection
{
	using System;
	using System.Collections.Generic;

	public sealed record MveChunkInfo(
		Int64 Offset,
		UInt16 Length,
		UInt16 Type,
		IReadOnlyList<MveOpcodeInfo> Opcodes)
	{
		public MveChunkType? KnownType => Enum.IsDefined(typeof(MveChunkType), Type) ? (MveChunkType)Type : null;
	}
}