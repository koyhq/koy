# Koy paketleme ve kurulum betigi (Velopack)
# Kullanim: powershell -ExecutionPolicy Bypass -File scripts\pack.ps1 [-Version 0.1.0] [-CertFile path.pfx -CertPassword ...] [-SignParams "..."]

param(
    [string]$Version = "",
    [string]$SignParams = $env:KOY_SIGN_PARAMS,
    [string]$CertFile = $env:KOY_SIGN_CERT_FILE,
    [string]$CertPassword = $env:KOY_SIGN_CERT_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

# Surum verilmediyse Directory.Build.props'taki surumu kullan (release-please bunu gunceller).
if (-not $Version) {
    $versionMatch = [regex]::Match((Get-Content (Join-Path $root 'Directory.Build.props') -Raw), '<Version>([^<]+)</Version>')
    if (-not $versionMatch.Success) { throw 'Directory.Build.props icinde <Version> bulunamadi; -Version ver.' }
    $Version = $versionMatch.Groups[1].Value
}

# Native komutlar $ErrorActionPreference'a takilmaz; cikis kodunu elle denetle.
function Assert-LastExit([string]$what) {
    if ($LASTEXITCODE -ne 0) {
        throw "$what basarisiz oldu (cikis kodu $LASTEXITCODE)."
    }
}

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $sdkTool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($sdkTool) {
        # vpk --signParams signtool.exe'yi PATH uzerinden aradigi icin klasoru PATH'e ekle.
        $env:PATH = "$($sdkTool.DirectoryName);$env:PATH"
        return $sdkTool.FullName
    }
    throw "Kod imzalama istendi ancak signtool.exe bulunamadi (Windows SDK Signing Tools gerekli)."
}

function Invoke-CodeSign([string]$signTool, [string]$signArgs, [string]$filePath) {
    $proc = Start-Process -FilePath $signTool -ArgumentList "sign $signArgs `"$filePath`"" -Wait -NoNewWindow -PassThru
    if ($proc.ExitCode -ne 0) {
        throw "signtool ($filePath) basarisiz oldu (cikis kodu $($proc.ExitCode))."
    }
}

$resolvedSignParams = $SignParams
if (-not $resolvedSignParams -and $CertFile) {
    if (-not (Test-Path $CertFile)) {
        throw "Sertifika dosyasi bulunamadi: $CertFile"
    }
    $resolvedCertPath = (Resolve-Path $CertFile).Path
    $resolvedSignParams = "/fd SHA256 /tr $TimestampUrl /td SHA256 /f `"$resolvedCertPath`""
    if ($CertPassword) {
        $resolvedSignParams += " /p `"$CertPassword`""
    }
}

$signToolPath = $null
if ($resolvedSignParams) {
    $signToolPath = Find-SignTool
}

Write-Host "==> Koy v$Version derleniyor ve yayin klasorune cikariliyor..." -ForegroundColor Cyan

$publishDir = Join-Path $root 'publish\win-x64'
$distDir = Join-Path $root 'dist'

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

dotnet publish src/App/Koy.App.csproj -c Release -r win-x64 --self-contained false -o $publishDir
Assert-LastExit 'dotnet publish'

# Gozcu surec (Native AOT, C++ derleme araclari gerekir): Koy ile ayni klasore yalniz exe kopyalanir.
$watchdogDir = Join-Path $root 'publish\watchdog'
if (Test-Path $watchdogDir) { Remove-Item $watchdogDir -Recurse -Force }
dotnet publish src/Watchdog/Koy.Watchdog.csproj -c Release -r win-x64 -o $watchdogDir
Assert-LastExit 'dotnet publish (Koy.Watchdog)'
Copy-Item (Join-Path $watchdogDir 'Koy.Watchdog.exe') $publishDir -Force

if ($resolvedSignParams) {
    Write-Host "==> Ikili dosyalar imzalaniyor (Koy.exe, Koy.Watchdog.exe)..." -ForegroundColor Cyan
    Invoke-CodeSign $signToolPath $resolvedSignParams (Join-Path $publishDir 'Koy.exe')
    Invoke-CodeSign $signToolPath $resolvedSignParams (Join-Path $publishDir 'Koy.Watchdog.exe')
}

Write-Host "==> Velopack paketi (vpk pack) olusturuluyor..." -ForegroundColor Cyan

# vpk surumu Velopack paket surumuyle ayni olmali (csproj'daki surum tek kaynak)
$vpkVersion = & (Join-Path $PSScriptRoot 'get-velopack-version.ps1')
$vpkLine = dotnet tool list -g | Select-String '^vpk\s+(\S+)'
$vpkHave = if ($vpkLine) { $vpkLine.Matches[0].Groups[1].Value } else { $null }
if ($vpkHave -ne $vpkVersion) {
    Write-Host "vpk $vpkVersion kuruluyor (mevcut: $vpkHave)..." -ForegroundColor Yellow
    if ($vpkHave) {
        dotnet tool update -g vpk --version $vpkVersion | Out-Null
    } else {
        dotnet tool install -g vpk --version $vpkVersion | Out-Null
    }
    Assert-LastExit 'vpk kurulumu'
}

$vpkSignArgs = @()
if ($resolvedSignParams) {
    $vpkSignArgs = @('--signParams', $resolvedSignParams)
}

vpk pack --packId Koy `
         --packVersion $Version `
         --packDir $publishDir `
         --mainExe Koy.exe `
         --packTitle "Koy" `
         --packAuthors "Burhan Şahin" `
         --icon (Join-Path $root "src\App\Resources\app.ico") `
         --runtime win-x64 `
         --outputDir $distDir `
         @vpkSignArgs
Assert-LastExit 'vpk pack'

Write-Host "==> Portable zip paketi olusturuluyor..." -ForegroundColor Cyan
Compress-Archive -Path "$publishDir\*" -DestinationPath (Join-Path $distDir "Koy-win-Portable.zip") -Force

Write-Host "==> SHA256 karmalari hesaplaniyor..." -ForegroundColor Cyan
$sumsFile = Join-Path $distDir "SHA256SUMS.txt"
if (Test-Path $sumsFile) { Remove-Item $sumsFile -Force }

Get-ChildItem -Path $distDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)" | Out-File -FilePath $sumsFile -Append -Encoding utf8
}

Write-Host "==> Paketleme tamamlandi. Ciktilar: $distDir" -ForegroundColor Green
Get-Content $sumsFile
