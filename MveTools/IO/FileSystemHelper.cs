namespace MveTools.IO
{
	using System;
	using System.IO;

	internal static class FileSystemHelper
	{
		public static void EnsureParentDirectory(String path)
		{
			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}
		}

		public static String CreateTempDirectory()
		{
			String path = Path.Combine(Path.GetTempPath(), $"mvetools-{Guid.NewGuid():N}");
			Directory.CreateDirectory(path);
			return path;
		}

		public static void TryDeleteDirectory(String path)
		{
			try
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, true);
				}
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
