param(
	[String]$Configuration = "Release",
	# Defaults to every supported platform: Windows, macOS and Linux on x64 and arm64.
	[String[]]$RuntimeIdentifier = @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"),
	[String]$OutputDirectory = "",
	[Switch]$Clean
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
	$publishRoot = Join-Path $root "publish"
}
else
{
	$publishRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
}

# Accept both "-RuntimeIdentifier a,b" and "-RuntimeIdentifier 'a,b'"; ignore duplicates and blanks.
$runtimeIdentifiers = @(
	$RuntimeIdentifier |
		ForEach-Object { $_ -split "[,;\s]+" } |
		Where-Object { -not [String]::IsNullOrWhiteSpace($_) } |
		Select-Object -Unique
)

$supportedPattern = "^(win|osx|linux)-(x64|arm64)$"
foreach ($rid in $runtimeIdentifiers)
{
	if ($rid -notmatch $supportedPattern)
	{
		throw "Unsupported runtime identifier '$rid'. Supported: win|osx|linux with x64|arm64 (for example win-x64, osx-arm64, linux-arm64)."
	}
}

function Publish-Executable
{
	param(
		[String]$Runtime,
		[Boolean]$SelfContained,
		[String]$PublishDirectory
	)

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
		"-p:PublishDir=$PublishDirectory$([IO.Path]::DirectorySeparatorChar)"
	)

	if ($SelfContained)
	{
		$arguments += "-p:EnableCompressionInSingleFile=true"
	}

	& dotnet @arguments | Out-Host

	if ($LASTEXITCODE -ne 0)
	{
		throw "dotnet publish failed for $Runtime with exit code $LASTEXITCODE."
	}
}

function Publish-Runtime
{
	param([String]$Runtime)

	$output = Join-Path $publishRoot $Runtime
	$staging = Join-Path $output "_staging"
	$frameworkStaging = Join-Path $staging "framework-dependent"
	$selfContainedStaging = Join-Path $staging "self-contained"
	$frameworkOutput = Join-Path $output "without-dotnet"
	$selfContainedOutput = Join-Path $output "with-dotnet"
	# Windows publishes .exe files; Linux and macOS publish extensionless executable files.
	$executableExtension = if ($Runtime.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { ".exe" } else { "" }
	$executableBaseName = if ($Runtime.StartsWith("linux-", [StringComparison]::OrdinalIgnoreCase)) { "mveconverter" } else { "MveConverter" }
	$executableName = "$executableBaseName$executableExtension"

	if ($Clean -and (Test-Path -LiteralPath $output))
	{
		Remove-Item -LiteralPath $output -Recurse -Force
	}

	New-Item -ItemType Directory -Force -Path $output | Out-Null
	New-Item -ItemType Directory -Force -Path $frameworkOutput | Out-Null
	New-Item -ItemType Directory -Force -Path $selfContainedOutput | Out-Null
	if (Test-Path -LiteralPath $staging)
	{
		Remove-Item -LiteralPath $staging -Recurse -Force
	}

	Write-Host "[$Runtime] Publishing framework-dependent single-file executable..."
	Publish-Executable -Runtime $Runtime -SelfContained $false -PublishDirectory $frameworkStaging

	Write-Host "[$Runtime] Publishing self-contained single-file executable with .NET 10 runtime..."
	Publish-Executable -Runtime $Runtime -SelfContained $true -PublishDirectory $selfContainedStaging

	$frameworkExe = Join-Path $frameworkStaging "MveTools.Cli$executableExtension"
	$selfContainedExe = Join-Path $selfContainedStaging "MveTools.Cli$executableExtension"
	$frameworkTarget = Join-Path $frameworkOutput $executableName
	$selfContainedTarget = Join-Path $selfContainedOutput $executableName

	if (-not (Test-Path -LiteralPath $frameworkExe))
	{
		throw "Expected executable was not produced: $frameworkExe"
	}

	if (-not (Test-Path -LiteralPath $selfContainedExe))
	{
		throw "Expected executable was not produced: $selfContainedExe"
	}

	Copy-Item -LiteralPath $frameworkExe -Destination $frameworkTarget -Force
	Copy-Item -LiteralPath $selfContainedExe -Destination $selfContainedTarget -Force
	Remove-Item -LiteralPath $staging -Recurse -Force

	Get-Item -LiteralPath $frameworkTarget
	Get-Item -LiteralPath $selfContainedTarget
}

$results = @()
foreach ($rid in $runtimeIdentifiers)
{
	$results += Publish-Runtime -Runtime $rid
}

Write-Host ""
Write-Host "Publish complete:"
$results | Select-Object @{ Name = "Name"; Expression = { $_.Name } }, @{ Name = "Length"; Expression = { $_.Length } }, FullName | Format-Table -AutoSize

if ($runtimeIdentifiers | Where-Object { -not $_.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase) })
{
	Write-Host "Note: the Linux and macOS files are not marked executable when published from Windows."
	Write-Host "      Run 'chmod +x' on them after copying (tar/zip them from a Unix shell to keep the mode)."
}
