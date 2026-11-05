# 实体重构验收门 G1-G8（docs/entity-refactor-plan.md §6 + G7 实体层不变量 + G8 跨版本棘轮）
# 用法:  pwsh ./tools/gates/gates.ps1 [-Gate all|G1|G2|G3|G4|G5|G6] [-Repo <repoRoot>]
# 语义:  每门输出 PASS / FAIL(<原因>) / BLOCKED(<缺依赖>)；任一 FAIL/BLOCKED 则退出码非 0。
#        无 fixture 时 G2/G3/G4 报 BLOCKED，不得报绿（§6 BLOCKED 语义）。
param(
    [string]$Gate = "all",
    [string]$Repo = ""
)

$ErrorActionPreference = "Continue"
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

function Invoke-G8 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G8" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G8" "BLOCKED" "CLI 未构建"; return }
    $knownBad = @()
    $mf = Join-Path $Repo "out/base/manifest.json"
    try { $knownBad = (Get-Content $mf -Raw -Encoding UTF8 | ConvertFrom-Json).g8KnownBad } catch {}
    $versions = @("AC1015","AC1018","AC1021","AC1024","AC1027","AC1032")
    $fails = @(); $knownCount = 0; $recovered = @()
    foreach ($f in $fixtures) {
        if (-not (Test-Path $f.source)) { continue }
        $srcJson = Join-Path $Repo $f.json
        foreach ($v in $versions) {
            $combo = "$($f.stem):$v"
            $outDwg = "out/gate/$($f.stem).g8.$v.dwg"
            $bad = $null
            & $cli $f.source --convert $v --output $outDwg *> $null
            if ($LASTEXITCODE -ne 0) { $bad = "convert rc=$LASTEXITCODE" }
            else {
                & $cli $outDwg --json "out/gate/g8reread.json" *> $null
                if ($LASTEXITCODE -ne 0) { $bad = "reread rc=$LASTEXITCODE" }
                else {
                    foreach ($k in @("ltscale", "celtscale")) {
                        $sm = Select-String -Path $srcJson -Pattern ('"' + $k + '":([-0-9.eE]+)')
                        $rm = Select-String -Path "out/gate/g8reread.json" -Pattern ('"' + $k + '":([-0-9.eE]+)')
                        if ($sm -and $sm.Matches.Groups.Count -gt 1 -and $rm -and $rm.Matches.Groups.Count -gt 1) {
                            $sv = $sm.Matches.Groups[1].Value
                            $rv = $rm.Matches.Groups[1].Value
                            if ($sv -ne $rv) { $bad = "$k 退化 $sv->$rv" }
                        }
                    }
                }
            }
            if ($bad -ne $null) {
                if ($knownBad -contains $combo) { $knownCount++; $fails += "$combo($bad 已知坏)" }
                else { $fails += "$combo($bad 新坏)" }
            }
            elseif ($knownBad -contains $combo) { $recovered += $combo }
        }
    }
    if ($recovered.Count -gt 0) {
        Add-Result "G8" "FAIL" ("组合已修复但仍在 manifest.g8KnownBad 名单上（请删除名单条目）: " + ($recovered -join "; "))
        return
    }
    $hardFail = $fails | Where-Object { $_ -notmatch "已知坏" }
    if ($hardFail.Count -eq 0) {
        Add-Result "G8" "PASS" ("跨版本转换×自消费全矩阵（已知坏/退化 " + $knownCount + " 项挂棘轮名单）")
    } else {
        Add-Result "G8" "FAIL" ("新坏组合: " + ($hardFail -join "; "))
    }
}

