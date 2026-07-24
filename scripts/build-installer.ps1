param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Manufacturer = "NodeTie"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sharedVersionFile = Join-Path $repoRoot "Directory.Build.props"

function Get-SharedNodeTieVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PropsFilePath
    )

    if (-not (Test-Path $PropsFilePath)) {
        throw "Shared version file not found: $PropsFilePath"
    }

    [xml]$props = Get-Content -Path $PropsFilePath
    $sharedVersion = $props.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($sharedVersion)) {
        throw "Version was not found in $PropsFilePath"
    }

    return $sharedVersion.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-SharedNodeTieVersion -PropsFilePath $sharedVersionFile
}

$publishDir = Join-Path $repoRoot "artifacts\publish\$Version"
$installerOutDir = Join-Path $repoRoot "artifacts\installer\$Version"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerOutDir | Out-Null

Write-Host "Publishing NodeTie $Version to $publishDir"
dotnet publish (Join-Path $repoRoot "NodeTie.csproj") `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Building MSI for NodeTie $Version"
dotnet build (Join-Path $repoRoot "Installer\NodeTie.Installer.wixproj") `
    -t:Rebuild `
    -c $Configuration `
    -p:ProductVersion=$Version `
    -p:Manufacturer=$Manufacturer `
    -p:PublishDir=$publishDir `
    -p:OutputPath=$installerOutDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build (installer) failed with exit code $LASTEXITCODE"
}

Write-Host "Installer build complete."
Write-Host "MSI output folder: $installerOutDir"