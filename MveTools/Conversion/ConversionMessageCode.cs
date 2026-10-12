namespace MveTools.Conversion
{
	/// <summary>Identifies a conversion step so that applications can show a translated description.</summary>
	public enum ConversionMessageCode
	{
		ReadMve,
		WroteAvi8,
		WroteAvi24,
		WroteWav,
		/// <summary>Arguments: CRF quality.</summary>
		WroteMp4,
		ReadAvi,
		ConvertedSource,
		/// <summary>Arguments: width, height, frame rate (Double).</summary>
		DetectedProfile,
		/// <summary>Arguments: frame count.</summary>
		FrameRange,
		/// <summary>Arguments: sample rate, bits per sample, channels.</summary>
		AudioPcm,
		AudioNone,
		/// <summary>Arguments: compression mode (<see cref="MveCompressionMode"/>).</summary>
		CompressionProfile,
		/// <summary>Arguments: width, height, new width, new height.</summary>
		Rescaled,
		/// <summary>Arguments: width, height, new width, new height.</summary>
		AdjustedDimensions,
		/// <summary>Arguments: bit rate in KiB/s, duration in seconds (Double), size limit in MiB (Double).</summary>
		BitrateTarget,
		/// <summary>Arguments: size limit in MiB (Double).</summary>
		SizeLimit,
		/// <summary>Arguments: size limit in MiB, resulting size in MiB (both Double).</summary>
		TargetMetLossless,
		/// <summary>Arguments: size limit in MiB, tolerance, resulting size in MiB.</summary>
		TargetMetLossy,
		/// <summary>Arguments: size limit in MiB, best size in MiB, tolerance.</summary>
		TargetNotReached,
		/// <summary>Arguments: number of frames, maximum number of colors per block.</summary>
		ColorReduction,
		/// <summary>Arguments: width, height, new width, new height.</summary>
		Downscaled,
		TestMode,
		WroteMveNoAudio,
		WroteMveAudio
	}
}