function Invoke-G9 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G9" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G9" "BLOCKED" "CLI 未构建"; return }
    function New-OpsFile([string]$path, [string]$json) {
        [System.IO.File]::WriteAllText((Join-Path $Repo $path), $json, (New-Object System.Text.UTF8Encoding($false)))
    }
    function Run-Ops([string]$src, [string]$opsPath, [string]$tag) {
        & $cli $src --ops $opsPath --output "out/gate/g9out-$tag.dwg" --report "out/gate/g9rep-$tag.json" *> $null
        return $LASTEXITCODE
    }
    $byStem = @{}
    foreach ($x in $fixtures) { $byStem[$x.stem] = $x }

    $fail = @()

    $f1 = $byStem["mgf2_1015"]
    if ($f1 -and (Test-Path $f1.source)) {
        & $cli $f1.source --inspect "out/gate/g9inspect.json" --limit 40 *> $null
        $insp = Get-Content "out/gate/g9inspect.json" -Raw -Encoding UTF8 | ConvertFrom-Json
        $cand = $insp.candidates | Where-Object { $_.ops -contains "setText" } | Select-Object -First 1
        if ($null -eq $cand) { $fail += "mgf2 无 setText 候选" }
        else {
            New-OpsFile "out/gate/g9ops1.json" ('{"ops":[{"op":"setText","handle":"' + $cand.handle + '","value":"G9 编辑门测试文本"}]}')
            $rc = Run-Ops $f1.source "out/gate/g9ops1.json" "mgf2"
            if ($rc -ne 0) { $fail += "mgf2 setText rc=$rc" }
            else {
                & $cli "out/gate/g9out-mgf2.dwg" --json "out/gate/g9re-mgf2.json" *> $null
                $re = Get-Content "out/gate/g9re-mgf2.json" -Raw -Encoding UTF8 | ConvertFrom-Json
                $hit = $re.entities | Where-Object { $_.handle -eq $cand.handle } | Select-Object -First 1
                if ($null -eq $hit -or $hit.text -ne "G9 编辑门测试文本") { $fail += "mgf2 回读未见新值" }
            }
        }
    }

    $f2 = $byStem["kindee测试260508"]
    if ($f2 -and (Test-Path $f2.source)) {
        & $cli $f2.source --inspect "out/gate/g9inspect2.json" *> $null
        $insp2 = Get-Content "out/gate/g9inspect2.json" -Raw -Encoding UTF8 | ConvertFrom-Json
        $sa = $insp2.candidates | Where-Object { $_.ops -contains "setAttrib" } | Select-Object -First 1
        $dl = $insp2.candidates | Where-Object { $_.ops -contains "delete" -and $_.ops.Count -eq 1 } | Select-Object -First 1
        if ($null -eq $sa -or $null -eq $dl) { $fail += "kindee 缺 setAttrib/delete 候选" }
        else {
            $opsJson = '{"ops":[' +
                '{"op":"setAttrib","handle":"' + $sa.handle + '","text":"G9A","tag":"G9TAG"},' +
                '{"op":"addMText","x":10.5,"y":20.25,"value":"G9 新增注释","height":3.5},' +
                '{"op":"delete","handle":"' + $dl.handle + '"}]}'
            New-OpsFile "out/gate/g9ops2.json" $opsJson
            $before = & $cli $f2.source --json "out/gate/g9src-kindee.json" *> $null
            $srcE = (Get-Content "out/gate/g9src-kindee.json" -Raw -Encoding UTF8 | ConvertFrom-Json).entities.Count
            $rc = Run-Ops $f2.source "out/gate/g9ops2.json" "kindee"
            if ($rc -ne 0) { $fail += "kindee 三算子 rc=$rc" }
            else {
                $rep = Get-Content "out/gate/g9rep-kindee.json" -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($rep.written -ne $true) { $fail += "kindee written=false" }
                $bad = $rep.outcomes | Where-Object { $_.status -ne "Applied" }
                if ($bad) { $fail += ("kindee 算子未全 Applied: " + (($bad | ForEach-Object { $_.op + ":" + $_.reason }) -join "; ")) }
                & $cli "out/gate/g9out-kindee.dwg" --json "out/gate/g9re-kindee.json" *> $null
                $re2 = Get-Content "out/gate/g9re-kindee.json" -Raw -Encoding UTF8 | ConvertFrom-Json
                $ah = $re2.entities | Where-Object { $_.handle -eq $sa.handle } | Select-Object -First 1
                if ($null -eq $ah -or $ah.tag -ne "G9TAG") { $fail += "kindee setAttrib 回读未见新 tag" }
                $nm = $re2.entities | Where-Object { $_.text -eq "G9 新增注释" } | Select-Object -First 1
                if ($null -eq $nm) { $fail += "kindee addMText 回读未见新增" }
                $gone = $re2.entities | Where-Object { $_.handle -eq $dl.handle }
                if ($gone) { $fail += "kindee delete 回读仍存在" }
            }
        }
    }

    $neg = $null
    & $cli $f1.source --ops "out/gate/g9ops1.json" --report "out/gate/g9r2.json" *> $null
    if ($LASTEXITCODE -ne 2) { $fail += "负例（缺 output）rc=$LASTEXITCODE 应为 2" }

    $ea = $insp.candidates | Where-Object { $_.ops -contains "setAttrib" } | Select-Object -First 1
    if ($ea) {
        & $cli $f1.source --edit-attr ($ea.handle + "=G9ATTR") --convert AC1015 --output "out/gate/g9ea.dwg" *> $null
        if ($LASTEXITCODE -ne 0) { $fail += "edit-attr rc=$LASTEXITCODE" }
        else {
            & $cli "out/gate/g9ea.dwg" --json "out/gate/g9ea.json" *> $null
            $reEa = Get-Content "out/gate/g9ea.json" -Raw -Encoding UTF8 | ConvertFrom-Json
            $hitEa = $reEa.entities | Where-Object { $_.handle -eq $ea.handle } | Select-Object -First 1
            if ($null -eq $hitEa -or $hitEa.text -ne "G9ATTR") { $fail += "edit-attr 回读未见新值" }
        }
    }

    if ($fail.Count -eq 0) { Add-Result "G9" "PASS" "五腿编辑回路（四算子 + --edit-attr 旧模式）+ 用法负例 rc=2" }
    else { Add-Result "G9" "FAIL" ($fail -join "; ") }
}

