namespace MveTools.Inspection
{
	using System;

	public enum MveChunkType : UInt16
	{
		InitializeAudio = 0x0000,
		AudioOnly = 0x0001,
		InitializeVideo = 0x0002,
		Video = 0x0003,
		Shutdown = 0x0004,
		End = 0x0005
	}
}