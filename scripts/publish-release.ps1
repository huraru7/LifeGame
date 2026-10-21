# リリース版を出力する。出力先のフォルダの LifeGame.App.exe をダブルクリックすれば起動できる。
# 記録と保存したパターンは %APPDATA%\LifeGame にあるので、出力し直しても消えない。
# 使い方: powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1
param(
    [string]$Output = "C:\15_MyProjects\Releases\LifeGame"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\LifeGame.App\LifeGame.App.csproj"

# 古いファイルが残らないよう、出力先を空にしてから出力する
if (Test-Path $Output) {
    Remove-Item (Join-Path $Output "*") -Recurse -Force
}

dotnet publish $project -c Release -o $Output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish に失敗しました(終了コード $LASTEXITCODE)。"
}

Write-Host "リリース版を出力しました: $Output"
