namespace MveTools.Cli
{
	using System;

	internal static partial class AppText
	{
		public static String LocalizeStep(String step)
		{
			if (Current != CliLanguage.French)
			{
				return step;
			}

			if (step == "Read MVE with integrated 8 bpp video and audio decoder")
			{
				return "Lecture du MVE avec le décodeur vidéo 8 bpp et audio intégré";
			}

			if (step == "Wrote AVI DIB indexed 8 bpp with PCM audio without external tools")
			{
				return "Écriture AVI DIB 8 bpp palettisé avec audio PCM, sans outil externe";
			}

			if (step == "Wrote AVI DIB RGB 24 bpp with PCM audio without external tools")
			{
				return "Écriture AVI DIB RGB 24 bpp avec audio PCM, sans outil externe";
			}

			if (step == "Wrote WAV PCM 16-bit audio without external tools")
			{
				return "Écriture audio WAV PCM 16 bits, sans outil externe";
			}

			if (step == "Wrote MP4/H.264 with system FFmpeg")
			{
				return "Écriture MP4/H.264 avec le FFmpeg du système";
			}

			if (step.StartsWith("Wrote MP4/H.264 with system FFmpeg using CRF ", StringComparison.Ordinal))
			{
				String value = step["Wrote MP4/H.264 with system FFmpeg using CRF ".Length..];
				return $"Écriture MP4/H.264 avec le FFmpeg du système en CRF {value}";
			}

			if (step == "Read AVI DIB and PCM audio with integrated reader")
			{
				return "Lecture AVI DIB et audio PCM avec le lecteur intégré";
			}

			if (step == "Converted source video to temporary uncompressed AVI with system FFmpeg and read it with integrated reader")
			{
				return "Conversion de la vidéo source en AVI temporaire non compressé avec le FFmpeg du système, puis lecture avec le lecteur intégré";
			}

			if (step.StartsWith("Detected source video profile: ", StringComparison.Ordinal))
			{
				String value = step["Detected source video profile: ".Length..].Replace(" at ", " à ", StringComparison.Ordinal).Replace(" fps", " i/s", StringComparison.Ordinal);
				return $"Profil vidéo source détecté : {value}";
			}

			if (step.StartsWith("Selected frame range: ", StringComparison.Ordinal))
			{
				String value = step["Selected frame range: ".Length..].Replace(" frame(s)", " image(s)", StringComparison.Ordinal);
				return $"Plage sélectionnée : {value}";
			}

			if (step.StartsWith("Selected audio: PCM ", StringComparison.Ordinal))
			{
				String value = step["Selected audio: ".Length..]
					.Replace("-bit", " bits", StringComparison.Ordinal)
					.Replace(" channel(s)", " canal/canaux", StringComparison.Ordinal);
				return $"Audio sélectionné : {value}";
			}

			if (step == "Selected audio: none")
			{
				return "Audio sélectionné : aucun";
			}

			if (step.StartsWith("Compression profile: ", StringComparison.Ordinal))
			{
				String value = step["Compression profile: ".Length..] switch
				{
					"HighQuality" => Get("CompressionHighQuality"),
					"Old" => Get("CompressionOld"),
					_ => Get("CompressionDefault")
				};
				return $"Profil de compression : {value}";
			}

			if (step.StartsWith("Bitrate target requested: ", StringComparison.Ordinal))
			{
				String value = BeforeParenthesis(step["Bitrate target requested: ".Length..]);
				return $"Débit cible demandé : {value} (conservé comme intention de conversion ; l'encodeur intégré reste sans perte par bloc).";
			}

			if (step.StartsWith("Size limit requested: ", StringComparison.Ordinal))
			{
				String value = BeforeParenthesis(step["Size limit requested: ".Length..]);
				return $"Taille cible demandée : {value} (conservée comme intention de conversion ; l'encodeur intégré reste sans perte par bloc).";
			}

			if (step == "Test mode: input and parameters validated; MVE writing skipped.")
			{
				return "Mode simulation : entrée et paramètres validés ; écriture MVE ignorée.";
			}

			if (step == "Wrote compressed Interplay MVE 0x11 video without audio and without external tools")
			{
				return "Écriture vidéo Interplay MVE 0x11 compressée sans audio et sans outil externe";
			}

			if (step == "Wrote compressed Interplay MVE 0x11 video with MVE DPCM audio without external tools")
			{
				return "Écriture vidéo Interplay MVE 0x11 compressée avec audio MVE DPCM et sans outil externe";
			}

			if (step.StartsWith("Compressed frame exceeded the MVE payload limit; downscaled video from ", StringComparison.Ordinal))
			{
				String value = step["Compressed frame exceeded the MVE payload limit; downscaled video from ".Length..].Replace(" to ", " à ", StringComparison.Ordinal).Replace(" and retried.", "", StringComparison.Ordinal);
				return $"Une image compressée dépassait la limite MVE ; vidéo réduite de {value}, puis nouvel essai.";
			}

			if (step.StartsWith("Adjusted video dimensions from ", StringComparison.Ordinal))
			{
				String value = step["Adjusted video dimensions from ".Length..].Replace(" to ", " à ", StringComparison.Ordinal).Replace(" so MVE blocks stay aligned to 8 pixels.", "", StringComparison.Ordinal);
				return $"Dimensions vidéo ajustées de {value} pour aligner les blocs MVE sur 8 pixels.";
			}

			return step;
		}

		private static String BeforeParenthesis(String value)
		{
			Int32 index = value.IndexOf(" (", StringComparison.Ordinal);
			return index < 0 ? value : value[..index];
		}
	}
}
