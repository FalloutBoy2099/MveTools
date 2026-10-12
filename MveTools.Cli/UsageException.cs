namespace MveTools.Cli
{
	using System;

	/// <summary>The command line is wrong; the message is already written in the current language.</summary>
	internal sealed class UsageException : Exception
	{
		public UsageException(String message)
			: base(message)
		{
		}
	}
}