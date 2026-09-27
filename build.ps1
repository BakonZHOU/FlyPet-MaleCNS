$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish FlyPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o dist --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Copy-Item -LiteralPath README.md -Destination 'dist/使用说明.md' -Force
    Copy-Item -LiteralPath THIRD_PARTY.md -Destination 'dist/THIRD_PARTY.md' -Force
    Copy-Item -LiteralPath licenses -Destination dist -Recurse -Force
    Write-Host 'Ready: dist/FlyPet.exe'
} finally { Pop-Location }
