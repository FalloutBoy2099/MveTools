param(
	[String]$Configuration = "Release",
	# Defaults to every supported platform: Windows, macOS and Linux on x64 and arm64.
	[String[]]$RuntimeIdentifier = @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"),
	[String]$OutputDirectory = "",
	# Bundle the .NET runtime in the executable (much larger). By default the executable needs .NET 10 installed.
	[Switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($root))
{
	$root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$project = Join-Path $root "MveTools.Cli\MveTools.Cli.csproj"
if (-not (Test-Path -LiteralPath $project))
{
	throw "Project not found: $project"
}

if ([String]::IsNullOrWhiteSpace($OutputDirectory))
{
	$zipRoot = Join-Path $root "publish"
}
else
{
	$zipRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
}

# Accept both "-RuntimeIdentifier a,b" and "-RuntimeIdentifier 'a,b'"; ignore duplicates and blanks.
$runtimeIdentifiers = @(
	$RuntimeIdentifier |
		ForEach-Object { $_ -split "[,;\s]+" } |
		Where-Object { -not [String]::IsNullOrWhiteSpace($_) } |
		Select-Object -Unique
)

foreach ($rid in $runtimeIdentifiers)
{
	if ($rid -notmatch "^(win|osx|linux)-(x64|arm64)$")
	{
		throw "Unsupported runtime identifier '$rid'. Supported: win|osx|linux with x64|arm64 (for example win-x64, osx-arm64, linux-arm64)."
	}
}

New-Item -ItemType Directory -Force -Path $zipRoot | Out-Null
$staging = Join-Path ([IO.Path]::GetTempPath()) "mvetools-gitpublish-$([Guid]::NewGuid().ToString('N'))"

function Publish-Zip
{
	param([String]$Runtime)

	# Windows publishes .exe files; Linux and macOS publish extensionless executable files.
	$extension = if ($Runtime.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { ".exe" } else { "" }
	$baseName = if ($Runtime.StartsWith("linux-", [StringComparison]::OrdinalIgnoreCase)) { "mveconverter" } else { "MveConverter" }

	$publishDirectory = Join-Path $staging "$Runtime-publish"
	$packDirectory = Join-Path $staging "$Runtime-pack"
	New-Item -ItemType Directory -Force -Path $packDirectory | Out-Null

	$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
	$arguments = @(
		"publish", $project,
		"--configuration", $Configuration,
		"--runtime", $Runtime,
		"--self-contained", $selfContainedValue,
		"-p:PublishSingleFile=true",
		"-p:IncludeNativeLibrariesForSelfExtract=true",
		"-p:DebugType=None",
		"-p:DebugSymbols=false",
		"-p:PublishDir=$publishDirectory$([IO.Path]::DirectorySeparatorChar)"
	)

	if ($SelfContained)
	{
		$arguments += "-p:EnableCompressionInSingleFile=true"
	}

	Write-Host "[$Runtime] Publishing single-file executable ($(if ($SelfContained) { 'with' } else { 'without' }) the .NET runtime)..."
	& dotnet @arguments | Out-Host
	if ($LASTEXITCODE -ne 0)
	{
		throw "dotnet publish failed for $Runtime with exit code $LASTEXITCODE."
	}

	$published = Join-Path $publishDirectory "MveTools.Cli$extension"
	if (-not (Test-Path -LiteralPath $published))
	{
		throw "Expected executable was not produced: $published"
	}

	Copy-Item -LiteralPath $published -Destination (Join-Path $packDirectory "$baseName$extension") -Force

	$zipPath = Join-Path $zipRoot "$Runtime.zip"
	Compress-Archive -Path (Join-Path $packDirectory "*") -DestinationPath $zipPath -Force
	Get-Item -LiteralPath $zipPath
}

try
{
	$results = @()
	foreach ($rid in $runtimeIdentifiers)
	{
		$results += Publish-Zip -Runtime $rid
	}
}
finally
{
	if (Test-Path -LiteralPath $staging)
	{
		Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
	}
}

Write-Host ""
Write-Host "Publish complete:"
$results | Select-Object Name, Length, FullName | Format-Table -AutoSize

if ($runtimeIdentifiers | Where-Object { -not $_.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase) })
{
	Write-Host "Note: zip files built on Windows do not keep the Unix executable bit."
	Write-Host "      Users must run 'chmod +x' on the Linux and macOS executables after extracting them."
}