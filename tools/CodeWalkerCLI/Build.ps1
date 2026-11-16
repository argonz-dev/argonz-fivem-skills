param(
    [string] $CodeWalkerBin = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($CodeWalkerBin)) {
    $defaultBin = Join-Path $PSScriptRoot '..\CodeWalker\bin\Debug\net48'
    if (Test-Path -LiteralPath $defaultBin -PathType Container) {
        $CodeWalkerBin = (Resolve-Path -LiteralPath $defaultBin).Path
    } else {
        throw "CodeWalkerBin not specified and default Debug\net48 bin not found: $defaultBin"
    }
}

$compilerDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $compilerDir 'csc.exe'
$facade = Join-Path $compilerDir 'netstandard.dll'
$dependencies = 'CodeWalker.Core.dll', 'SharpDX.dll', 'SharpDX.Mathematics.dll'
foreach ($name in $dependencies) {
    $source = Join-Path $CodeWalkerBin $name
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Missing CodeWalker dependency: $source"
    }
}
foreach ($name in $dependencies) {
    Copy-Item -LiteralPath (Join-Path $CodeWalkerBin $name) -Destination $PSScriptRoot -Force
}

$stringsSource = Join-Path $CodeWalkerBin 'strings.txt'
if (!(Test-Path -LiteralPath $stringsSource -PathType Leaf)) {
    $fallbackStrings = Join-Path $PSScriptRoot '..\CodeWalker.Core\strings.txt'
    if (Test-Path -LiteralPath $fallbackStrings -PathType Leaf) {
        $stringsSource = $fallbackStrings
    } else {
        $stringsSource = $null
    }
}
if ($stringsSource) {
    Copy-Item -LiteralPath $stringsSource -Destination (Join-Path $PSScriptRoot 'strings.txt') -Force
}

Push-Location $PSScriptRoot
try {
    & $compiler /nologo /optimize+ /target:exe /platform:x64 /out:codewalker-cli.exe /r:CodeWalker.Core.dll "/r:$facade" /r:System.Web.Extensions.dll Program.cs
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