function Invoke-G10 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G10" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G10" "BLOCKED" "CLI 未构建"; return }
    $fails = @()
    foreach ($f in $fixtures) {
        if (-not (Test-Path $f.source)) { continue }
        foreach ($m in @("--strings", "--classes", "--eedaudit")) {
            & $cli $f.source $m *> $null
            if ($LASTEXITCODE -ne 0) { $fails += "$($f.stem)$m rc=$LASTEXITCODE" }
        }
    }
    foreach ($f2x in $fixtures) {
        if (-not (Test-Path $f2x.source)) { continue }
        & $cli $f2x.source --fieldize *> $null
        if ($LASTEXITCODE -ne 0) { $fails += "$($f2x.stem)--fieldize rc=$LASTEXITCODE" }
    }
    $acisFixture = $fixtures | Where-Object { $_.sourceVersion -eq "AC1027" -and (Test-Path $_.source) } | Select-Object -First 1
    if ($acisFixture) {
        & $cli $acisFixture.source --acis "out/gate/g10.sab" *> $null
        if ($LASTEXITCODE -ne 0) { $fails += "$($acisFixture.stem)--acis rc=$LASTEXITCODE" }
        & $cli $acisFixture.source --preview "out/gate/g10.bmp" *> $null
        if ($LASTEXITCODE -ne 0) { $fails += "$($acisFixture.stem)--preview rc=$LASTEXITCODE" }
    }
    if ($fails.Count -eq 0) { Add-Result "G10" "PASS" "只读诊断模式（strings/classes/eedaudit/fieldize×7 + acis/preview）全 rc=0" }
    else { Add-Result "G10" "FAIL" ($fails -join "; ") }
}

function Invoke-G11 {
    $fixtures = Get-Fixtures
    if ($null -eq $fixtures) { Add-Result "G11" "BLOCKED" "out/base/manifest.json 缺失"; return }
    $cli = Join-Path $Repo "DwgParser.Cli/bin/Debug/net8.0-windows/DwgParser.Cli.exe"
    if (-not (Test-Path $cli)) { Add-Result "G11" "BLOCKED" "CLI 未构建"; return }
    $fails = @()
    foreach ($f in $fixtures) {
        if (-not (Test-Path $f.source)) { continue }
        if (-not $f.pngLSha256) { $fails += "$($f.stem):基线无 pngLSha256"; continue }
        $out = "out/gate/$($f.stem).g11.pngL"
        & $cli $f.source --pngL $out *> $null
        if ($LASTEXITCODE -ne 0) { $fails += "$($f.stem):pngL rc=$LASTEXITCODE"; continue }
        $h = (Get-FileHash $out -Algorithm SHA256).Hash.ToLower()
        if ($h -ne $f.pngLSha256.ToLower()) { $fails += "$($f.stem):pngL 哈希≠基线" }
    }
    if ($fails.Count -eq 0) { Add-Result "G11" "PASS" "分层渲染 PNG-L 逐 fixture 哈希=基线（跨会话稳定）" }
    else { Add-Result "G11" "FAIL" ($fails -join "; ") }
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
$gates = if ($Gate -eq "all") { @("G1","G2","G3","G4","G5","G6","G7","G8","G9","G10","G11") } else { @($Gate) }
foreach ($g in $gates) { & ("Invoke-" + $g) }

$passCount = 0
foreach ($r in $script:results) { if ($r.Status -eq "PASS") { $passCount++ } }
$summary = "----"
$total = $script:results.Count
Write-Host $summary
Write-Host ("{0}/{1} PASS" -f $passCount, $total)
if ($passCount -eq $total) { exit 0 } else { exit 1 }
