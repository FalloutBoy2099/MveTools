namespace MveTools.Cli.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Text.RegularExpressions;

	internal static partial class AppText
	{
		// The conversion steps are produced in English by MveConverter. Each pattern recognizes one of them and
		// names the text key whose template (with {0}, {1}... for the captured values) is looked up in the
		// current language only: a language without that key keeps the original English step.
		private static readonly (String Key, Regex Pattern)[] StepPatterns =
		[
			Exact("StepReadMve", "Read MVE with integrated 8 bpp video and audio decoder"),
			Exact("StepWroteAvi8", "Wrote AVI DIB indexed 8 bpp with PCM audio without external tools"),
			Exact("StepWroteAvi24", "Wrote AVI DIB RGB 24 bpp with PCM audio without external tools"),
			Exact("StepWroteWav", "Wrote WAV PCM 16-bit audio without external tools"),
			Exact("StepWroteMp4", "Wrote MP4/H.264 with system FFmpeg"),
			Pattern("StepWroteMp4Crf", @"^Wrote MP4/H\.264 with system FFmpeg using CRF (.+)$"),
			Exact("StepReadAvi", "Read AVI DIB and PCM audio with integrated reader"),
			Exact("StepConvertedSource", "Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader"),
			Pattern("StepDetectedProfile", @"^Detected source video profile: (.+) at (.+) fps$"),
			Pattern("StepFrameRange", @"^Selected frame range: (.+) frame\(s\)$"),
			Pattern("StepAudioPcm", @"^Selected audio: PCM (.+) Hz, (.+)-bit, (.+) channel\(s\)$"),
			Exact("StepAudioNone", "Selected audio: none"),
			Pattern("StepCompressionProfile", @"^Compression profile: (.+)$"),
			Pattern("StepBitrateTarget", @"^Bitrate target requested: ((?:(?! \().)*)"),
			Pattern("StepSizeLimit", @"^Size limit requested: ((?:(?! \().)*)"),
			Exact("StepTestMode", "Test mode: input and parameters validated; MVE writing skipped."),
			Exact("StepWroteMveNoAudio", "Wrote compressed Interplay MVE 0x11 video without audio and without external tools"),
			Exact("StepWroteMveAudio", "Wrote compressed Interplay MVE 0x11 video with MVE DPCM audio without external tools"),
			Pattern("StepDownscaled", @"^Compressed frame exceeded the MVE payload limit; downscaled video from (.+) to (.+) and retried\.$"),
			Pattern("StepAdjustedDimensions", @"^Adjusted video dimensions from (.+) to (.+) so MVE blocks stay aligned to 8 pixels\.$")
		];

		public static String LocalizeStep(String step)
		{
			foreach ((String key, Regex pattern) in StepPatterns)
			{
				Match match = pattern.Match(step);
				if (!match.Success)
				{
					continue;
				}

				if (!TryGetOwnText(key, out String? template))
				{
					return step;
				}

				Object[] values = new Object[match.Groups.Count - 1];
				for (Int32 index = 1; index < match.Groups.Count; index++)
				{
					values[index - 1] = match.Groups[index].Value;
				}

				if (key == "StepCompressionProfile")
				{
					values[0] = values[0].ToString() switch
					{
						"HighQuality" => Get("CompressionHighQuality"),
						"Old" => Get("CompressionOld"),
						_ => Get("CompressionDefault")
					};
				}

				return String.Format(CultureInfo.CurrentCulture, template, values);
			}

			return step;
		}

		private static Boolean TryGetOwnText(String key, [NotNullWhen(true)] out String? value)
		{
			value = null;
			return Texts.TryGetValue(Current, out IReadOnlyDictionary<String, String>? own) && own.TryGetValue(key, out value);
		}

		private static (String Key, Regex Pattern) Exact(String key, String step)
		{
			return Pattern(key, $"^{Regex.Escape(step)}$");
		}

		private static (String Key, Regex Pattern) Pattern(String key, String expression)
		{
			return (key, new Regex(expression, RegexOptions.CultureInvariant));
		}
	}
}