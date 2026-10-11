# MveTools

.NET 10 library and CLI for Fallout/Interplay MVE conversion.

## Design

- `MVE -> video`: the library reads Interplay MVE directly, including the Fallout compressed `0x11` paletted profile and Interplay DPCM/PCM audio, then writes a standard AVI DIB indexed 8 bpp file with PCM audio. No external executable is launched.
- `video -> MVE`: the library reads uncompressed AVI DIB indexed 8 bpp, 24 bpp, or 32 bpp files with optional PCM audio and writes a compressed Interplay MVE `0x11` 8 bpp profile with MVE DPCM audio chunks. No external executable is launched.
- `inspection`: `MveFileInspector` reads the Interplay header, chunk preambles, and opcode preambles so generated files can be sanity-checked.
- Current integrated MVE support: Fallout-style paletted 8 bpp video, compressed block opcodes, global palette, Interplay DPCM audio decoding, PCM audio decoding, and PCM audio encoding.

## CLI

```powershell
dotnet run --project .\MveTools.Cli
dotnet run --project .\MveTools.Cli -- /?
dotnet run --project .\MveTools.Cli -- to-mve --help
dotnet run --project .\MveTools.Cli -- inspect "C:\Fallout2\master.dat\art\cuts\iplogo.mve"
dotnet run --project .\MveTools.Cli -- to-video ".\out\integrated\source.mve" ".\out\integrated\roundtrip.avi"
dotnet run --project .\MveTools.Cli -- to-video ".\out\integrated\source.mve" ".\out\integrated\roundtrip-rgb.avi" --format avi24
dotnet run --project .\MveTools.Cli -- to-video ".\out\integrated\source.mve" ".\out\integrated\audio.wav" --format wav
dotnet run --project .\MveTools.Cli -- to-video ".\out\integrated\source.mve" ".\out\integrated\roundtrip.mp4" --format mp4 --quality 18
dotnet run --project .\MveTools.Cli -- to-mve ".\out\integrated\source.avi" ".\out\integrated\source.mve"
dotnet run --project .\MveTools.Cli -- to-mve ".\out\integrated\source.avi" ".\out\integrated\source.mve" --hq --start 0 --len 120 --palette ".\palette.act" --dither --audio-rate 22050
```

Without arguments, the CLI opens an interactive Spectre.Console menu. Direct command-line parameters are available for scripts and non-interactive console usage. Help is available with `/?`, `-?`, `-h`, `--help`, or `help`, globally or after a command.
For `to-video`, the output format is detected from the file extension when possible and can be forced with `--format`/`-f` `avi8`, `avi24`, `wav`, or `mp4`. AVI 8 bpp, AVI 24 bpp, and WAV are integrated. MP4/H.264 uses the system `ffmpeg` command; install FFmpeg and make sure it is available in `PATH` before exporting MP4. MP4 quality is controlled with `--quality`/`--crf` from 0 to 51; 18 is the default.

FFmpeg install examples:

- Windows: `winget install Gyan.FFmpeg`
- Debian/Ubuntu: `sudo apt install ffmpeg`
- macOS: `brew install ffmpeg`
For `to-mve`, source width, height, and frame rate are detected from the video stream unless `--width`, `--height`, or `--fps` is explicitly provided. AVI is read directly. MP4, M4V, and MOV are imported through the system `ffmpeg` command into a temporary uncompressed AVI before MVE encoding. Dimensions are adjusted to MVE 8x8 blocks, and very detailed frames are downscaled automatically when they exceed the MVE payload limit. The CLI exposes conversion options such as `--test`, `--no-audio`/`-ns`, `--audio-source`/`-w`, `--audio-rate`, `--palette`/`-p`, `--save-palette`, `--start`, `--end`, `--len`, `--old`, `--hq`, `--rate`, `--size`, `--dither`, and `--no-overwrite`. The integrated encoder records `--rate`, `--size`, `--old`, and `--hq` as conversion intent; it does not use external lossy bitrate fitting.

## Library

```csharp
namespace Sample
{
    using MveTools.Conversion;

    public static class Demo
    {
        public static async Task RunAsync()
        {
            MveConverter converter = new();
            await converter.ConvertMveToVideoAsync("intro.mve", "intro.avi");
            await converter.ConvertVideoToMveAsync("intro-remastered.avi", "intro-remastered.mve");
        }
    }
}
```

## Notes

The `MveTools` library project has no external executable dependency. The CLI uses `Spectre.Console` from NuGet for the interactive menu.