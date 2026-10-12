namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public sealed record ConversionResult(
		String OutputPath,
		IReadOnlyList<ConversionMessage> Messages)
	{
		/// <summary>The English description of each step.</summary>
		public IReadOnlyList<String> Steps => [.. Messages.Select(message => message.ToString())];
	}
}