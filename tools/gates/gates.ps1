# 实体重构验收门 G1-G7（docs/entity-refactor-plan.md §6 + G7 实体层不变量）
# 用法:  pwsh ./tools/gates/gates.ps1 [-Gate all|G1|G2|G3|G4|G5|G6] [-Repo <repoRoot>]
# 语义:  每门输出 PASS / FAIL(<原因>) / BLOCKED(<缺依赖>)；任一 FAIL/BLOCKED 则退出码非 0。
#        无 fixture 时 G2/G3/G4 报 BLOCKED，不得报绿（§6 BLOCKED 语义）。
param(
    [string]$Gate = "all",
    [string]$Repo = ""
)

$ErrorActionPreference = "Stop"
if (-not $Repo) { $Repo = (Resolve-Path "$PSScriptRoot/../..").Path }
Set-Location $Repo

$script:results = @()

function Add-Result([string]$name, [string]$status, [string]$detail) {
    $script:results += [pscustomobject]@{ Gate = $name; Status = $status; Detail = $detail }
    Write-Host ("[{0}] {1}  {2}" -f $status, $name, $detail)
}

function Get-Fixtures {
    $manifest = Join-Path $Repo "out/base/manifest.json"
    if (-not (Test-Path $manifest)) { return $null }
    try { return (Get-Content $manifest -Raw -Encoding UTF8 | ConvertFrom-Json).fixtures }
    catch { return $null }
}

function Invoke-G1 {
    $out = dotnet build DwgParser.sln -warnaserror --no-incremental 2>&1
    $failed = ($LASTEXITCODE -ne 0) -or ($out -match ": error ")
    $warns = ($out | Select-String ": warning ").Count
    if ($failed -or $warns -gt 0) {
        Add-Result "G1" "FAIL" ("build errors/warnings={0}" -f $warns)
    } else {
        Add-Result "G1" "PASS" "dotnet build -warnaserror --no-incremental"
    }
}

function Invoke-G2 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G2" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G2" "BLOCKED" "CLI 未构建"; return }
    $bad = @()
    foreach ($f in $fixtures) {
        $src = $f.source
        if (-not (Test-Path $src)) { $bad += "$($f.stem):fixture缺失(BLOCKED)"; continue }
        $outJson = "out/gate/$($f.stem).json"
        & $cli $src --json $outJson *> $null
        if ($LASTEXITCODE -ne 0) { $bad += "$($f.stem):run rc=$LASTEXITCODE"; continue }
        $base = Join-Path $Repo $f.json
        if ((Get-FileHash $outJson -Algorithm SHA256).Hash -ne (Get-FileHash $base -Algorithm SHA256).Hash) {
            $bad += "$($f.stem):json≠基线"
        }
    }
    if ($bad.Count -eq 0) { Add-Result "G2" "PASS" "JSON 结构快照=基线" }
    else { Add-Result "G2" "FAIL" ($bad -join "; ") }
}

function Invoke-G3 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G3" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G3" "BLOCKED" "CLI 未构建"; return }
    $bad = @()
    foreach ($f in $fixtures) {
        $src = $f.source
        if (-not (Test-Path $src)) { $bad += "$($f.stem):fixture缺失(BLOCKED)"; continue }
        $outSvg = "out/gate/$($f.stem).svg"
        & $cli $src --svg $outSvg *> $null
        if ($LASTEXITCODE -ne 0) { $bad += "$($f.stem):run rc=$LASTEXITCODE"; continue }
        $base = Join-Path $Repo $f.svg
        if ((Get-FileHash $outSvg -Algorithm SHA256).Hash -ne (Get-FileHash $base -Algorithm SHA256).Hash) {
            $bad += "$($f.stem):svg≠基线"
        }
    }
    if ($bad.Count -eq 0) { Add-Result "G3" "PASS" "SVG 渲染快照=基线" }
    else { Add-Result "G3" "FAIL" ($bad -join "; ") }
}

