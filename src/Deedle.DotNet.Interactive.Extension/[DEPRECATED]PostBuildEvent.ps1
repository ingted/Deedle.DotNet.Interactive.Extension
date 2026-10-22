param(
    [string]$PackageId = "Deedle.DotNet.Interactive.Extension",
    [string]$Configuration = "Release",
    [string]$PackageOutputPath = "bin\Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Configuration -ne "Release") {
    return
}

$apiKeyFile =
    if ($env:NUGET_API_KEY_FILE) {
        $env:NUGET_API_KEY_FILE
    } elseif (Test-Path -LiteralPath "/Nuget/apikey.txt") {
        "/Nuget/apikey.txt"
    } else {
        "G:\Nuget\apikey.txt"
    }

function Find-LibPacksFile {
    param([string]$StartDirectory)

    $currentDirectory = [System.IO.DirectoryInfo]::new($StartDirectory)

    while ($null -ne $currentDirectory) {
        $candidate = Join-Path -Path $currentDirectory.FullName -ChildPath "lib-packs.txt"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }

        $currentDirectory = $currentDirectory.Parent
    }

    throw "lib-packs.txt was not found from '$StartDirectory' or any parent directory."
}

function Get-Sha256 {
    param([string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString($sha256.ComputeHash($stream)).Replace("-", "")
        } finally {
            $sha256.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

$resolvedOutputPath =
    if ([System.IO.Path]::IsPathRooted($PackageOutputPath)) {
        $PackageOutputPath
    } else {
        Join-Path -Path $PSScriptRoot -ChildPath $PackageOutputPath
    }

if (-not (Test-Path -LiteralPath $resolvedOutputPath -PathType Container)) {
    throw "Package output directory does not exist: $resolvedOutputPath"
}

$package =
    Get-ChildItem -LiteralPath $resolvedOutputPath -Filter "$PackageId.*.nupkg" -File |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
        Sort-Object -Property LastWriteTimeUtc -Descending |
        Select-Object -First 1

if ($null -eq $package) {
    throw "No package matching '$PackageId.*.nupkg' was found in '$resolvedOutputPath'."
}

if (-not (Test-Path -LiteralPath $apiKeyFile -PathType Leaf)) {
    throw "NuGet API key file does not exist. Set NUGET_API_KEY_FILE or provide G:\Nuget\apikey.txt."
}

$apiKey = [System.IO.File]::ReadAllText($apiKeyFile).Trim()
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "NuGet API key file is empty."
}

$libPacksFile = Find-LibPacksFile -StartDirectory $PSScriptRoot
$libPacksDirectory = [System.IO.File]::ReadAllText($libPacksFile).Trim()
if (-not (Test-Path -LiteralPath $libPacksDirectory -PathType Container)) {
    throw "Local library-packs directory does not exist: $libPacksDirectory"
}

$localPackagePath = Join-Path -Path $libPacksDirectory -ChildPath $package.Name
Copy-Item -LiteralPath $package.FullName -Destination $localPackagePath -Force

$sourceHash = Get-Sha256 -Path $package.FullName
$localHash = Get-Sha256 -Path $localPackagePath
if ($sourceHash -ne $localHash) {
    throw "SHA-256 mismatch after copying '$($package.Name)' to local library-packs."
}

Write-Host "[PostBuild] Local package synchronized: $localPackagePath"
Write-Host "[PostBuild] Publishing $($package.Name) to NuGet.org"

& dotnet nuget push $package.FullName `
    --api-key $apiKey `
    --source "https://api.nuget.org/v3/index.json" `
    --skip-duplicate

if ($LASTEXITCODE -ne 0) {
    throw "dotnet nuget push failed with exit code $LASTEXITCODE."
}
