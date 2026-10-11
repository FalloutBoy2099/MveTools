namespace MveTools.Mve
{
	using System;
	using System.IO;

	internal sealed class MveBlockDecoder
	{
		private readonly Int32 width;
		private readonly Int32 height;
		private readonly Byte[] current;
		private readonly Byte[]? last;
		private readonly Byte[]? secondLast;
		private readonly BinaryReader reader;

		public MveBlockDecoder(Int32 width, Int32 height, Byte[] current, Byte[]? last, Byte[]? secondLast, BinaryReader reader)
		{
			this.width = width;
			this.height = height;
			this.current = current;
			this.last = last;
			this.secondLast = secondLast;
			this.reader = reader;
		}

		public void DecodeBlock(Byte opcode, Int32 blockX, Int32 blockY)
		{
			switch (opcode)
			{
				case MveBlockOpcodes.CopyPrevious:
					CopyBlock(last, blockX, blockY, 0, 0);
					break;
				case MveBlockOpcodes.CopySecondPrevious:
					CopyBlock(secondLast, blockX, blockY, 0, 0);
					break;
				case MveBlockOpcodes.MotionFromSecondPrevious:
					DecodeMotionFromSecondLast(blockX, blockY, reverse: false);
					break;
				case MveBlockOpcodes.MotionFromCurrent:
					DecodeMotionFromCurrent(blockX, blockY);
					break;
				case MveBlockOpcodes.SmallMotionFromPrevious:
					DecodeSmallMotionFromLast(blockX, blockY);
					break;
				case MveBlockOpcodes.MotionFromPrevious:
					CopyBlock(last, blockX, blockY, ReadSByte(), ReadSByte());
					break;
				case MveBlockOpcodes.Skip:
					break;
				case MveBlockOpcodes.TwoColor:
					DecodeTwoColor(blockX, blockY);
					break;
				case MveBlockOpcodes.TwoColorSplit:
					DecodeTwoColorSplit(blockX, blockY);
					break;
				case MveBlockOpcodes.FourColor:
					DecodeFourColor(blockX, blockY);
					break;
				case MveBlockOpcodes.FourColorSplit:
					DecodeFourColorSplit(blockX, blockY);
					break;
				case MveBlockOpcodes.Raw:
					DecodeRaw(blockX, blockY);
					break;
				case MveBlockOpcodes.SixteenColor:
					DecodeSixteenColor(blockX, blockY);
					break;
				case MveBlockOpcodes.FourColorQuadrants:
					DecodeFourColorBlocks(blockX, blockY);
					break;
				case MveBlockOpcodes.Solid:
					FillBlock(blockX, blockY, reader.ReadByte());
					break;
				case MveBlockOpcodes.Dither:
					DecodeDither(blockX, blockY);
					break;
				default:
					throw new InvalidDataException($"Unknown MVE block opcode 0x{opcode:X}.");
			}
		}

		private void DecodeMotionFromSecondLast(Int32 blockX, Int32 blockY, Boolean reverse)
		{
			Byte b = reader.ReadByte();
			Int32 x;
			Int32 y;
			if (b < 56)
			{
				x = 8 + b % 7;
				y = b / 7;
			}
			else
			{
				x = -14 + (b - 56) % 29;
				y = 8 + (b - 56) / 29;
			}

			if (reverse)
			{
				x = -x;
				y = -y;
			}

			CopyBlock(secondLast, blockX, blockY, x, y);
		}

		private void DecodeMotionFromCurrent(Int32 blockX, Int32 blockY)
		{
			Byte b = reader.ReadByte();
			Int32 x;
			Int32 y;
			if (b < 56)
			{
				x = -(8 + b % 7);
				y = -(b / 7);
			}
			else
			{
				x = -(-14 + (b - 56) % 29);
				y = -(8 + (b - 56) / 29);
			}

			CopyBlock(current, blockX, blockY, x, y);
		}

		private void DecodeSmallMotionFromLast(Int32 blockX, Int32 blockY)
		{
			Byte b = reader.ReadByte();
			Int32 x = -8 + (b & 0x0F);
			Int32 y = -8 + ((b >> 4) & 0x0F);
			CopyBlock(last, blockX, blockY, x, y);
		}

		private void DecodeTwoColor(Int32 blockX, Int32 blockY)
		{
			Byte p0 = reader.ReadByte();
			Byte p1 = reader.ReadByte();
			if (p0 <= p1)
			{
				for (Int32 y = 0; y < 8; y++)
				{
					Int32 flags = reader.ReadByte() | 0x100;
					for (Int32 x = 0; flags != 1; x++, flags >>= 1)
					{
						SetPixel(blockX + x, blockY + y, (flags & 1) == 0 ? p0 : p1);
					}
				}
			}
			else
			{
				UInt16 flags = reader.ReadUInt16();
				for (Int32 y = 0; y < 8; y += 2)
				{
					for (Int32 x = 0; x < 8; x += 2, flags >>= 1)
					{
						Fill2x2(blockX + x, blockY + y, (flags & 1) == 0 ? p0 : p1);
					}
				}
			}
		}

		private void DecodeTwoColorSplit(Int32 blockX, Int32 blockY)
		{
			Byte p0 = reader.ReadByte();
			Byte p1 = reader.ReadByte();
			if (p0 <= p1)
			{
				for (Int32 quadrant = 0; quadrant < 4; quadrant++)
				{
					if (quadrant > 0)
					{
						p0 = reader.ReadByte();
						p1 = reader.ReadByte();
					}

					UInt16 flags = reader.ReadUInt16();
					Int32 qx = blockX + quadrant % 2 * 4;
					Int32 qy = blockY + quadrant / 2 * 4;
					for (Int32 y = 0; y < 4; y++)
					{
						for (Int32 x = 0; x < 4; x++, flags >>= 1)
						{
							SetPixel(qx + x, qy + y, (flags & 1) == 0 ? p0 : p1);
						}
					}
				}
			}
			else
			{
				UInt32 flags = reader.ReadUInt32();
				Byte p2 = reader.ReadByte();
				Byte p3 = reader.ReadByte();
				Boolean vertical = p2 <= p3;
				for (Int32 half = 0; half < 2; half++)
				{
					Byte a = half == 0 ? p0 : p2;
					Byte b = half == 0 ? p1 : p3;
					if (half == 1)
					{
						flags = reader.ReadUInt32();
					}

					for (Int32 y = 0; y < 8; y++)
					{
						for (Int32 x = 0; x < 4; x++, flags >>= 1)
						{
							Int32 px = vertical ? blockX + half * 4 + x : blockX + x + (y >= 4 ? 4 : 0);
							Int32 py = vertical ? blockY + y : blockY + half * 4 + y % 4;
							SetPixel(px, py, (flags & 1) == 0 ? a : b);
						}
					}
				}
			}
		}

		private void DecodeFourColor(Int32 blockX, Int32 blockY)
		{
			Byte[] p = reader.ReadBytes(4);
			if (p[0] <= p[1])
			{
				if (p[2] <= p[3])
				{
					for (Int32 y = 0; y < 8; y++)
					{
						UInt16 flags = reader.ReadUInt16();
						for (Int32 x = 0; x < 8; x++, flags >>= 2)
						{
							SetPixel(blockX + x, blockY + y, p[flags & 0x03]);
						}
					}
				}
				else
				{
					UInt32 flags = reader.ReadUInt32();
					for (Int32 y = 0; y < 8; y += 2)
					{
						for (Int32 x = 0; x < 8; x += 2, flags >>= 2)
						{
							Fill2x2(blockX + x, blockY + y, p[flags & 0x03]);
						}
					}
				}
			}
			else
			{
				UInt64 flags = reader.ReadUInt64();
				if (p[2] <= p[3])
				{
					for (Int32 y = 0; y < 8; y++)
					{
						for (Int32 x = 0; x < 8; x += 2, flags >>= 2)
						{
							Byte value = p[flags & 0x03];
							SetPixel(blockX + x, blockY + y, value);
							SetPixel(blockX + x + 1, blockY + y, value);
						}
					}
				}
				else
				{
					for (Int32 y = 0; y < 8; y += 2)
					{
						for (Int32 x = 0; x < 8; x++, flags >>= 2)
						{
							Byte value = p[flags & 0x03];
							SetPixel(blockX + x, blockY + y, value);
							SetPixel(blockX + x, blockY + y + 1, value);
						}
					}
				}
			}
		}

		private void DecodeFourColorSplit(Int32 blockX, Int32 blockY)
		{
			Byte[] p = reader.ReadBytes(4);
			if (p[0] <= p[1])
			{
				for (Int32 quadrant = 0; quadrant < 4; quadrant++)
				{
					if (quadrant > 0)
					{
						p = reader.ReadBytes(4);
					}

					UInt32 flags = reader.ReadUInt32();
					Int32 qx = blockX + quadrant % 2 * 4;
					Int32 qy = blockY + quadrant / 2 * 4;
					for (Int32 y = 0; y < 4; y++)
					{
						for (Int32 x = 0; x < 4; x++, flags >>= 2)
						{
							SetPixel(qx + x, qy + y, p[flags & 0x03]);
						}
					}
				}
			}
			else
			{
				UInt64 flags = reader.ReadUInt64();
				Byte[] p2 = reader.ReadBytes(4);
				Boolean vertical = p2[0] <= p2[1];

				for (Int32 half = 0; half < 2; half++)
				{
					Byte[] colors = half == 0 ? p : p2;
					if (half == 1)
					{
						flags = reader.ReadUInt64();
					}

					if (vertical)
					{
						Int32 startX = blockX + half * 4;
						for (Int32 y = 0; y < 8; y++)
						{
							for (Int32 x = 0; x < 4; x++, flags >>= 2)
							{
								SetPixel(startX + x, blockY + y, colors[flags & 0x03]);
							}
						}
					}
					else
					{
						Int32 startY = blockY + half * 4;
						for (Int32 y = 0; y < 4; y++)
						{
							for (Int32 x = 0; x < 8; x++, flags >>= 2)
							{
								SetPixel(blockX + x, startY + y, colors[flags & 0x03]);
							}
						}
					}
				}
			}
		}

		private void DecodeRaw(Int32 blockX, Int32 blockY)
		{
			for (Int32 y = 0; y < 8; y++)
			{
				Byte[] row = reader.ReadBytes(8);
				for (Int32 x = 0; x < 8; x++)
				{
					SetPixel(blockX + x, blockY + y, row[x]);
				}
			}
		}

		private void DecodeSixteenColor(Int32 blockX, Int32 blockY)
		{
			for (Int32 y = 0; y < 8; y += 2)
			{
				for (Int32 x = 0; x < 8; x += 2)
				{
					Fill2x2(blockX + x, blockY + y, reader.ReadByte());
				}
			}
		}

		private void DecodeFourColorBlocks(Int32 blockX, Int32 blockY)
		{
			Byte p0 = 0;
			Byte p1 = 0;
			for (Int32 y = 0; y < 8; y++)
			{
				if ((y & 3) == 0)
				{
					p0 = reader.ReadByte();
					p1 = reader.ReadByte();
				}

				for (Int32 x = 0; x < 4; x++)
				{
					SetPixel(blockX + x, blockY + y, p0);
					SetPixel(blockX + x + 4, blockY + y, p1);
				}
			}
		}

		private void DecodeDither(Int32 blockX, Int32 blockY)
		{
			Byte p0 = reader.ReadByte();
			Byte p1 = reader.ReadByte();
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					SetPixel(blockX + x, blockY + y, ((x ^ y) & 1) == 0 ? p0 : p1);
				}
			}
		}

		private void FillBlock(Int32 blockX, Int32 blockY, Byte value)
		{
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					SetPixel(blockX + x, blockY + y, value);
				}
			}
		}

		private void CopyBlock(Byte[]? source, Int32 blockX, Int32 blockY, Int32 motionX, Int32 motionY)
		{
			if (source is null)
			{
				return;
			}

			Int32 sourceX = blockX + motionX;
			Int32 sourceY = blockY + motionY;
			for (Int32 y = 0; y < 8; y++)
			{
				for (Int32 x = 0; x < 8; x++)
				{
					Int32 sx = sourceX + x;
					Int32 sy = sourceY + y;
					if (sx >= 0 && sx < width && sy >= 0 && sy < height)
					{
						SetPixel(blockX + x, blockY + y, source[sy * width + sx]);
					}
				}
			}
		}

		private void Fill2x2(Int32 x, Int32 y, Byte value)
		{
			SetPixel(x, y, value);
			SetPixel(x + 1, y, value);
			SetPixel(x, y + 1, value);
			SetPixel(x + 1, y + 1, value);
		}

		private void SetPixel(Int32 x, Int32 y, Byte value)
		{
			if (x >= 0 && x < width && y >= 0 && y < height)
			{
				current[y * width + x] = value;
			}
		}

		private SByte ReadSByte() => unchecked((SByte)reader.ReadByte());
	}
}