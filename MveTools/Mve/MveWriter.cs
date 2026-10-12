namespace MveTools.Mve
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using Errors;
	using IO;
	using Inspection;
	using Media;

	public sealed class MveWriter
	{
		/// <summary>Largest payload of one MVE chunk or opcode.</summary>
		private const Int32 MaxPayload = UInt16.MaxValue;

		/// <summary>Writes the video to <paramref name="path"/>; the partial file is deleted if the writing fails or is cancelled.</summary>
		public MveWriteReport Write(String path, IndexedVideo video, MveEncoderOptions? options = null, CancellationToken cancellationToken = default, IProgress<Double>? progress = null)
		{
			EnsureSupported(video);
			FileSystemHelper.EnsureParentDirectory(path);

			Boolean completed = false;
			try
			{
				MveWriteReport report;
				using (FileStream stream = File.Create(path))
				{
					report = Write(stream, video, options, cancellationToken, progress);
				}

				completed = true;
				return report;
			}
			finally
			{
				if (!completed && File.Exists(path))
				{
					try
					{
						File.Delete(path);
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}
			}
		}

		/// <summary>Encodes the video without keeping the result, to know how large the file would be.</summary>
		public MveWriteReport Measure(IndexedVideo video, MveEncoderOptions? options = null, CancellationToken cancellationToken = default, IProgress<Double>? progress = null)
		{
			EnsureSupported(video);
			return Write(new CountingStream(), video, options, cancellationToken, progress);
		}

		public MveWriteReport Write(Stream stream, IndexedVideo video, MveEncoderOptions? options = null, CancellationToken cancellationToken = default, IProgress<Double>? progress = null)
		{
			EnsureSupported(video);
			options ??= new MveEncoderOptions();

			using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
			writer.Write(MveFormat.CreateHeaderBytes());
			writer.Write(MveFormat.MagicWords);

			PcmAudioTrack? audio = video.Audio?.To16Bit();
			WriteInitVideoChunk(writer, video);
			if (audio is not null)
			{
				WriteInitAudioChunk(writer, audio);
			}

			MveFrameEncoder encoder = new(video.Palette, options.AllowMotion);
			MveEncodeLevel[] levels = CreateLevels(options);
			Byte[]? previousFrame = null;
			Byte[]? secondPreviousFrame = null;
			Int32 audioOffset = 0;
			Int32 reducedFrames = 0;
			Int32 fewestColors = 0;
			Int32 frameCount = video.Frames.Count;
			for (Int32 frameIndex = 0; frameIndex < frameCount; frameIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (audio is not null)
				{
					audioOffset = WriteAudioForFrame(writer, audio, video.FrameRate, frameIndex, audioOffset);
				}

				IndexedVideoFrame frame = video.Frames[frameIndex];
				MveEncodeLevel used = WriteVideoChunk(writer, encoder, frame, previousFrame, secondPreviousFrame, levels, out Byte[] reconstruction);
				if (used.MaxColors != 0)
				{
					reducedFrames++;
					fewestColors = fewestColors == 0 ? used.MaxColors : Math.Min(fewestColors, used.MaxColors);
				}

				secondPreviousFrame = previousFrame;
				previousFrame = reconstruction;
				progress?.Report((frameIndex + 1) / (Double)frameCount);
			}

			while (audio is not null && audioOffset < audio.Samples.Length)
			{
				Int32 size = Math.Min(audio.SampleRate * audio.BlockAlign, audio.Samples.Length - audioOffset);
				WriteCompressedAudioChunk(writer, audio, audioOffset, size);
				audioOffset += size;
			}

			WriteSimpleChunk(writer, MveChunkType.Shutdown, []);
			WriteSimpleChunk(writer, MveChunkType.End, CreateOpcode(MveOpcodes.EndOfStream, 0, []));
			writer.Flush();
			return new MveWriteReport(stream is CountingStream counting ? counting.Length : stream.CanSeek ? stream.Length : 0, reducedFrames, fewestColors);
		}

		private static void EnsureSupported(IndexedVideo video)
		{
			if (video.Width % 8 != 0 || video.Height % 8 != 0)
			{
				throw new NotSupportedException("MVE raw profile requires dimensions divisible by 8.").Tag(MveErrorCode.DimensionsNotMultipleOf8);
			}
		}

		/// <summary>The exact encoding first, then — only for a frame that does not fit — blocks reduced to 4, 2 and 1 colors.</summary>
		private static MveEncodeLevel[] CreateLevels(MveEncoderOptions options)
		{
			Int32 tolerance = Math.Max(0, options.Tolerance);
			return options.AllowColorReduction
				? [new(tolerance, 0), new(tolerance, 4), new(tolerance, 2), new(tolerance, 1)]
				: [new(tolerance, 0)];
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

			if (framePayload.Length + 16 > MaxPayload && size > audio.BlockAlign)
			{
				Int32 firstHalf = size / 2 / audio.BlockAlign * audio.BlockAlign;
				WriteCompressedAudioChunk(writer, audio, offset, firstHalf);
				WriteCompressedAudioChunk(writer, audio, offset + firstHalf, size - firstHalf);
				return;
			}

			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);
			payloadWriter.Write(CreateOpcode(MveOpcodes.AudioFrame, 0, framePayload));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.AudioOnly, payload.ToArray());
		}

		private static MveEncodeLevel WriteVideoChunk(
			BinaryWriter writer,
			MveFrameEncoder encoder,
			IndexedVideoFrame frame,
			Byte[]? previousFrame,
			Byte[]? secondPreviousFrame,
			MveEncodeLevel[] levels,
			out Byte[] reconstruction)
		{
			Byte[] decodeMap = [];
			Byte[] videoPayload = [];
			MveEncodeLevel used = levels[0];
			reconstruction = [];
			Boolean fits = false;
			foreach (MveEncodeLevel level in levels)
			{
				encoder.Encode(frame, previousFrame, secondPreviousFrame, level, out decodeMap, out videoPayload, out reconstruction);
				used = level;

				// The chunk holds the decoding map, the video data, the "send buffer" and the "end of chunk" opcodes (4 bytes of preamble each).
				if (decodeMap.Length <= MaxPayload && videoPayload.Length <= MaxPayload && 16 + decodeMap.Length + videoPayload.Length <= MaxPayload)
				{
					fits = true;
					break;
				}
			}

			if (!fits)
			{
				throw new MveFrameTooLargeException(videoPayload.Length);
			}

			using MemoryStream payload = new();
			using BinaryWriter payloadWriter = new(payload);
			payloadWriter.Write(CreateOpcode(MveOpcodes.SetDecodingMap, 0, decodeMap));
			payloadWriter.Write(CreateOpcode(MveOpcodes.VideoData11, 3, videoPayload));
			payloadWriter.Write(CreateOpcode(MveOpcodes.SendBuffer, 1, []));
			payloadWriter.Write(CreateOpcode(MveOpcodes.EndOfChunk, 0, []));

			WriteSimpleChunk(writer, MveChunkType.Video, payload.ToArray());
			return used;
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
				throw new NotSupportedException($"MVE opcode 0x{type:X2} payload is too large ({payload.Length} bytes).").Tag(MveErrorCode.OpcodePayloadTooLarge, type, payload.Length);
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
				throw new NotSupportedException($"MVE chunk 0x{(UInt16)type:X4} payload is too large ({payload.Length} bytes).").Tag(MveErrorCode.ChunkPayloadTooLarge, (UInt16)type, payload.Length);
			}

			writer.WriteUInt16LittleEndian((UInt16)payload.Length);
			writer.WriteUInt16LittleEndian((UInt16)type);
			writer.Write(payload);
		}
	}
}