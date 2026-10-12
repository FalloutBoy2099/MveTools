namespace MveTools.Errors
{
	/// <summary>
	/// Identifies a user-facing error so that applications can show a translated message.
	/// The exception <see cref="System.Exception.Message"/> stays in English; the arguments of the message
	/// are returned by <see cref="MveErrorInfo.TryGet"/>, in the order documented on each value.
	/// </summary>
	public enum MveErrorCode
	{
		NotRiffContainer,
		NotWaveContainer,
		/// <summary>Arguments: file offset.</summary>
		WavChunkInvalid,
		/// <summary>Arguments: channels, bits per sample, sample rate.</summary>
		WavAudioLayoutNotSupported,
		NotAviContainer,
		/// <summary>Arguments: format tag (UInt16).</summary>
		UnsupportedWavFormat,
		AviHeaderIncomplete,
		/// <summary>Arguments: bits per pixel.</summary>
		UnsupportedBitDepth,
		/// <summary>Arguments: format tag (UInt16).</summary>
		UnsupportedAviAudioFormat,
		/// <summary>Arguments: width, height.</summary>
		AviInvalidDimensions,
		/// <summary>Arguments: file offset.</summary>
		AviChunkOutOfRange,
		/// <summary>Arguments: frame index.</summary>
		AviFrameTruncated,
		AviNoFrames,
		/// <summary>Arguments: compression value.</summary>
		AviCompressionNotSupported,
		/// <summary>Arguments: channels, bits per sample.</summary>
		AviAudioLayoutNotSupported,
		/// <summary>Arguments: video standard.</summary>
		UnsupportedOutputStandard,
		NoAudioForWav,
		OutputIsInput,
		Mp4QualityOutOfRange,
		/// <summary>Arguments: path.</summary>
		InputFileMissing,
		/// <summary>Arguments: path.</summary>
		AudioSourceFileMissing,
		/// <summary>Arguments: path.</summary>
		PaletteFileMissing,
		/// <summary>Arguments: path.</summary>
		OutputExists,
		/// <summary>Arguments: path.</summary>
		OutputNotProduced,
		VideoSourceNotSupported,
		ExternalAviWithoutAudio,
		ExternalAudioNotSupported,
		/// <summary>Arguments: path.</summary>
		MveFileMissing,
		NotMveFile,
		MveHeaderTruncated,
		/// <summary>Arguments: file offset.</summary>
		MveChunkPreambleTruncated,
		/// <summary>Arguments: file offset.</summary>
		MveChunkExceedsFile,
		/// <summary>Arguments: file offset.</summary>
		MveOpcodePreambleTruncated,
		/// <summary>Arguments: file offset.</summary>
		MveOpcodeExceedsChunk,
		/// <summary>Arguments: file offset.</summary>
		MveOpcodeTruncated,
		PaletteFileTooSmall,
		FrameRangeEmpty,
		FfmpegMissingForExport,
		FfmpegMissingForImport,
		/// <summary>Arguments: exit code, details.</summary>
		FfmpegExportFailed,
		/// <summary>Arguments: exit code, details.</summary>
		FfmpegImportFailed,
		DpcmPayloadTruncated,
		DecodingMapTruncated,
		/// <summary>Arguments: opcode.</summary>
		UnknownBlockOpcode,
		NoVideoFrames,
		AudioInitTruncated,
		PcmAudioTruncated,
		FrameMissingDecodingMap,
		FramePayloadMissingPreamble,
		/// <summary>Arguments: block x, block y, opcode, reason.</summary>
		BlockDecodeFailed,
		VideoDimensionsNotInitialized,
		RawFrameMissingDecodingMap,
		/// <summary>Arguments: opcode.</summary>
		RawOpcodeNotSupported,
		RawPayloadTruncated,
		DimensionsNotMultipleOf8,
		/// <summary>Arguments: opcode type, payload length.</summary>
		OpcodePayloadTooLarge,
		/// <summary>Arguments: chunk type, payload length.</summary>
		ChunkPayloadTooLarge,
		/// <summary>Arguments: payload length.</summary>
		FrameTooLarge,
		/// <summary>Arguments: width, height.</summary>
		MveInvalidDimensions,
		/// <summary>Arguments: byte count.</summary>
		MveSilenceTooLarge
	}
}