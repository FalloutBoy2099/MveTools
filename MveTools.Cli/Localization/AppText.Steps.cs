namespace MveTools.Cli.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using MveTools.Conversion;
	using MveTools.Errors;

	internal static partial class AppText
	{
		/// <summary>
		/// Describes a conversion step in the current language. The template is the text <c>Step&lt;Code&gt;</c> of that language only:
		/// a language without it keeps the English description given by the library.
		/// </summary>
		public static String LocalizeMessage(ConversionMessage message)
		{
			if (!TryGetOwnText("Step" + message.Code, out String? template))
			{
				return message.ToString();
			}

			Object[] values = [.. message.Arguments.Select(LocalizeArgument)];
			return String.Format(CultureInfo.CurrentCulture, template, values);
		}

		/// <summary>
		/// Describes an error in the current language: the text <c>Error&lt;Code&gt;</c> of that language when the library gave a code
		/// and the language has it, otherwise the message of the exception.
		/// </summary>
		public static String DescribeError(Exception exception)
		{
			if (exception is OperationCanceledException)
			{
				return Get("Cancelled");
			}

			if (MveErrorInfo.TryGet(exception, out MveErrorCode code, out IReadOnlyList<Object> arguments)
				&& TryGetOwnText("Error" + code, out String? template))
			{
				return String.Format(CultureInfo.CurrentCulture, template, [.. arguments]);
			}

			return exception.Message;
		}

		private static Object LocalizeArgument(Object argument)
		{
			return argument is MveCompressionMode mode ? DescribeCompressionMode(mode) : argument;
		}

		private static Boolean TryGetOwnText(String key, [NotNullWhen(true)] out String? value)
		{
			value = null;
			return Texts.TryGetValue(Current, out IReadOnlyDictionary<String, String>? own) && own.TryGetValue(key, out value);
		}
	}
}