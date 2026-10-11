param(
	[String]$Configuration = "Release",
	[String]$RuntimeIdentifier = "win-x64",
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

$output = Join-Path $publishRoot $RuntimeIdentifier
$staging = Join-Path $output "_staging"
$frameworkStaging = Join-Path $staging "framework-dependent"
$selfContainedStaging = Join-Path $staging "self-contained"
$frameworkOutput = Join-Path $output "without-dotnet"
$selfContainedOutput = Join-Path $output "with-dotnet"
# Windows publishes .exe files; Linux and macOS publish extensionless executable files.
$executableExtension = if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { ".exe" } else { "" }
$executableBaseName = if ($RuntimeIdentifier.StartsWith("linux-", [StringComparison]::OrdinalIgnoreCase)) { "mveconverter" } else { "MveConverter" }
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

function Publish-Executable
{
	param(
		[Boolean]$SelfContained,
		[String]$PublishDirectory
	)

	$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
	$arguments = @(
		"publish", $project,
		"--configuration", $Configuration,
		"--runtime", $RuntimeIdentifier,
		"--self-contained", $selfContainedValue,
		"-p:PublishSingleFile=true",
		"-p:IncludeNativeLibrariesForSelfExtract=true",
		"-p:DebugType=None",
		"-p:DebugSymbols=false",
		"-p:PublishDir=$PublishDirectory\"
	)

	if ($SelfContained)
	{
		$arguments += "-p:EnableCompressionInSingleFile=true"
	}

	& dotnet @arguments

	if ($LASTEXITCODE -ne 0)
	{
		throw "dotnet publish failed with exit code $LASTEXITCODE."
	}
}

Write-Host "Publishing framework-dependent single-file executable..."
Publish-Executable -SelfContained $false -PublishDirectory $frameworkStaging

Write-Host "Publishing self-contained single-file executable with .NET 10 runtime..."
Publish-Executable -SelfContained $true -PublishDirectory $selfContainedStaging

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

$results = @(
	Get-Item -LiteralPath $frameworkTarget
	Get-Item -LiteralPath $selfContainedTarget
)

Write-Host ""
Write-Host "Publish complete:"
$results | Select-Object Name, Length, FullName | Format-Table -AutoSize