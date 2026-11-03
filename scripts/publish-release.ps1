#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = '0.6.0'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\MdEditor\MdEditor.csproj'
$artifactRoot = Join-Path $root 'artifacts'

if (-not (Test-Path -LiteralPath $project)) {
    throw "找不到專案：$project"
}

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
        "-p:Platform=$($target.Platform)",
        "-p:PlatformTarget=$($target.Platform)",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$Version.0",
        "-p:FileVersion=$Version.0",
        '-o', $output,
        '--nologo'
    )

    & dotnet @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "發佈失敗：$($target.Rid)"
    }

    $readme = @"
Markdown 編輯器 $Version（$($target.Rid)，非 Self-Contained）

執行前請先安裝下列執行環境（需與 App 相同架構）：
1. .NET 10 Runtime（Microsoft.NETCore.App 10.0）
2. Windows App Runtime 1.8（Windows App SDK）
3. Microsoft Edge WebView2 Evergreen Runtime

安裝完成後執行 MdEditor.exe。
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
