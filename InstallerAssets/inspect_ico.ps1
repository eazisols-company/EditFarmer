
$path = "c:\Users\user\Desktop\Eazisols\Carrot Download\InstallerAssets\carrot_square.ico"

if (-not (Test-Path $path)) { Write-Error "File not found"; exit }

$bytes = [System.IO.File]::ReadAllBytes($path)
$count = [BitConverter]::ToInt16($bytes, 4)

Write-Host "Icon Count: $count"

for ($i = 0; $i -lt $count; $i++) {
    $offset = 6 + ($i * 16)
    $w = $bytes[$offset]
    $h = $bytes[$offset+1]
    if ($w -eq 0) { $w = 256 }
    if ($h -eq 0) { $h = 256 }
    Write-Host "Image $i : $w x $h"
}
