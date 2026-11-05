#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\MdEditor\MdEditor.csproj'
$propsFile = Join-Path $root 'Directory.Build.props'
$licenseFile = Join-Path $root 'LICENSE'
$noticesFile = Join-Path $root 'THIRD-PARTY-NOTICES.txt'
$artifactRoot = Join-Path $root 'artifacts'

if (-not (Test-Path -LiteralPath $project)) {
    throw "找不到專案：$project"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (-not (Test-Path -LiteralPath $propsFile)) {
        throw "找不到版本設定檔：$propsFile"
    }

    [xml]$propsXml = Get-Content -LiteralPath $propsFile -Raw -Encoding UTF8
    $Version = ($propsXml.Project.PropertyGroup.Version | Select-Object -First 1).Trim()
}

$Version = $Version.TrimStart('v', 'V')
$parsedVersion = [Version]::Parse($Version)
if ($parsedVersion.Build -lt 0) {
    throw "版號格式需為 Major.Minor.Patch（例如 1.0.0），目前為：$Version"
}

$assemblyVersion = "$($parsedVersion.Major).$($parsedVersion.Minor).0.0"
$fileVersion = "$($parsedVersion.Major).$($parsedVersion.Minor).$($parsedVersion.Build).0"

$targets = @(
    [pscustomobject]@{ Rid = 'win-x64'; Platform = 'x64' },
    [pscustomobject]@{ Rid = 'win-arm64'; Platform = 'arm64' }
)

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

foreach ($target in $targets) {
    $output = Join-Path $artifactRoot $target.Rid

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    New-Item -ItemType Directory -Path $output -Force | Out-Null

    $arguments = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $target.Rid,
        '--self-contained', 'false',
        '-p:WindowsAppSDKSelfContained=false',
        '-p:WindowsPackageType=None',
        '-p:DebugSymbols=false',
        '-p:DebugType=none',
        "-p:Platform=$($target.Platform)",
        "-p:PlatformTarget=$($target.Platform)",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$assemblyVersion",
        "-p:FileVersion=$fileVersion",
        '-o', $output,
        '--nologo'
    )

    & dotnet @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "發佈失敗：$($target.Rid)"
    }

    Get-ChildItem -LiteralPath $output -Filter '*.pdb' -Recurse -File | Remove-Item -Force

    if (Test-Path -LiteralPath $licenseFile) {
        Copy-Item -LiteralPath $licenseFile -Destination (Join-Path $output 'LICENSE.txt') -Force
    }

    if (Test-Path -LiteralPath $noticesFile) {
        Copy-Item -LiteralPath $noticesFile -Destination (Join-Path $output 'THIRD-PARTY-NOTICES.txt') -Force
    }

    $readme = @"
Markdown 編輯器 v$Version（$($target.Rid)，非 Self-Contained）

執行前請先安裝下列執行環境（需與 App 相同架構）：
1. .NET 10 Runtime（Microsoft.NETCore.App 10.0）
2. Windows App Runtime 1.8（Windows App SDK）
3. Microsoft Edge WebView2 Evergreen Runtime

安裝完成後解壓縮至任意資料夾並執行 MdEditor.exe（無須系統管理員權限）。
授權條款請見 LICENSE.txt 與 THIRD-PARTY-NOTICES.txt。
更多說明：https://github.com/mark216tw/win-mdeditor-01
"@
    [IO.File]::WriteAllText(
        (Join-Path $output '安裝說明.txt'),
        $readme,
        (New-Object System.Text.UTF8Encoding($true)))

    $zip = Join-Path $artifactRoot "MdEditor-$Version-$($target.Rid).zip"

    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }

    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal

    Write-Host "已建立 $zip"
}
