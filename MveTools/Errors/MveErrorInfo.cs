namespace MveTools.Errors
{
	using System;
	using System.Collections.Generic;

	/// <summary>Reads the machine-readable description attached to the exceptions thrown by this library.</summary>
	public static class MveErrorInfo
	{
		private const String CodeKey = "MveTools.ErrorCode";
		private const String ArgumentsKey = "MveTools.ErrorArguments";

		/// <summary>Gets the error code and message arguments of an exception thrown by this library, if it has any.</summary>
		public static Boolean TryGet(Exception exception, out MveErrorCode code, out IReadOnlyList<Object> arguments)
		{
			if (exception.Data[CodeKey] is MveErrorCode found)
			{
				code = found;
				arguments = exception.Data[ArgumentsKey] as IReadOnlyList<Object> ?? [];
				return true;
			}

			code = default;
			arguments = [];
			return false;
		}

		internal static TException Tag<TException>(this TException exception, MveErrorCode code, params Object[] arguments)
			where TException : Exception
		{
			exception.Data[CodeKey] = code;
			exception.Data[ArgumentsKey] = arguments;
			return exception;
		}
	}
}