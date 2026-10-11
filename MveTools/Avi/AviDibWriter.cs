namespace MveTools.Avi
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;
	using Media;

	public sealed class AviDibWriter
	{
		public void Write(String path, IndexedVideo video)
		{
			Write(path, video, AviPixelFormat.Indexed8);
		}

		public void WriteRgb24(String path, IndexedVideo video)
		{
			Write(path, video, AviPixelFormat.Rgb24);
		}

		private static void Write(String path, IndexedVideo video, AviPixelFormat pixelFormat)
		{
			FileSystemHelper.EnsureParentDirectory(path);

			using FileStream stream = File.Create(path);
			using BinaryWriter writer = new(stream);

			Int32 bytesPerPixel = pixelFormat == AviPixelFormat.Indexed8 ? 1 : 3;
			Int32 stride = Align4(video.Width * bytesPerPixel);
			Int32 frameSize = stride * video.Height;
			UInt32 microsecondsPerFrame = (UInt32)Math.Round(1_000_000d / video.FrameRate);
			PcmAudioTrack? audio = video.Audio?.To16Bit();

			writer.WriteAscii("RIFF");
			Int64 riffSizePosition = stream.Position;
			writer.WriteInt32LittleEndian(0);
			writer.WriteAscii("AVI ");

			Int64 hdrlSizePosition = BeginList(writer, "hdrl");
			WriteAvih(writer, microsecondsPerFrame, frameSize, video.Frames.Count, video.Width, video.Height, audio is null ? 1 : 2);
			Int64 strlSizePosition = BeginList(writer, "strl");
			WriteStrh(writer, video.FrameRate, video.Frames.Count, frameSize);
			WriteStrf(writer, video, pixelFormat);
			EndChunk(writer, strlSizePosition);
			if (audio is not null)
			{
				Int64 audioStrlSizePosition = BeginList(writer, "strl");
				WriteAudioStrh(writer, audio);
				WriteAudioStrf(writer, audio);
				EndChunk(writer, audioStrlSizePosition);
			}
			EndChunk(writer, hdrlSizePosition);

			Int64 moviListStart = stream.Position;
			Int64 moviSizePosition = BeginList(writer, "movi");
			List<(String Id, UInt32 Offset, UInt32 Size)> index = [];
			Byte[] paddedFrame = new Byte[frameSize];

			foreach (IndexedVideoFrame frame in video.Frames)
			{
				Int64 chunkStart = stream.Position;
				writer.WriteAscii("00db");
				writer.WriteInt32LittleEndian(frameSize);
				Array.Clear(paddedFrame);
				CopyTopDownToBottomUp(frame.Pixels, paddedFrame, video, stride, pixelFormat);
				writer.Write(paddedFrame);
				index.Add(("00db", (UInt32)(chunkStart - moviListStart - 8), (UInt32)frameSize));
				PadWord(writer);
			}

			if (audio is not null && audio.Samples.Length > 0)
			{
				Int64 chunkStart = stream.Position;
				writer.WriteAscii("01wb");
				writer.WriteInt32LittleEndian(audio.Samples.Length);
				writer.Write(audio.Samples);
				index.Add(("01wb", (UInt32)(chunkStart - moviListStart - 8), (UInt32)audio.Samples.Length));
				PadWord(writer);
			}

			EndChunk(writer, moviSizePosition);
			WriteIdx1(writer, index);
			EndChunk(writer, riffSizePosition);
		}

		private static void WriteAvih(BinaryWriter writer, UInt32 microsecondsPerFrame, Int32 frameSize, Int32 frameCount, Int32 width, Int32 height, Int32 streamCount)
		{
			BeginChunk(writer, "avih", out Int64 sizePosition);
			writer.WriteUInt32LittleEndian(microsecondsPerFrame);
			writer.WriteUInt32LittleEndian((UInt32)frameSize * 30);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian(0x10);
			writer.WriteUInt32LittleEndian((UInt32)frameCount);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian((UInt32)streamCount);
			writer.WriteUInt32LittleEndian((UInt32)frameSize);
			writer.WriteInt32LittleEndian(width);
			writer.WriteInt32LittleEndian(height);
			for (Int32 i = 0; i < 4; i++)
			{
				writer.WriteUInt32LittleEndian(0);
			}

			EndChunk(writer, sizePosition);
		}

		private static void WriteStrh(BinaryWriter writer, Double frameRate, Int32 frameCount, Int32 frameSize)
		{
			BeginChunk(writer, "strh", out Int64 sizePosition);
			writer.WriteAscii("vids");
			writer.WriteAscii("DIB ");
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian(1000);
			writer.WriteUInt32LittleEndian((UInt32)Math.Round(frameRate * 1000));
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian((UInt32)frameCount);
			writer.WriteUInt32LittleEndian((UInt32)frameSize);
			writer.WriteUInt32LittleEndian(UInt32.MaxValue);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			EndChunk(writer, sizePosition);
		}

		private static void WriteStrf(BinaryWriter writer, IndexedVideo video, AviPixelFormat pixelFormat)
		{
			BeginChunk(writer, "strf", out Int64 sizePosition);
			writer.WriteUInt32LittleEndian(40);
			writer.WriteInt32LittleEndian(video.Width);
			writer.WriteInt32LittleEndian(video.Height);
			writer.WriteUInt16LittleEndian(1);
			writer.WriteUInt16LittleEndian(pixelFormat == AviPixelFormat.Indexed8 ? (UInt16)8 : (UInt16)24);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian((UInt32)(Align4(video.Width * (pixelFormat == AviPixelFormat.Indexed8 ? 1 : 3)) * video.Height));
			writer.WriteInt32LittleEndian(0);
			writer.WriteInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian(pixelFormat == AviPixelFormat.Indexed8 ? (UInt32)256 : 0);
			writer.WriteUInt32LittleEndian(pixelFormat == AviPixelFormat.Indexed8 ? (UInt32)256 : 0);

			if (pixelFormat == AviPixelFormat.Indexed8)
			{
				foreach (RgbColor color in video.Palette)
				{
					writer.Write(color.B);
					writer.Write(color.G);
					writer.Write(color.R);
					writer.Write((Byte)0);
				}
			}

			EndChunk(writer, sizePosition);
		}

		private static void WriteAudioStrh(BinaryWriter writer, PcmAudioTrack audio)
		{
			BeginChunk(writer, "strh", out Int64 sizePosition);
			writer.WriteAscii("auds");
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt16LittleEndian(0);
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian((UInt32)audio.BlockAlign);
			writer.WriteUInt32LittleEndian((UInt32)(audio.SampleRate * audio.BlockAlign));
			writer.WriteUInt32LittleEndian(0);
			writer.WriteUInt32LittleEndian((UInt32)(audio.Samples.Length / audio.BlockAlign));
			writer.WriteUInt32LittleEndian((UInt32)audio.BlockAlign);
			writer.WriteUInt32LittleEndian(UInt32.MaxValue);
			writer.WriteUInt32LittleEndian((UInt32)audio.BlockAlign);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			writer.WriteInt16LittleEndian(0);
			EndChunk(writer, sizePosition);
		}

		private static void WriteAudioStrf(BinaryWriter writer, PcmAudioTrack audio)
		{
			BeginChunk(writer, "strf", out Int64 sizePosition);
			writer.WriteUInt16LittleEndian(1);
			writer.WriteUInt16LittleEndian((UInt16)audio.Channels);
			writer.WriteUInt32LittleEndian((UInt32)audio.SampleRate);
			writer.WriteUInt32LittleEndian((UInt32)(audio.SampleRate * audio.BlockAlign));
			writer.WriteUInt16LittleEndian((UInt16)audio.BlockAlign);
			writer.WriteUInt16LittleEndian((UInt16)audio.BitsPerSample);
			EndChunk(writer, sizePosition);
		}

		private static void WriteIdx1(BinaryWriter writer, List<(String Id, UInt32 Offset, UInt32 Size)> index)
		{
			BeginChunk(writer, "idx1", out Int64 sizePosition);
			foreach ((String id, UInt32 offset, UInt32 size) in index)
			{
				writer.WriteAscii(id);
				writer.WriteUInt32LittleEndian(0x10);
				writer.WriteUInt32LittleEndian(offset);
				writer.WriteUInt32LittleEndian(size);
			}

			EndChunk(writer, sizePosition);
		}

		private static Int64 BeginList(BinaryWriter writer, String listType)
		{
			writer.WriteAscii("LIST");
			Int64 sizePosition = writer.BaseStream.Position;
			writer.WriteInt32LittleEndian(0);
			writer.WriteAscii(listType);
			return sizePosition;
		}

		private static void BeginChunk(BinaryWriter writer, String id, out Int64 sizePosition)
		{
			writer.WriteAscii(id);
			sizePosition = writer.BaseStream.Position;
			writer.WriteInt32LittleEndian(0);
		}

		private static void EndChunk(BinaryWriter writer, Int64 sizePosition)
		{
			Int64 end = writer.BaseStream.Position;
			Int64 size = end - sizePosition - 4;
			writer.BaseStream.Position = sizePosition;
			writer.WriteInt32LittleEndian((Int32)size);
			writer.BaseStream.Position = end;
			PadWord(writer);
		}

		private static void PadWord(BinaryWriter writer)
		{
			if ((writer.BaseStream.Position & 1) != 0)
			{
				writer.Write((Byte)0);
			}
		}

		private static Int32 Align4(Int32 value) => (value + 3) & ~3;

		private static void CopyTopDownToBottomUp(Byte[] source, Byte[] target, IndexedVideo video, Int32 stride, AviPixelFormat pixelFormat)
		{
			for (Int32 y = 0; y < video.Height; y++)
			{
				Int32 sourceOffset = y * video.Width;
				Int32 targetOffset = (video.Height - 1 - y) * stride;
				if (pixelFormat == AviPixelFormat.Indexed8)
				{
					Buffer.BlockCopy(source, sourceOffset, target, targetOffset, video.Width);
					continue;
				}

				for (Int32 x = 0; x < video.Width; x++)
				{
					RgbColor color = video.Palette[source[sourceOffset + x]];
					Int32 rgbOffset = targetOffset + x * 3;
					target[rgbOffset] = color.B;
					target[rgbOffset + 1] = color.G;
					target[rgbOffset + 2] = color.R;
				}
			}
		}
	}
}