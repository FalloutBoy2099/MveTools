namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;

	public sealed record ConversionResult(
		String OutputPath,
		IReadOnlyList<String> Steps);
}