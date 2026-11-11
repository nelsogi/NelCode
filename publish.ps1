# Publie Nel Code Desktop : une version autonome (self-contained, win-x64 : .NET n'a pas besoin d'être
# installé) dans publish\NelCode.Desktop, puis :
#   .\publish.ps1                            un raccourci « Nel Code » dans le menu Démarrer
#   .\publish.ps1 -Msi                       l'installateur dans publish\msi (le .msi et ses .cab)
#   .\publish.ps1 -Output D:\Apps\NelCode    ailleurs (sans -Msi)
# Les poids de TorchSharp (CUDA) sont lourds : compter quelques Go.
param(
    [string]$Output = (Join-Path $PSScriptRoot "publish\NelCode.Desktop"),
    [switch]$Msi
)

$ErrorActionPreference = "Stop"

dotnet publish (Join-Path $PSScriptRoot "NelCode.Desktop\NelCode.Desktop.csproj") `
    -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué (code $LASTEXITCODE)." }

if ($Msi) {
    # L'installateur crée lui-même son raccourci : pas de raccourci vers le dossier publish.
    $msiDir = Join-Path $PSScriptRoot "publish\msi"
    dotnet build (Join-Path $PSScriptRoot "NelCode.Installer\NelCode.Installer.wixproj") `
        -c Release -p:PublishDir=$Output -o $msiDir
    if ($LASTEXITCODE -ne 0) { throw "La construction du MSI a échoué (code $LASTEXITCODE)." }
    Write-Host "Installateur dans $msiDir (garder le .msi et les .cab ensemble)"
    return
}

$exe = Join-Path $Output "NelCode.Desktop.exe"
$shortcut = Join-Path ([Environment]::GetFolderPath("Programs")) "Nel Code.lnk"
$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = [Environment]::GetFolderPath("UserProfile")   # dossier de travail au lancement
$link.IconLocation = "$exe,0"
$link.Description = "Nel Code : discuter avec les modèles MiniChat"
$link.Save()

Write-Host "Publié dans $Output"
Write-Host "Raccourci : $shortcut"
