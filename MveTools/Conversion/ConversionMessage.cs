namespace MveTools.Conversion
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>One step of a conversion: a code, its arguments and the English description.</summary>
	public sealed record ConversionMessage(ConversionMessageCode Code, IReadOnlyList<Object> Arguments)
	{
		public ConversionMessage(ConversionMessageCode code, params Object[] arguments)
			: this(code, (IReadOnlyList<Object>)arguments)
		{
		}

		/// <summary>The English description, formatted with the current culture.</summary>
		public override String ToString()
		{
			Object[] values = [.. Arguments];
			return String.Format(CultureInfo.CurrentCulture, Template(Code), values);
		}

		private static String Template(ConversionMessageCode code)
		{
			return code switch
			{
				ConversionMessageCode.ReadMve => "Read MVE with integrated 8 bpp video and audio decoder",
				ConversionMessageCode.WroteAvi8 => "Wrote AVI DIB indexed 8 bpp with PCM audio without external tools",
				ConversionMessageCode.WroteAvi24 => "Wrote AVI DIB RGB 24 bpp with PCM audio without external tools",
				ConversionMessageCode.WroteWav => "Wrote WAV PCM 16-bit audio without external tools",
				ConversionMessageCode.WroteMp4 => "Wrote MP4/H.264 with system FFmpeg using CRF {0}",
				ConversionMessageCode.ReadAvi => "Read AVI DIB and PCM audio with integrated reader",
				ConversionMessageCode.ConvertedSource => "Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader",
				ConversionMessageCode.DetectedProfile => "Detected source video profile: {0}x{1} at {2:0.###} fps",
				ConversionMessageCode.FrameRange => "Selected frame range: {0} frame(s)",
				ConversionMessageCode.AudioPcm => "Selected audio: PCM {0} Hz, {1}-bit, {2} channel(s)",
				ConversionMessageCode.AudioNone => "Selected audio: none",
				ConversionMessageCode.CompressionProfile => "Compression profile: {0}",
				ConversionMessageCode.Rescaled => "Rescaled video from {0}x{1} to {2}x{3} as requested.",
				ConversionMessageCode.AdjustedDimensions => "Adjusted video dimensions from {0}x{1} to {2}x{3} so MVE blocks stay aligned to 8 pixels.",
				ConversionMessageCode.BitrateTarget => "Bitrate target requested: {0} KiB/s over {1:0.###} s, a size limit of {2:0.###} MiB.",
				ConversionMessageCode.SizeLimit => "Size limit requested: {0:0.###} MiB.",
				ConversionMessageCode.TargetMetLossless => "Size limit of {0:0.###} MiB met without any quality loss ({1:0.###} MiB).",
				ConversionMessageCode.TargetMetLossy => "Size limit of {0:0.###} MiB met by accepting pixel differences up to {1} between frames ({2:0.###} MiB).",
				ConversionMessageCode.TargetNotReached => "Size limit of {0:0.###} MiB could not be reached: the smallest result is {1:0.###} MiB (pixel difference tolerance {2}).",
				ConversionMessageCode.ColorReduction => "{0} frame(s) exceeded the MVE frame size limit and were encoded with at most {1} colors per block.",
				ConversionMessageCode.Downscaled => "Compressed frame exceeded the MVE payload limit; downscaled video from {0}x{1} to {2}x{3} and retried.",
				ConversionMessageCode.TestMode => "Test mode: input and parameters validated; MVE writing skipped.",
				ConversionMessageCode.WroteMveNoAudio => "Wrote compressed Interplay MVE 0x11 video without audio and without external tools",
				ConversionMessageCode.WroteMveAudio => "Wrote compressed Interplay MVE 0x11 video with MVE DPCM audio without external tools",
				_ => code.ToString()
			};
		}
	}
}