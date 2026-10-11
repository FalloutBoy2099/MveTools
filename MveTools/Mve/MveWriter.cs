namespace MveTools.Mve
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;
	using Inspection;
	using Media;

	public sealed class MveWriter
	{
		public void Write(String path, IndexedVideo video)
		{
			if (video.Width % 8 != 0 || video.Height % 8 != 0)
			{
				throw new NotSupportedException("MVE raw profile requires dimensions divisible by 8.");
			}

			FileSystemHelper.EnsureParentDirectory(path);

			using FileStream stream = File.Create(path);
			using BinaryWriter writer = new(stream);
			writer.Write(MveFormat.CreateHeaderBytes());
			writer.Write(MveFormat.MagicWords);

			PcmAudioTrack? audio = video.Audio?.To16Bit();
			WriteInitVideoChunk(writer, video);
			if (audio is not null)
			{
				WriteInitAudioChunk(writer, audio);
			}
			Byte[]? previousFrame = null;
			Byte[]? secondPreviousFrame = null;
			Int32 audioOffset = 0;
			for (Int32 frameIndex = 0; frameIndex < video.Frames.Count; frameIndex++)
			{
				if (audio is not null)
				{
					audioOffset = WriteAudioForFrame(writer, audio, video.FrameRate, frameIndex, audioOffset);
				}

				IndexedVideoFrame frame = video.Frames[frameIndex];
				WriteVideoChunk(writer, frame, previousFrame, secondPreviousFrame);
				secondPreviousFrame = previousFrame;
				previousFrame = frame.Pixels;
			}

			while (audio is not null && audioOffset < audio.Samples.Length)
			{
				Int32 size = Math.Min(audio.SampleRate * audio.BlockAlign, audio.Samples.Length - audioOffset);
				WriteCompressedAudioChunk(writer, audio, audioOffset, size);
				audioOffset += size;
			}

			WriteSimpleChunk(writer, MveChunkType.Shutdown, []);
			WriteSimpleChunk(writer, MveChunkType.End, CreateOpcode(MveOpcodes.EndOfStream, 0, []));
		}

		private static void WriteInitVideoChunk(BinaryWriter writer, IndexedVideo video)
		{
			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);

			payloadWriter.Write(CreateOpcode(MveOpcodes.CreateTimer, 0, CreateTimerPayload(video.FrameRate)));
			payloadWriter.Write(CreateOpcode(MveOpcodes.InitVideoMode, 0, CreateInitVideoModePayload(video.Width, video.Height)));
			payloadWriter.Write(CreateOpcode(MveOpcodes.InitVideoBuffers, 2, CreateInitVideoBuffersPayload(video.Width, video.Height)));
			payloadWriter.Write(CreateOpcode(MveOpcodes.SetPalette, 0, CreatePalettePayload(video.Palette)));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.InitializeVideo, payload.ToArray());
		}

		private static Byte[] CreateTimerPayload(Double frameRate)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			UInt32 microseconds = (UInt32)Math.Max(1, Math.Round(1_000_000d / frameRate));
			writer.WriteUInt32LittleEndian(microseconds);
			writer.WriteUInt16LittleEndian(1);
			return stream.ToArray();
		}

		private static void WriteInitAudioChunk(BinaryWriter writer, PcmAudioTrack audio)
		{
			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);
			payloadWriter.Write(CreateOpcode(MveOpcodes.InitAudioBuffers, 1, CreateInitAudioPayload(audio, compressed: true)));
			payloadWriter.Write(CreateOpcode(MveOpcodes.StartStopAudio, 0, []));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.InitializeAudio, payload.ToArray());
		}

		private static Int32 WriteAudioForFrame(BinaryWriter writer, PcmAudioTrack audio, Double frameRate, Int32 frameIndex, Int32 audioOffset)
		{
			Int32 targetSample = (Int32)Math.Round((frameIndex + 1) * audio.SampleRate / frameRate);
			Int32 targetOffset = Math.Min(targetSample * audio.BlockAlign, audio.Samples.Length);
			Int32 size = targetOffset - audioOffset;
			if (size > 0)
			{
				WriteCompressedAudioChunk(writer, audio, audioOffset, size);
			}

			return targetOffset;
		}

		private static void WriteCompressedAudioChunk(BinaryWriter writer, PcmAudioTrack audio, Int32 offset, Int32 size)
		{
			Byte[] framePayload = InterplayDpcmDecoder.EncodeFromPcm16(audio.Samples, offset, size, audio.Channels);
			if (framePayload.Length == 0)
			{
				return;
			}

			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);
			payloadWriter.Write(CreateOpcode(MveOpcodes.AudioFrame, 0, framePayload));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.AudioOnly, payload.ToArray());
		}

		private static void WriteVideoChunk(BinaryWriter writer, IndexedVideoFrame frame, Byte[]? previousFrame, Byte[]? secondPreviousFrame)
		{
			MveFrameEncoder.Encode(frame, previousFrame, secondPreviousFrame, out Byte[] decodeMap, out Byte[] videoPayload);

			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);
			payloadWriter.Write(CreateOpcode(MveOpcodes.SetDecodingMap, 0, decodeMap));
			payloadWriter.Write(CreateOpcode(MveOpcodes.VideoData11, 3, videoPayload));
			payloadWriter.Write(CreateOpcode(MveOpcodes.SendBuffer, 1, []));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.Video, payload.ToArray());
		}

		private static Byte[] CreateInitVideoModePayload(Int32 width, Int32 height)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			writer.WriteUInt16LittleEndian((UInt16)width);
			writer.WriteUInt16LittleEndian((UInt16)height);
			writer.WriteUInt16LittleEndian(0);
			return stream.ToArray();
		}

		private static Byte[] CreateInitAudioPayload(PcmAudioTrack audio, Boolean compressed)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			UInt16 flags = 0;
			if (audio.Channels == 2)
			{
				flags |= 0x01;
			}

			if (audio.BitsPerSample == 16)
			{
				flags |= 0x02;
			}

			if (compressed)
			{
				flags |= 0x04;
			}

			writer.WriteUInt16LittleEndian((UInt16)Math.Min(UInt16.MaxValue, audio.SampleRate));
			writer.WriteUInt16LittleEndian(flags);
			writer.WriteUInt16LittleEndian((UInt16)audio.SampleRate);
			return stream.ToArray();
		}

		private static Byte[] CreateInitVideoBuffersPayload(Int32 width, Int32 height)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			writer.WriteUInt16LittleEndian((UInt16)(width / 8));
			writer.WriteUInt16LittleEndian((UInt16)(height / 8));
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt16LittleEndian(0);
			return stream.ToArray();
		}

		private static Byte[] CreatePalettePayload(RgbColor[] palette)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt16LittleEndian(256);
			foreach (RgbColor color in palette)
			{
				writer.Write((Byte)(color.R >> 2));
				writer.Write((Byte)(color.G >> 2));
				writer.Write((Byte)(color.B >> 2));
			}

			return stream.ToArray();
		}

		private static Byte[] CreateOpcode(Byte type, Byte version, Byte[] payload)
		{
			if (payload.Length > UInt16.MaxValue)
			{
				throw new NotSupportedException($"MVE opcode 0x{type:X2} payload is too large ({payload.Length} bytes).");
			}

			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);
			writer.WriteUInt16LittleEndian((UInt16)payload.Length);
			writer.Write(type);
			writer.Write(version);
			writer.Write(payload);
			return stream.ToArray();
		}

		private static void WriteSimpleChunk(BinaryWriter writer, MveChunkType type, Byte[] payload)
		{
			if (payload.Length > UInt16.MaxValue)
			{
				throw new NotSupportedException($"MVE chunk 0x{(UInt16)type:X4} payload is too large ({payload.Length} bytes).");
			}

			writer.WriteUInt16LittleEndian((UInt16)payload.Length);
			writer.WriteUInt16LittleEndian((UInt16)type);
			writer.Write(payload);
		}
	}
}