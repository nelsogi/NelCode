# Publie Nel Code Desktop : une version autonome (self-contained, win-x64 : .NET n'a pas besoin d'être
# installé) dans publish\NelCode.Desktop, et un raccourci « Nel Code » dans le menu Démarrer.
#   .\publish.ps1
#   .\publish.ps1 -Output D:\Apps\NelCode
# Les poids de TorchSharp (CUDA) sont lourds : compter quelques Go.
param([string]$Output = (Join-Path $PSScriptRoot "publish\NelCode.Desktop"))

$ErrorActionPreference = "Stop"

dotnet publish (Join-Path $PSScriptRoot "NelCode.Desktop\NelCode.Desktop.csproj") `
    -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué (code $LASTEXITCODE)." }

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
