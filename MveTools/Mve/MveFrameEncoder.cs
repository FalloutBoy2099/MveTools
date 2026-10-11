namespace MveTools.Mve
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;
	using Media;

	/// <summary>Encodes one frame into a format 0x11 decoding map and video payload.</summary>
	internal static class MveFrameEncoder
	{
		public static void Encode(IndexedVideoFrame frame, Byte[]? previousFrame, Byte[]? secondPreviousFrame, out Byte[] decodeMap, out Byte[] videoPayload)
		{
			List<Byte> opcodes = [];
			using MemoryStream videoStream = new();
			using BinaryWriter videoWriter = new(videoStream);
			videoWriter.Write(new Byte[14]);

			for (Int32 by = 0; by < frame.Height; by += 8)
			{
				for (Int32 bx = 0; bx < frame.Width; bx += 8)
				{
					EncodeBlock(frame, previousFrame, secondPreviousFrame, bx, by, opcodes, videoWriter);
				}
			}

			decodeMap = PackNibbles(opcodes);
			videoPayload = videoStream.ToArray();
			if (videoPayload.Length > UInt16.MaxValue)
			{
				throw new MveFrameTooLargeException(videoPayload.Length);
			}
		}

		private static void EncodeBlock(IndexedVideoFrame frame, Byte[]? previousFrame, Byte[]? secondPreviousFrame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (previousFrame is not null && IsSameAsPrevious(frame, previousFrame, bx, by))
			{
				opcodes.Add(MveBlockOpcodes.CopyPrevious);
				return;
			}

			if (secondPreviousFrame is not null && IsSameAsPrevious(frame, secondPreviousFrame, bx, by))
			{
				opcodes.Add(MveBlockOpcodes.CopySecondPrevious);
				return;
			}

			if (previousFrame is not null && TryEncodeMotionFromPrevious(frame, previousFrame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryGetSolidColor(frame, bx, by, out Byte solidColor))
			{
				opcodes.Add(MveBlockOpcodes.Solid);
				writer.Write(solidColor);
				return;
			}

			if (TryEncodeFourColorBlocks(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryEncodeDither(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryGetTwoColors(frame, bx, by, out Byte color0, out Byte color1))
			{
				if (color0 > color1)
				{
					(color0, color1) = (color1, color0);
				}

				opcodes.Add(MveBlockOpcodes.TwoColor);
				writer.Write(color0);
				writer.Write(color1);
				for (Int32 y = 0; y < 8; y++)
				{
					Byte flags = 0;
					for (Int32 x = 0; x < 8; x++)
					{
						Byte value = frame.Pixels[(by + y) * frame.Width + bx + x];
						if (value == color1)
						{
							flags |= (Byte)(1 << x);
						}
					}

					writer.Write(flags);
				}

				return;
			}

			if (TryEncodeTwoColorSplit(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryEncodeFourColorScaled(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryEncodeSixteenColor(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryEncodeFourColor(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (TryEncodeFourColorSplit(frame, bx, by, opcodes, writer))
			{
				return;
			}

			opcodes.Add(MveBlockOpcodes.Raw);
			for (Int32 y = 0; y < 8; y++)
			{
				writer.Write(frame.Pixels, (by + y) * frame.Width + bx, 8);
			}
		}

		private static Boolean IsSameAsPrevious(IndexedVideoFrame frame, Byte[] previousFrame, Int32 bx, Int32 by)
		{
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Int32 offset = (by + y) * frame.Width + bx + x;
					if (frame.Pixels[offset] != previousFrame[offset])
					{
						return false;
					}
				}
			}

			return true;
		}

		private static Boolean TryEncodeMotionFromPrevious(IndexedVideoFrame frame, Byte[] previousFrame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			for (Int32 dy = -8; dy <= 7; dy++)
			{
				for (Int32 dx = -8; dx <= 7; dx++)
				{
					if (dx == 0 && dy == 0)
					{
						continue;
					}

					if (IsSameAsPreviousAt(frame, previousFrame, bx, by, dx, dy))
					{
						opcodes.Add(MveBlockOpcodes.SmallMotionFromPrevious);
						writer.Write((Byte)((dx + 8) | ((dy + 8) << 4)));
						return true;
					}
				}
			}

			for (Int32 dy = -32; dy <= 31; dy++)
			{
				for (Int32 dx = -32; dx <= 31; dx++)
				{
					if (dx >= -8 && dx <= 7 && dy >= -8 && dy <= 7)
					{
						continue;
					}

					if (IsSameAsPreviousAt(frame, previousFrame, bx, by, dx, dy))
					{
						opcodes.Add(MveBlockOpcodes.MotionFromPrevious);
						writer.Write(unchecked((Byte)(SByte)dx));
						writer.Write(unchecked((Byte)(SByte)dy));
						return true;
					}
				}
			}

			return false;
		}

		private static Boolean IsSameAsPreviousAt(IndexedVideoFrame frame, Byte[] previousFrame, Int32 bx, Int32 by, Int32 dx, Int32 dy)
		{
			Int32 sourceX = bx + dx;
			Int32 sourceY = by + dy;
			if (sourceX < 0 || sourceY < 0 || sourceX + 8 > frame.Width || sourceY + 8 > frame.Height)
			{
				return false;
			}

			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					if (frame.Pixels[(by + y) * frame.Width + bx + x] != previousFrame[(sourceY + y) * frame.Width + sourceX + x])
					{
						return false;
					}
				}
			}

			return true;
		}

		private static Boolean TryGetSolidColor(IndexedVideoFrame frame, Int32 bx, Int32 by, out Byte color)
		{
			color = frame.Pixels[by * frame.Width + bx];
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					if (frame.Pixels[(by + y) * frame.Width + bx + x] != color)
					{
						return false;
					}
				}
			}

			return true;
		}

		private static Boolean TryGetTwoColors(IndexedVideoFrame frame, Int32 bx, Int32 by, out Byte color0, out Byte color1)
		{
			color0 = frame.Pixels[by * frame.Width + bx];
			color1 = color0;
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Byte value = frame.Pixels[(by + y) * frame.Width + bx + x];
					if (value != color0 && value != color1)
					{
						if (color1 == color0)
						{
							color1 = value;
						}
						else
						{
							return false;
						}
					}
				}
			}

			return color0 != color1;
		}

		private static Boolean TryEncodeFourColorBlocks(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			Byte[] colors = new Byte[4];
			Int32 colorIndex = 0;
			for (Int32 band = 0; band < 2; band++)
			{
				Int32 y0 = by + band * 4;
				for (Int32 half = 0; half < 2; half++)
				{
					Int32 x0 = bx + half * 4;
					Byte color = frame.Pixels[y0 * frame.Width + x0];
					for (Int32 y = 0; y < 4; y++)
					{
						for (Int32 x = 0; x < 4; x++)
						{
							if (frame.Pixels[(y0 + y) * frame.Width + x0 + x] != color)
							{
								return false;
							}
						}
					}

					colors[colorIndex++] = color;
				}
			}

			opcodes.Add(MveBlockOpcodes.FourColorQuadrants);
			writer.Write(colors);
			return true;
		}

		private static Boolean TryEncodeDither(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			Byte color0 = frame.Pixels[by * frame.Width + bx];
			Byte color1 = frame.Pixels[by * frame.Width + bx + 1];
			if (color0 == color1)
			{
				return false;
			}

			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Byte expected = ((x ^ y) & 1) == 0 ? color0 : color1;
					if (frame.Pixels[(by + y) * frame.Width + bx + x] != expected)
					{
						return false;
					}
				}
			}

			opcodes.Add(MveBlockOpcodes.Dither);
			writer.Write(color0);
			writer.Write(color1);
			return true;
		}

		private static Boolean TryEncodeTwoColorSplit(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (TryEncodeTwoColorHalves(frame, bx, by, vertical: true, opcodes, writer))
			{
				return true;
			}

			if (TryEncodeTwoColorHalves(frame, bx, by, vertical: false, opcodes, writer))
			{
				return true;
			}

			Byte[][] quadrantColors = new Byte[4][];
			for (Int32 quadrant = 0; quadrant < 4; quadrant++)
			{
				Int32 qx = bx + quadrant % 2 * 4;
				Int32 qy = by + quadrant / 2 * 4;
				if (!TryCollectColors(frame, qx, qy, 4, 4, 2, out Byte[] colors))
				{
					return false;
				}

				if (colors[0] > colors[1])
				{
					(colors[0], colors[1]) = (colors[1], colors[0]);
				}

				quadrantColors[quadrant] = colors;
			}

			opcodes.Add(MveBlockOpcodes.TwoColorSplit);
			for (Int32 quadrant = 0; quadrant < 4; quadrant++)
			{
				Byte[] colors = quadrantColors[quadrant];
				Int32 qx = bx + quadrant % 2 * 4;
				Int32 qy = by + quadrant / 2 * 4;
				writer.Write(colors[0]);
				writer.Write(colors[1]);
				UInt16 flags = 0;
				for (Int32 y = 3; y >= 0; y--)
				{
					for (Int32 x = 3; x >= 0; x--)
					{
						flags <<= 1;
						if (frame.Pixels[(qy + y) * frame.Width + qx + x] == colors[1])
						{
							flags |= 1;
						}
					}
				}

				writer.WriteUInt16LittleEndian(flags);
			}

			return true;
		}

		private static Boolean TryEncodeTwoColorHalves(IndexedVideoFrame frame, Int32 bx, Int32 by, Boolean vertical, List<Byte> opcodes, BinaryWriter writer)
		{
			Int32 halfWidth = vertical ? 4 : 8;
			Int32 halfHeight = vertical ? 8 : 4;
			Byte[][] halfColors = new Byte[2][];
			for (Int32 half = 0; half < 2; half++)
			{
				Int32 hx = bx + (vertical ? half * 4 : 0);
				Int32 hy = by + (vertical ? 0 : half * 4);
				if (!TryCollectColors(frame, hx, hy, halfWidth, halfHeight, 2, out Byte[] colors))
				{
					return false;
				}

				halfColors[half] = colors;
			}

			OrderTwoColorHalf(halfColors[0], greaterFirst: true);
			OrderTwoColorHalf(halfColors[1], greaterFirst: !vertical);

			opcodes.Add(MveBlockOpcodes.TwoColorSplit);
			writer.Write(halfColors[0][0]);
			writer.Write(halfColors[0][1]);
			WriteTwoColorHalfFlags(frame, bx, by, vertical, half: 0, halfColors[0], writer);
			writer.Write(halfColors[1][0]);
			writer.Write(halfColors[1][1]);
			WriteTwoColorHalfFlags(frame, bx, by, vertical, half: 1, halfColors[1], writer);
			return true;
		}

		private static void OrderTwoColorHalf(Byte[] colors, Boolean greaterFirst)
		{
			Boolean isGreaterFirst = colors[0] > colors[1];
			if (isGreaterFirst != greaterFirst)
			{
				(colors[0], colors[1]) = (colors[1], colors[0]);
			}
		}

		private static void WriteTwoColorHalfFlags(IndexedVideoFrame frame, Int32 bx, Int32 by, Boolean vertical, Int32 half, Byte[] colors, BinaryWriter writer)
		{
			UInt32 flags = 0;
			for (Int32 i = 31; i >= 0; i--)
			{
				flags <<= 1;
				Int32 x;
				Int32 y;
				if (vertical)
				{
					x = bx + half * 4 + i % 4;
					y = by + i / 4;
				}
				else
				{
					x = bx + i % 4 + (i / 4 >= 4 ? 4 : 0);
					y = by + half * 4 + i / 4 % 4;
				}

				if (frame.Pixels[y * frame.Width + x] == colors[1])
				{
					flags |= 1;
				}
			}

			writer.WriteUInt32LittleEndian(flags);
		}

		private static Boolean TryEncodeSixteenColor(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			for (Int32 y = 0; y < 8; y += 2)
			{
				for (Int32 x = 0; x < 8; x += 2)
				{
					Byte color = frame.Pixels[(by + y) * frame.Width + bx + x];
					if (frame.Pixels[(by + y) * frame.Width + bx + x + 1] != color
						|| frame.Pixels[(by + y + 1) * frame.Width + bx + x] != color
						|| frame.Pixels[(by + y + 1) * frame.Width + bx + x + 1] != color)
					{
						return false;
					}
				}
			}

			opcodes.Add(MveBlockOpcodes.SixteenColor);
			for (Int32 y = 0; y < 8; y += 2)
			{
				for (Int32 x = 0; x < 8; x += 2)
				{
					writer.Write(frame.Pixels[(by + y) * frame.Width + bx + x]);
				}
			}

			return true;
		}

		private static Boolean TryEncodeFourColorScaled(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (TryEncodeFourColorTwoByTwo(frame, bx, by, opcodes, writer))
			{
				return true;
			}

			if (TryEncodeFourColorHorizontalPairs(frame, bx, by, opcodes, writer))
			{
				return true;
			}

			return TryEncodeFourColorVerticalPairs(frame, bx, by, opcodes, writer);
		}

		private static Boolean TryEncodeFourColorTwoByTwo(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (!TryCollectColors(frame, bx, by, 8, 8, 4, out Byte[] colors))
			{
				return false;
			}

			for (Int32 y = 0; y < 8; y += 2)
			{
				for (Int32 x = 0; x < 8; x += 2)
				{
					Byte color = frame.Pixels[(by + y) * frame.Width + bx + x];
					if (frame.Pixels[(by + y) * frame.Width + bx + x + 1] != color
						|| frame.Pixels[(by + y + 1) * frame.Width + bx + x] != color
						|| frame.Pixels[(by + y + 1) * frame.Width + bx + x + 1] != color)
					{
						return false;
					}
				}
			}

			Array.Sort(colors);
			if (colors[2] <= colors[3])
			{
				(colors[2], colors[3]) = (colors[3], colors[2]);
			}

			opcodes.Add(MveBlockOpcodes.FourColor);
			WriteFourColorTable(writer, colors);
			UInt32 flags = 0;
			for (Int32 y = 6; y >= 0; y -= 2)
			{
				for (Int32 x = 6; x >= 0; x -= 2)
				{
					flags <<= 2;
					flags |= FindColorIndex(colors, frame.Pixels[(by + y) * frame.Width + bx + x]);
				}
			}

			writer.WriteUInt32LittleEndian(flags);
			return true;
		}

		private static Boolean TryEncodeFourColorHorizontalPairs(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (!TryCollectColors(frame, bx, by, 8, 8, 4, out Byte[] colors))
			{
				return false;
			}

			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x += 2)
				{
					if (frame.Pixels[(by + y) * frame.Width + bx + x] != frame.Pixels[(by + y) * frame.Width + bx + x + 1])
					{
						return false;
					}
				}
			}

			Array.Sort(colors);
			if (colors[0] <= colors[1])
			{
				(colors[0], colors[1]) = (colors[1], colors[0]);
			}

			if (colors[2] > colors[3])
			{
				(colors[2], colors[3]) = (colors[3], colors[2]);
			}

			opcodes.Add(MveBlockOpcodes.FourColor);
			WriteFourColorTable(writer, colors);
			UInt64 flags = 0;
			for (Int32 y = 7; y >= 0; y--)
			{
				for (Int32 x = 6; x >= 0; x -= 2)
				{
					flags <<= 2;
					flags |= FindColorIndex(colors, frame.Pixels[(by + y) * frame.Width + bx + x]);
				}
			}

			writer.WriteUInt64LittleEndian(flags);
			return true;
		}

		private static Boolean TryEncodeFourColorVerticalPairs(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (!TryCollectColors(frame, bx, by, 8, 8, 4, out Byte[] colors))
			{
				return false;
			}

			for (Int32 y = 0; y < 8; y += 2)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					if (frame.Pixels[(by + y) * frame.Width + bx + x] != frame.Pixels[(by + y + 1) * frame.Width + bx + x])
					{
						return false;
					}
				}
			}

			Array.Sort(colors);
			if (colors[0] <= colors[1])
			{
				(colors[0], colors[1]) = (colors[1], colors[0]);
			}

			if (colors[2] <= colors[3])
			{
				(colors[2], colors[3]) = (colors[3], colors[2]);
			}

			opcodes.Add(MveBlockOpcodes.FourColor);
			WriteFourColorTable(writer, colors);
			UInt64 flags = 0;
			for (Int32 y = 6; y >= 0; y -= 2)
			{
				for (Int32 x = 7; x >= 0; x--)
				{
					flags <<= 2;
					flags |= FindColorIndex(colors, frame.Pixels[(by + y) * frame.Width + bx + x]);
				}
			}

			writer.WriteUInt64LittleEndian(flags);
			return true;
		}

		private static Boolean TryEncodeFourColor(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			if (!TryCollectColors(frame, bx, by, 8, 8, 4, out Byte[] colors))
			{
				return false;
			}

			Array.Sort(colors);
			opcodes.Add(MveBlockOpcodes.FourColor);
			WriteFourColorTable(writer, colors);
			for (Int32 y = 0; y < 8; y++)
			{
				UInt16 flags = 0;
				for (Int32 x = 7; x >= 0; x--)
				{
					flags <<= 2;
					flags |= (UInt16)FindColorIndex(colors, frame.Pixels[(by + y) * frame.Width + bx + x]);
				}

				writer.WriteUInt16LittleEndian(flags);
			}

			return true;
		}

		private static Boolean TryEncodeFourColorSplit(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			Byte[][] quadrantColors = new Byte[4][];
			for (Int32 quadrant = 0; quadrant < 4; quadrant++)
			{
				Int32 qx = bx + quadrant % 2 * 4;
				Int32 qy = by + quadrant / 2 * 4;
				if (!TryCollectColors(frame, qx, qy, 4, 4, 4, out Byte[] colors))
				{
					return false;
				}

				Array.Sort(colors);
				quadrantColors[quadrant] = colors;
			}

			opcodes.Add(MveBlockOpcodes.FourColorSplit);
			for (Int32 quadrant = 0; quadrant < 4; quadrant++)
			{
				Byte[] colors = quadrantColors[quadrant];
				Int32 qx = bx + quadrant % 2 * 4;
				Int32 qy = by + quadrant / 2 * 4;
				WriteFourColorTable(writer, colors);
				UInt32 flags = 0;
				for (Int32 y = 3; y >= 0; y--)
				{
					for (Int32 x = 3; x >= 0; x--)
					{
						flags <<= 2;
						flags |= FindColorIndex(colors, frame.Pixels[(qy + y) * frame.Width + qx + x]);
					}
				}

				writer.WriteUInt32LittleEndian(flags);
			}

			return true;
		}

		private static Boolean TryCollectColors(IndexedVideoFrame frame, Int32 bx, Int32 by, Int32 width, Int32 height, Int32 maxColors, out Byte[] colors)
		{
			List<Byte> result = [];
			for (Int32 y = 0; y < height; y++)
			{
				for (Int32 x = 0; x < width; x++)
				{
					Byte value = frame.Pixels[(by + y) * frame.Width + bx + x];
					if (!result.Contains(value))
					{
						if (result.Count == maxColors)
						{
							colors = [];
							return false;
						}

						result.Add(value);
					}
				}
			}

			while (result.Count < maxColors)
			{
				result.Add(result[^1]);
			}

			colors = [.. result];
			return true;
		}

		private static void WriteFourColorTable(BinaryWriter writer, Byte[] colors)
		{
			writer.Write(colors[0]);
			writer.Write(colors[1]);
			writer.Write(colors[2]);
			writer.Write(colors[3]);
		}

		private static UInt32 FindColorIndex(Byte[] colors, Byte value)
		{
			for (Int32 i = 0; i < colors.Length; i++)
			{
				if (colors[i] == value)
				{
					return (UInt32)i;
				}
			}

			return 0;
		}

		private static Byte[] PackNibbles(List<Byte> opcodes)
		{
			Byte[] result = new Byte[(opcodes.Count + 1) / 2];
			for (Int32 i = 0; i < opcodes.Count; i++)
			{
				if ((i & 1) == 0)
				{
					result[i / 2] |= (Byte)(opcodes[i] & 0x0F);
				}
				else
				{
					result[i / 2] |= (Byte)(opcodes[i] << 4);
				}
			}

			return result;
		}
	}
}