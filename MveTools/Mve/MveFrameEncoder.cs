namespace MveTools.Mve
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;
	using Media;

	/// <summary>How much a frame may differ from the source to get smaller.</summary>
	/// <param name="Tolerance">Largest per-channel color difference accepted when a block is copied from a previous frame (0 = exact).</param>
	/// <param name="MaxColors">When 4, 2 or 1, blocks that would cost more than a block of that many colors are reduced to that many colors; 0 keeps every color.</param>
	internal readonly record struct MveEncodeLevel(Int32 Tolerance, Int32 MaxColors);

	/// <summary>
	/// Encodes one frame into a format 0x11 decoding map and video payload. The encoder also produces the frame as a
	/// player will reconstruct it, which is what the following frames must be compared with when the encoding is lossy.
	/// </summary>
	internal sealed class MveFrameEncoder
	{
		private readonly RgbColor[] palette;
		private readonly Boolean allowMotion;
		private readonly MotionIndex motionIndex = new();
		private readonly Int32[] colorCounts = new Int32[256];

		public MveFrameEncoder(RgbColor[] palette, Boolean allowMotion)
		{
			this.palette = palette;
			this.allowMotion = allowMotion;
		}

		public void Encode(
			IndexedVideoFrame frame,
			Byte[]? previousFrame,
			Byte[]? secondPreviousFrame,
			MveEncodeLevel level,
			out Byte[] decodeMap,
			out Byte[] videoPayload,
			out Byte[] reconstruction)
		{
			List<Byte> opcodes = [];
			using MemoryStream videoStream = new();
			using BinaryWriter videoWriter = new(videoStream);
			videoWriter.Write(new Byte[14]);

			IndexedVideoFrame working = new(frame.Width, frame.Height, (Byte[])frame.Pixels.Clone());
			motionIndex.Reset(previousFrame, frame.Width, frame.Height);

			for (Int32 by = 0; by < frame.Height; by += 8)
			{
				for (Int32 bx = 0; bx < frame.Width; bx += 8)
				{
					EncodeBlock(working, previousFrame, secondPreviousFrame, level, bx, by, opcodes, videoWriter);
				}
			}

			decodeMap = PackNibbles(opcodes);
			videoPayload = videoStream.ToArray();
			reconstruction = working.Pixels;
		}

		private void EncodeBlock(IndexedVideoFrame frame, Byte[]? previousFrame, Byte[]? secondPreviousFrame, MveEncodeLevel level, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			// Exact copies first: they cost nothing and keep the reconstruction equal to the source, which also keeps
			// the following frames easy to match. Approximate copies only come after every exact way has failed.
			if (previousFrame is not null && CopyBlockIfSimilar(frame, previousFrame, bx, by, 0))
			{
				opcodes.Add(MveBlockOpcodes.CopyPrevious);
				return;
			}

			if (secondPreviousFrame is not null && CopyBlockIfSimilar(frame, secondPreviousFrame, bx, by, 0))
			{
				opcodes.Add(MveBlockOpcodes.CopySecondPrevious);
				return;
			}

			if (allowMotion && previousFrame is not null && TryEncodeMotionFromPrevious(frame, bx, by, opcodes, writer))
			{
				return;
			}

			if (level.Tolerance > 0)
			{
				if (previousFrame is not null && CopyBlockIfSimilar(frame, previousFrame, bx, by, level.Tolerance))
				{
					opcodes.Add(MveBlockOpcodes.CopyPrevious);
					return;
				}

				if (secondPreviousFrame is not null && CopyBlockIfSimilar(frame, secondPreviousFrame, bx, by, level.Tolerance))
				{
					opcodes.Add(MveBlockOpcodes.CopySecondPrevious);
					return;
				}
			}

			Int32 opcodeCount = opcodes.Count;
			Int64 position = writer.BaseStream.Position;
			EncodeBlockContent(frame, bx, by, opcodes, writer);
			if (level.MaxColors == 0 || writer.BaseStream.Position - position <= CostLimit(level.MaxColors))
			{
				return;
			}

			// Too expensive for this level: reduce the block to a few colors and encode that instead.
			opcodes.RemoveRange(opcodeCount, opcodes.Count - opcodeCount);
			writer.BaseStream.SetLength(position);
			ReduceColors(frame, bx, by, level.MaxColors);
			EncodeBlockContent(frame, bx, by, opcodes, writer);
		}

		/// <summary>Bytes of the cheapest encoding of a block that has at most <paramref name="maxColors"/> colors.</summary>
		private static Int32 CostLimit(Int32 maxColors)
		{
			return maxColors switch
			{
				1 => 1,
				2 => 10,
				_ => 20
			};
		}

		private void EncodeBlockContent(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
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

		/// <summary>
		/// Tells whether the block of <paramref name="frame"/> matches the block at the same place in <paramref name="reference"/>
		/// within <paramref name="tolerance"/>; when it does, the block of the frame is replaced by the reference block.
		/// </summary>
		private Boolean CopyBlockIfSimilar(IndexedVideoFrame frame, Byte[] reference, Int32 bx, Int32 by, Int32 tolerance)
		{
			Boolean exact = true;
			for (Int32 y = 0; y < 8; y++)
			{
				Int32 rowStart = (by + y) * frame.Width + bx;
				for (Int32 x = 0; x < 8; x++)
				{
					Byte value = frame.Pixels[rowStart + x];
					Byte other = reference[rowStart + x];
					if (value == other)
					{
						continue;
					}

					exact = false;
					if (tolerance == 0 || !AreClose(value, other, tolerance))
					{
						return false;
					}
				}
			}

			if (!exact)
			{
				for (Int32 y = 0; y < 8; y++)
				{
					Int32 rowStart = (by + y) * frame.Width + bx;
					Buffer.BlockCopy(reference, rowStart, frame.Pixels, rowStart, 8);
				}
			}

			return true;
		}

		private Boolean AreClose(Byte first, Byte second, Int32 tolerance)
		{
			RgbColor a = palette[first];
			RgbColor b = palette[second];
			return Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
		}

		/// <summary>Replaces the colors of a block by at most <paramref name="maxColors"/> of its own colors.</summary>
		private void ReduceColors(IndexedVideoFrame frame, Int32 bx, Int32 by, Int32 maxColors)
		{
			List<Byte> distinct = [];
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Byte value = frame.Pixels[(by + y) * frame.Width + bx + x];
					if (colorCounts[value]++ == 0)
					{
						distinct.Add(value);
					}
				}
			}

			try
			{
				if (distinct.Count <= maxColors)
				{
					return;
				}

				// Representatives: the most frequent color, then repeatedly the color that is far from the chosen ones and frequent.
				List<Byte> chosen = [MostFrequent(distinct)];
				while (chosen.Count < maxColors)
				{
					Byte best = chosen[0];
					Int64 bestScore = -1;
					foreach (Byte candidate in distinct)
					{
						if (chosen.Contains(candidate))
						{
							continue;
						}

						Int32 nearest = Int32.MaxValue;
						foreach (Byte other in chosen)
						{
							nearest = Math.Min(nearest, Distance(candidate, other));
						}

						Int64 score = (Int64)nearest * colorCounts[candidate];
						if (score > bestScore)
						{
							bestScore = score;
							best = candidate;
						}
					}

					chosen.Add(best);
				}

				for (Int32 y = 0; y < 8; y++)
				{
					for (Int32 x = 0; x < 8; x++)
					{
						Int32 offset = (by + y) * frame.Width + bx + x;
						Byte value = frame.Pixels[offset];
						Byte replacement = chosen[0];
						Int32 bestDistance = Int32.MaxValue;
						foreach (Byte candidate in chosen)
						{
							Int32 distance = Distance(value, candidate);
							if (distance < bestDistance)
							{
								bestDistance = distance;
								replacement = candidate;
							}
						}

						frame.Pixels[offset] = replacement;
					}
				}
			}
			finally
			{
				foreach (Byte value in distinct)
				{
					colorCounts[value] = 0;
				}
			}
		}

		private Byte MostFrequent(List<Byte> distinct)
		{
			Byte best = distinct[0];
			foreach (Byte candidate in distinct)
			{
				if (colorCounts[candidate] > colorCounts[best] || (colorCounts[candidate] == colorCounts[best] && candidate < best))
				{
					best = candidate;
				}
			}

			return best;
		}

		private Int32 Distance(Byte first, Byte second)
		{
			RgbColor a = palette[first];
			RgbColor b = palette[second];
			Int32 dr = a.R - b.R;
			Int32 dg = a.G - b.G;
			Int32 db = a.B - b.B;
			return dr * dr + dg * dg + db * db;
		}

		private Boolean TryEncodeMotionFromPrevious(IndexedVideoFrame frame, Int32 bx, Int32 by, List<Byte> opcodes, BinaryWriter writer)
		{
			UInt64 blockHash = MotionIndex.HashBlock(frame.Pixels, frame.Width, bx, by);
			if (!motionIndex.TryFind(frame, blockHash, bx, by, out Int32 dx, out Int32 dy, out Boolean small))
			{
				return false;
			}

			if (small)
			{
				opcodes.Add(MveBlockOpcodes.SmallMotionFromPrevious);
				writer.Write((Byte)((dx + 8) | ((dy + 8) << 4)));
			}
			else
			{
				opcodes.Add(MveBlockOpcodes.MotionFromPrevious);
				writer.Write(unchecked((Byte)(SByte)dx));
				writer.Write(unchecked((Byte)(SByte)dy));
			}

			return true;
		}

		/// <summary>
		/// Finds where an 8x8 block of the current frame also appears in the previous frame, within 32 pixels. The windows of the previous
		/// frame are hashed and filed by 64x64 area, so a lookup only visits the few windows that look like the block; every candidate is
		/// confirmed pixel by pixel. Among the matches, the one the historical scan order would have found first is returned: small
		/// displacements (-8..7) in row order, then the larger ones.
		/// </summary>
		private sealed class MotionIndex
		{
			private const UInt64 RowFactor = 0x9E3779B97F4A7C15UL;
			private const UInt64 ColumnFactor = 0xC2B2AE3D27D4EB4FUL;
			private const UInt64 SlotFactor = 0xD6E8FEB86659FD93UL;
			private const Int32 CellShift = 6;
			private const Int32 Reach = 32;
			private static readonly UInt64 RowFactorPower7 = Power(RowFactor, 7);
			private static readonly UInt64 ColumnFactorPower7 = Power(ColumnFactor, 7);

			private Byte[]? previous;
			private Int32 width;
			private Int32 height;
			private Int32 gridWidth;
			private Int32 cellsPerRow;
			private Int32 slotBits;
			private Boolean built;
			private UInt64[] rowHashes = [];
			private UInt64[] windowHashes = [];
			private Int32[] head = [];
			private Int32[] next = [];

			public void Reset(Byte[]? previousFrame, Int32 frameWidth, Int32 frameHeight)
			{
				previous = previousFrame;
				width = frameWidth;
				height = frameHeight;
				built = false;
			}

			public static UInt64 HashBlock(Byte[] pixels, Int32 frameWidth, Int32 bx, Int32 by)
			{
				UInt64 hash = 0;
				for (Int32 y = 0; y < 8; y++)
				{
					UInt64 row = 0;
					Int32 start = (by + y) * frameWidth + bx;
					for (Int32 x = 0; x < 8; x++)
					{
						row = row * RowFactor + pixels[start + x] + 1UL;
					}

					hash = hash * ColumnFactor + row;
				}

				return hash;
			}

			public Boolean TryFind(IndexedVideoFrame frame, UInt64 blockHash, Int32 bx, Int32 by, out Int32 dx, out Int32 dy, out Boolean small)
			{
				dx = 0;
				dy = 0;
				small = false;
				Int32 minX = Math.Max(0, bx - Reach);
				Int32 maxX = Math.Min(width - 8, bx + Reach - 1);
				Int32 minY = Math.Max(0, by - Reach);
				Int32 maxY = Math.Min(height - 8, by + Reach - 1);
				if (minX > maxX || minY > maxY)
				{
					return false;
				}

				if (!built)
				{
					Build();
				}

				Int32 bestRank = Int32.MaxValue;
				for (Int32 cellY = minY >> CellShift; cellY <= maxY >> CellShift; cellY++)
				{
					for (Int32 cellX = minX >> CellShift; cellX <= maxX >> CellShift; cellX++)
					{
						Int32 entry = head[Slot(blockHash, cellY * cellsPerRow + cellX)];
						while (entry != 0)
						{
							Int32 position = entry - 1;
							entry = next[position];
							if (windowHashes[position] != blockHash)
							{
								continue;
							}

							Int32 sourceY = Math.DivRem(position, gridWidth, out Int32 sourceX);
							if (sourceX >> CellShift != cellX || sourceY >> CellShift != cellY
								|| sourceX < minX || sourceX > maxX || sourceY < minY || sourceY > maxY)
							{
								continue;
							}

							Int32 shiftX = sourceX - bx;
							Int32 shiftY = sourceY - by;
							if (shiftX == 0 && shiftY == 0)
							{
								continue;
							}

							Int32 rank = Rank(shiftX, shiftY);
							if (rank >= bestRank || !PixelsEqual(frame.Pixels, bx, by, sourceX, sourceY))
							{
								continue;
							}

							bestRank = rank;
							dx = shiftX;
							dy = shiftY;
						}
					}
				}

				if (bestRank == Int32.MaxValue)
				{
					return false;
				}

				small = bestRank < 256;
				return true;
			}

			/// <summary>Position of a displacement in the scan order: small displacements first, each group in row order.</summary>
			private static Int32 Rank(Int32 dx, Int32 dy)
			{
				return dx is >= -8 and <= 7 && dy is >= -8 and <= 7
					? (dy + 8) * 16 + dx + 8
					: 256 + (dy + Reach) * 2 * Reach + dx + Reach;
			}

			private Boolean PixelsEqual(Byte[] pixels, Int32 bx, Int32 by, Int32 sourceX, Int32 sourceY)
			{
				for (Int32 y = 0; y < 8; y++)
				{
					Int32 target = (by + y) * width + bx;
					Int32 source = (sourceY + y) * width + sourceX;
					for (Int32 x = 0; x < 8; x++)
					{
						if (pixels[target + x] != previous![source + x])
						{
							return false;
						}
					}
				}

				return true;
			}

			private Int32 Slot(UInt64 hash, Int32 cell)
			{
				return (Int32)(((hash ^ ((UInt64)cell * RowFactor)) * SlotFactor) >> (64 - slotBits));
			}

			private void Build()
			{
				gridWidth = width - 7;
				Int32 gridHeight = height - 7;
				Int32 positions = gridWidth * gridHeight;
				cellsPerRow = ((width - 8) >> CellShift) + 1;
				if (rowHashes.Length < height * gridWidth)
				{
					rowHashes = new UInt64[height * gridWidth];
				}

				if (windowHashes.Length < positions)
				{
					windowHashes = new UInt64[positions];
					next = new Int32[positions];
				}

				Byte[] pixels = previous!;
				for (Int32 y = 0; y < height; y++)
				{
					Int32 start = y * width;
					UInt64 hash = 0;
					for (Int32 x = 0; x < 8; x++)
					{
						hash = hash * RowFactor + pixels[start + x] + 1UL;
					}

					rowHashes[y * gridWidth] = hash;
					for (Int32 x = 1; x < gridWidth; x++)
					{
						hash = (hash - (pixels[start + x - 1] + 1UL) * RowFactorPower7) * RowFactor + pixels[start + x + 7] + 1UL;
						rowHashes[y * gridWidth + x] = hash;
					}
				}

				for (Int32 x = 0; x < gridWidth; x++)
				{
					UInt64 hash = 0;
					for (Int32 y = 0; y < 8; y++)
					{
						hash = hash * ColumnFactor + rowHashes[y * gridWidth + x];
					}

					windowHashes[x] = hash;
					for (Int32 y = 1; y < gridHeight; y++)
					{
						hash = (hash - rowHashes[(y - 1) * gridWidth + x] * ColumnFactorPower7) * ColumnFactor + rowHashes[(y + 7) * gridWidth + x];
						windowHashes[y * gridWidth + x] = hash;
					}
				}

				slotBits = 12;
				while ((1 << slotBits) < positions * 2)
				{
					slotBits++;
				}

				if (head.Length != 1 << slotBits)
				{
					head = new Int32[1 << slotBits];
				}
				else
				{
					Array.Clear(head);
				}

				for (Int32 y = 0; y < gridHeight; y++)
				{
					for (Int32 x = 0; x < gridWidth; x++)
					{
						Int32 position = y * gridWidth + x;
						Int32 slot = Slot(windowHashes[position], (y >> CellShift) * cellsPerRow + (x >> CellShift));
						next[position] = head[slot];
						head[slot] = position + 1;
					}
				}

				built = true;
			}

			private static UInt64 Power(UInt64 value, Int32 exponent)
			{
				UInt64 result = 1;
				for (Int32 i = 0; i < exponent; i++)
				{
					result *= value;
				}

				return result;
			}
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