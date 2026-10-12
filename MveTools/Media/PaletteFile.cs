namespace MveTools.Media
{
	using System;
	using System.IO;
	using Errors;
	using IO;

	public static class PaletteFile
	{
		internal static RgbColor[] CreateGrayscale()
		{
			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				Byte value = (Byte)i;
				palette[i] = new RgbColor(value, value, value);
			}

			return palette;
		}

		public static RgbColor[] Read(String path)
		{
			Byte[] data = File.ReadAllBytes(path);
			if (data.Length < 768)
			{
				throw new InvalidDataException("Palette file must contain at least 768 RGB bytes.").Tag(MveErrorCode.PaletteFileTooSmall);
			}

			Int32 offset = data.Length >= 772 && data[0] == (Byte)'R' && data[1] == (Byte)'I' && data[2] == (Byte)'F' && data[3] == (Byte)'F'
				? data.Length - 768
				: 0;

			RgbColor[] palette = new RgbColor[256];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				palette[i] = new RgbColor(data[offset + i * 3], data[offset + i * 3 + 1], data[offset + i * 3 + 2]);
			}

			return palette;
		}

		public static void WriteAct(String path, RgbColor[] palette)
		{
			if (palette.Length != 256)
			{
				throw new ArgumentException("ACT palettes require 256 colors.", nameof(palette));
			}

			FileSystemHelper.EnsureParentDirectory(path);

			Byte[] data = new Byte[768];
			for (Int32 i = 0; i < palette.Length; i++)
			{
				data[i * 3] = palette[i].R;
				data[i * 3 + 1] = palette[i].G;
				data[i * 3 + 2] = palette[i].B;
			}

			File.WriteAllBytes(path, data);
		}
	}
}