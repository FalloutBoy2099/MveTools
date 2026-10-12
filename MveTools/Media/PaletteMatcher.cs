namespace MveTools.Media
{
	using System;

	/// <summary>Finds the palette entry closest to an RGB color (first entry wins on ties), with a small direct-mapped cache.</summary>
	internal sealed class PaletteMatcher
	{
		private const Int32 CacheBits = 18;
		private const Int32 CacheMask = (1 << CacheBits) - 1;

		private readonly RgbColor[] palette;
		private readonly Int32[] cacheKeys = new Int32[1 << CacheBits];
		private readonly Byte[] cacheValues = new Byte[1 << CacheBits];

		public PaletteMatcher(RgbColor[] palette)
		{
			this.palette = palette;
			Array.Fill(cacheKeys, -1);
		}

		public Byte FindNearest(Byte r, Byte g, Byte b)
		{
			Int32 key = (r << 16) | (g << 8) | b;
			Int32 slot = (Int32)((UInt32)key * 2654435761u >> (32 - CacheBits)) & CacheMask;
			if (cacheKeys[slot] == key)
			{
				return cacheValues[slot];
			}

			Byte index = Search(r, g, b);
			cacheKeys[slot] = key;
			cacheValues[slot] = index;
			return index;
		}

		private Byte Search(Byte r, Byte g, Byte b)
		{
			Int32 bestIndex = 0;
			Int32 bestDistance = Int32.MaxValue;
			for (Int32 i = 0; i < palette.Length; i++)
			{
				Int32 dr = palette[i].R - r;
				Int32 dg = palette[i].G - g;
				Int32 db = palette[i].B - b;
				Int32 distance = dr * dr + dg * dg + db * db;
				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestIndex = i;
					if (distance == 0)
					{
						break;
					}
				}
			}

			return (Byte)bestIndex;
		}
	}
}