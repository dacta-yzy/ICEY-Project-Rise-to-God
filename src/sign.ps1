param(
    [string]$Dist = "$PSScriptRoot\..\dist",
    [string]$ThumbprintFile = "$PSScriptRoot\..\signing\leaf-thumb.txt",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
if (-not $signtool) { throw "signtool.exe not found. Install Windows SDK." }

$thumb = if (Test-Path $ThumbprintFile) { (Get-Content $ThumbprintFile -Raw).Trim() } else { "" }
if (-not $thumb) { throw "leaf certificate thumbprint not found: $ThumbprintFile" }

$cert = Get-Item "Cert:\CurrentUser\My\$thumb" -ErrorAction SilentlyContinue
if (-not $cert) { throw "certificate $thumb not in CurrentUser\My" }

$targets = @("$Dist\ICEY_Tuner.exe", "$Dist\ICEY_Tuner.dll") | Where-Object { Test-Path $_ }
if (-not $targets) { throw "nothing to sign in $Dist" }

foreach ($f in $targets) {
    Write-Host "Signing $f ..." -ForegroundColor Cyan
    & $signtool.FullName sign /fd SHA256 /sha1 $thumb /tr $TimestampUrl /td SHA256 $f
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "timestamped signing failed, retry without timestamp..."
        & $signtool.FullName sign /fd SHA256 /sha1 $thumb $f
    }
    & $signtool.FullName verify /pa $f | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host "  OK verified" -ForegroundColor Green }
    else { Write-Warning "  verify failed" }
}

Get-AuthenticodeSignature $targets | Format-Table @{n='File';e={Split-Path $_.Path -Leaf}}, Status, @{n='Signer';e={$_.SignerCertificate.Subject}} -AutoSize
