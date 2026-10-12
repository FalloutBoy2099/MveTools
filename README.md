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
For `to-mve`, source width, height, and frame rate are detected from the video stream unless `--width`, `--height`, or `--fps` is explicitly provided. AVI is read directly. MP4, M4V, and MOV are imported through the system `ffmpeg` command into a temporary uncompressed AVI before MVE encoding. Dimensions are adjusted to MVE 8x8 blocks. `--width` and `--height` rescale the video (nearest neighbor; the one that is not given is unchanged). A frame too detailed for the MVE frame size limit (64 KiB) keeps its resolution: its blocks are reduced to 4, 2 or 1 colors as needed, and the video is only halved if even that is not enough. `--rate <KiB/s>` and `--size <MiB>` set a size target: the encoder raises the difference it tolerates between a block and the same block of an earlier frame until the file fits, and reports the result (if the target is out of reach, the smallest result is kept). `--old` disables the motion search (faster, larger files) and `--hq` lowers the largest difference the encoder may tolerate. The CLI exposes conversion options such as `--test`, `--no-audio`/`-ns`, `--audio-source`/`-w`, `--audio-rate`, `--palette`/`-p`, `--save-palette`, `--start`, `--end`, `--len`, `--old`, `--hq`, `--rate`, `--size`, `--dither`, and `--no-overwrite`.

Command line arguments are checked strictly: an unknown option, a missing or invalid value, a repeated option, or conflicting options (`--rate`/`--size`, `--end`/`--len`, `--old`/`--hq`) is an error. Ctrl+C cancels a running conversion and removes the partial output file. `--palette` also applies to 8 bpp AVI sources: their pixels are remapped to the closest colors of the given palette.

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

## Publishing

`Publish.ps1` produces single-file executables of the CLI for Windows, macOS and Linux on x64 and arm64 (12 files: each platform with and without the bundled .NET runtime) under `publish\<runtime>\with-dotnet` and `publish\<runtime>\without-dotnet`.

```powershell
.\Publish.ps1                                   # all six platforms
.\Publish.ps1 -RuntimeIdentifier win-x64        # a single platform
.\Publish.ps1 -RuntimeIdentifier osx-arm64,linux-arm64 -Clean
```

Supported runtime identifiers are `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, and `linux-arm64`. Files published from Windows for Linux or macOS are not marked executable; run `chmod +x` after copying them.

## Notes

The `MveTools` library project has no external executable dependency. The CLI uses `Spectre.Console` from NuGet for the interactive menu.