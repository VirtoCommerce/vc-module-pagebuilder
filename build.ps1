$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$webProject = Join-Path $repoRoot 'src\VirtoCommerce.PageBuilderModule.Web'
$appsDir = Join-Path $webProject 'Apps'
$shellDir = Join-Path $appsDir 'page-builder-shell'
$contentDir = Join-Path $webProject 'Content'

# 1. Web project: npm install + webpack build
Write-Host '=== Web project: npm install ===' -ForegroundColor Cyan
Push-Location $webProject
try {
    npm i
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }

    Write-Host '=== Web project: webpack build ===' -ForegroundColor Cyan
    npm run webpack:build
    if ($LASTEXITCODE -ne 0) { throw 'webpack build failed' }
}
finally { Pop-Location }

# 2. Shell: yarn install + build
Write-Host '=== Shell: yarn install ===' -ForegroundColor Cyan
Push-Location $shellDir
try {
    yarn
    if ($LASTEXITCODE -ne 0) { throw 'shell yarn install failed' }

    Write-Host '=== Shell: yarn build ===' -ForegroundColor Cyan
    yarn build
    if ($LASTEXITCODE -ne 0) { throw 'shell yarn build failed' }
}
finally { Pop-Location }

# 3. Create Content directory and symlinks
Write-Host '=== Creating Content symlinks ===' -ForegroundColor Cyan
if (-not (Test-Path $contentDir)) {
    New-Item -ItemType Directory -Path $contentDir | Out-Null
}

$shellLink = Join-Path $contentDir 'page-builder-shell'
$shellDist = Join-Path $shellDir 'dist'

if (Test-Path $shellLink) { Remove-Item $shellLink -Force }
New-Item -ItemType SymbolicLink -Path $shellLink -Target $shellDist | Out-Null

Write-Host "  $shellLink -> $shellDist"

# 4. Build .NET solution
Write-Host '=== Building solution ===' -ForegroundColor Cyan
Push-Location $repoRoot
try {
    dotnet build VirtoCommerce.PageBuilderModule.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
}
finally { Pop-Location }

Write-Host '=== Build completed successfully ===' -ForegroundColor Green
