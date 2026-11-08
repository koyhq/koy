# Surum PR'inda yeni surumun basligi iki degisiklik gunlugunde de olmali.
# CHANGELOG.md release-please ile uretilir; CHANGELOG.tr.md ayni PR'da elle eklenir.
# Kullanim: powershell -ExecutionPolicy Bypass -File scripts\check-changelogs.ps1 [-Version 0.3.0]

param(
    [string]$Version = "",
    [string]$Root = ""
)

$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = Split-Path $PSScriptRoot -Parent }

if (-not $Version) {
    $manifest = Get-Content (Join-Path $Root '.release-please-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $Version = $manifest.'.'
}

$pattern = '(?m)^##\s+\[?v?' + [regex]::Escape($Version) + '\]?(?:[\s(]|$)'
$missing = @()
foreach ($file in 'CHANGELOG.md', 'CHANGELOG.tr.md') {
    $text = Get-Content (Join-Path $Root $file) -Raw -Encoding UTF8
    if ($text -notmatch $pattern) { $missing += $file }
}

if ($missing.Count -gt 0) {
    Write-Host "Surum $Version icin not eksik: $($missing -join ', ')"
    Write-Host "Surum PR'inda Turkce notlari CHANGELOG.tr.md dosyasina ekleyin."
    exit 1
}

Write-Host "Surum $Version iki degisiklik gunlugunde de var."
