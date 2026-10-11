namespace MveTools.Inspection
{
	using System;

	public sealed record MveOpcodeInfo(Int64 Offset, UInt16 Length, Byte Type, Byte Version);
}