function Invoke-G4 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G4" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G4" "BLOCKED" "CLI 未构建"; return }
    $bad = @()
    foreach ($f in $fixtures) {
        $src = $f.source
        if (-not (Test-Path $src)) { $bad += "$($f.stem):fixture缺失(BLOCKED)"; continue }
        $outDwg = "out/gate/$($f.stem).conv.dwg"
        & $cli $src --convert $f.sourceVersion --output $outDwg *> $null
        if ($LASTEXITCODE -ne 0) { $bad += "$($f.stem):run rc=$LASTEXITCODE"; continue }
        if ((Get-FileHash $outDwg -Algorithm SHA256).Hash -ne (Get-FileHash $src -Algorithm SHA256).Hash) {
            $bad += "$($f.stem):convert≠源"
        }
    }
    if ($bad.Count -eq 0) { Add-Result "G4" "PASS" "同版本 convert 逐字节=源" }
    else { Add-Result "G4" "FAIL" ($bad -join "; ") }
}

function Invoke-G5 {
    $bad = @()
    Get-ChildItem -Recurse -Include *.cs -Path "DwgParser.Core","DwgParser.Rendering","DwgParser.Cli","DwgParser.WinForms" |
        ForEach-Object {
            $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
            if ($lines -gt 800) { $bad += ("{0}={1}行" -f $_.Name, $lines) }
        }
    if ($bad.Count -eq 0) { Add-Result "G5" "PASS" "全仓 *.cs ≤ 800 行" }
    else { Add-Result "G5" "FAIL" ($bad -join "; ") }
}

function Invoke-G6 {
    $bad = @()
    $coreRefs = Get-ChildItem -Recurse -Include *.cs -Path "DwgParser.Core" |
        Select-String -Pattern "using DwgParser\.Rendering" 
    if ($coreRefs) { $bad += ("Core 引用 Rendering: " + ($coreRefs.Path | Select-Object -First 3 -Unique)) }
    $coreProj = Get-Content "DwgParser.Core/DwgParser.Core.csproj" -Raw
    if ($coreProj -match 'ProjectReference\s+Include="[^"]*Rendering') { $bad += "Core.csproj 引用 Rendering" }
    $deviceFiles = @("DwgParser.Rendering/PngDrawDevice.cs", "DwgParser.Core/Render/Draw/SvgDrawDevice.cs",
                     "DwgParser.Core/Render/Draw/RecordingDrawDevice.cs")
    foreach ($df in $deviceFiles) {
        if ((Test-Path $df) -and (Select-String -Path $df -Pattern "FrameRenderer" -Quiet)) {
            $bad += "$df 反向引用 FrameRenderer"
        }
    }
    if ($bad.Count -eq 0) { Add-Result "G6" "PASS" "依赖方向单向（Rendering→Core）" }
    else { Add-Result "G6" "FAIL" ($bad -join "; ") }
}

function Invoke-G7 {
    $out = dotnet run --project tests/DwgParser.Tests 2>&1
    if ($LASTEXITCODE -ne 0) {
        $detail = ($out | Select-String "\[FAIL\]" | Select-Object -First 3) -join "; "
        Add-Result "G7" "FAIL" ("实体层不变量: " + $detail)
    } else {
        Add-Result "G7" "PASS" "实体层不变量 dotnet run tests/DwgParser.Tests"
    }
}

New-Item -ItemType Directory -Force -Path "out/gate" *> $null
$gates = if ($Gate -eq "all") { @("G1","G2","G3","G4","G5","G6","G7") } else { @($Gate) }
foreach ($g in $gates) { & ("Invoke-" + $g) }

$passCount = 0
foreach ($r in $script:results) { if ($r.Status -eq "PASS") { $passCount++ } }
$summary = "----"
$total = $script:results.Count
Write-Host $summary
Write-Host ("{0}/{1} PASS" -f $passCount, $total)
if ($passCount -eq $total) { exit 0 } else { exit 1 }
