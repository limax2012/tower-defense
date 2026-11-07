<#
.SYNOPSIS
Audits selected campaign and Endless31 replay proofs and writes a matrix report.
.DESCRIPTION
The JSON manifest contains Rows with Map, Difficulty, Challenge, Seed, CoOp,
Reused, CampaignTrace, CampaignReceipt, Endless31Trace and Endless31Receipt.
Paths are absolute or relative to the manifest. Optional CampaignCheckpoint and
Endless31Checkpoint paths add checkpoint signature and standing-board checks.
ExpectedCases, ExpectedBuildHash and ExpectedContentHash constrain new evidence;
reused evidence retains its recorded build identity. Missing proof pairs remain
pending. Invalid evidence always returns a nonzero exit code.
.PARAMETER RequireComplete
Also return a nonzero exit code when any selected scenario is still pending.
.EXAMPLE
powershell -File scripts/summarize-strategy-matrix.ps1 -Manifest .build/matrix/selected-manifest.json -OutputDirectory .build/matrix/summary -RequireComplete
#>
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$OutputDirectory = '',
    [switch]$RequireComplete
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$manifestPath = [IO.Path]::GetFullPath($Manifest)
$manifestRoot = Split-Path -Parent $manifestPath
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $manifestRoot 'summary' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

function Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}
function Read-Json([string]$Path) { return ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Path)) }
function Resolve-Artifact($Row, [string]$Name) {
    $path = [string](Field $Row $Name '')
    if ([string]::IsNullOrWhiteSpace($path)) { return $null }
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $manifestRoot $path }
    return [IO.Path]::GetFullPath($path)
}
function Relative-Link([string]$Path) {
    if ([string]::IsNullOrEmpty($Path)) { return '' }
    $base = [Uri]::new($outputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar)
    return $base.MakeRelativeUri([Uri]::new($Path)).ToString()
}
function Display-Name([string]$Id) {
    switch ($Id) {
        'relay_divide' { return 'Helix Reactor' }
        'prism_circuit' { return 'Prism Nullspace' }
        'crosswind_basin' { return 'Rainline Heights' }
        'foundry_loop' { return 'Cinderworks' }
        'easy' { return 'Easy' }
        'normal' { return 'Medium' }
        'hard' { return 'Hard' }
        'bastion' { return 'Bastion' }
        'standard' { return 'Standard' }
        'close_quarters' { return 'Signal Gauntlet' }
        'core_six' { return 'Core Six' }
        'no_reserves' { return 'Entrenched' }
        default { return $Id }
    }
}
function Assert-Trace($Trace, $Receipt, $Row, [int]$Horizon, [string]$Label, $Problems) {
    $scenario = Field $Trace 'scenario'
    foreach ($pair in @(@('mapId','Map'), @('difficultyId','Difficulty'), @('challengeId','Challenge'))) {
        if ((Field $scenario $pair[0]) -ne (Field $Row $pair[1])) { $Problems.Add("${Label}: scenario $($pair[0]) differs from manifest.") }
    }
    if ((Field $scenario 'seed') -ne (Field $Row 'Seed' 1337) -or [bool](Field $scenario 'coOp' $false) -ne [bool](Field $Row 'CoOp' $false)) {
        $Problems.Add("${Label}: seed or solo/co-op scope differs from manifest.")
    }
    if ((Field $scenario 'horizon') -ne $Horizon -or [bool](Field $scenario 'endless' $false) -ne ($Horizon -gt 30)) {
        $Problems.Add("${Label}: declared horizon differs from requested campaign/Endless horizon.")
    }
    if ((Field $scenario 'startingProfile') -ne 'fresh' -or $null -ne (Field $Trace 'originSaveJson') -or -not (Field $Receipt 'fromNormalStart' $false)) {
        $Problems.Add("${Label}: normal fresh start is not certified.")
    }
    if (-not (Field $scenario 'forbidCombatSales' $false)) { $Problems.Add("${Label}: mid-wave sales are not prohibited by the scenario.") }
    if (-not (Field $Receipt 'verified' $false) -or -not (Field $Receipt 'horizonSurvived' $false) -or -not (Field $Trace 'horizonSurvived' $false)) {
        $Problems.Add("${Label}: replay does not certify horizon survival.")
    }
    if ($Horizon -eq 30 -and (-not (Field $Receipt 'verifiedWin' $false) -or -not (Field $Trace 'campaignVictory' $false))) {
        $Problems.Add("${Label}: campaign victory is not certified.")
    }
    if ([bool](Field $Receipt 'verifiedWin' $false) -ne [bool](Field $Trace 'campaignVictory' $false) -or [bool](Field $Receipt 'horizonSurvived' $false) -ne [bool](Field $Trace 'horizonSurvived' $false)) {
        $Problems.Add("${Label}: receipt and trace outcomes differ.")
    }
    $actions = @(Field $Trace 'actions')
    if ([string]::IsNullOrWhiteSpace([string](Field $Trace 'finalSignature')) -or (Field $Trace 'finalSignature') -ne (Field $Receipt 'finalSignature') -or
        (Field $Trace 'finalTick') -ne (Field $Receipt 'ticksChecked') -or $actions.Count -ne (Field $Receipt 'actionsChecked')) {
        $Problems.Add("${Label}: receipt signature, tick, or action count differs from trace.")
    }
    $sales = @($actions | Where-Object { (Field $_ 'accepted' $false) -and (Field (Field $_ 'command') 'type') -in @('SellTower','SellGenerator') })
    $combatSales = @($sales | Where-Object { Field $_ 'duringWave' $false }).Count
    if ($combatSales -ne 0 -or (Field $Receipt 'combatSales') -ne $combatSales -or (Field $Receipt 'intermissionSales') -ne ($sales.Count - $combatSales)) {
        $Problems.Add("${Label}: sale counts or the mid-wave selling restriction disagree.")
    }
    if ((Field $Trace 'finalLives' 0) -le 0 -or (Field $Trace 'defeat' $false) -or (Field $Trace 'finalWave' 0) -lt $Horizon) {
        $Problems.Add("${Label}: final state does not survive the declared horizon.")
    }
    foreach ($name in @('buildHash','contentHash')) {
        if ([string](Field $Trace $name) -notmatch '^[A-Fa-f0-9]{64}$') { $Problems.Add("${Label}: $name is missing or malformed.") }
    }
    $coreRoster = @('needle_turret','shard_fan','breaker_cannon','ember_coil','frost_spire','signal_beacon')
    $entrenchedCommands = @('ChooseDoctrine','PlaceTower','SetTargetMode','SpecializeTower','StartWave','UpgradeTower','ContinueEndless')
    foreach ($action in $actions) {
        if (-not (Field $action 'accepted' $false)) { continue }
        $command = Field $action 'command'
        if ((Field $Row 'Challenge') -eq 'core_six' -and (Field $command 'type') -eq 'PlaceTower' -and (Field $command 'towerDefinitionId') -notin $coreRoster) {
            $Problems.Add("${Label}: Core Six places an excluded tower."); break
        }
        if ((Field $Row 'Challenge') -eq 'no_reserves' -and (Field $command 'type') -notin $entrenchedCommands) {
            $Problems.Add("${Label}: Entrenched contains an excluded command: $(Field $command 'type')."); break
        }
    }
}
function Tower-Count($Trace) {
    $placed = @(Field $Trace 'actions' | Where-Object { (Field $_ 'accepted' $false) -and (Field (Field $_ 'command') 'type') -eq 'PlaceTower' }).Count
    $sold = @(Field $Trace 'actions' | Where-Object { (Field $_ 'accepted' $false) -and (Field (Field $_ 'command') 'type') -eq 'SellTower' }).Count
    return $placed - $sold
}

$inputData = Read-Json $manifestPath
$selected = @(Field $inputData 'Rows')
$rows = [Collections.Generic.List[object]]::new()
$keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$matrixProblems = [Collections.Generic.List[string]]::new()
foreach ($item in $selected) {
    $key = (@((Field $item 'Map'), (Field $item 'Difficulty'), (Field $item 'Challenge'), (Field $item 'Seed' 1337), [bool](Field $item 'CoOp' $false)) -join '/')
    $problems = [Collections.Generic.List[string]]::new()
    $missing = [Collections.Generic.List[string]]::new()
    if (-not $keys.Add($key)) { $problems.Add('Duplicate scenario in selected manifest.') }
    $paths = @{}
    foreach ($name in @('CampaignTrace','CampaignReceipt','Endless31Trace','Endless31Receipt','CampaignCheckpoint','Endless31Checkpoint')) {
        $paths[$name] = Resolve-Artifact $item $name
        if ($name -notlike '*Checkpoint' -and ($null -eq $paths[$name] -or -not (Test-Path -LiteralPath $paths[$name] -PathType Leaf))) { $missing.Add($name) }
    }
    $record = [ordered]@{
        Map = Field $item 'Map'; Difficulty = Field $item 'Difficulty'; Challenge = Field $item 'Challenge'
        Seed = Field $item 'Seed' 1337; CoOp = [bool](Field $item 'CoOp' $false); Reused = [bool](Field $item 'Reused' $false)
        Status = 'pending'; CampaignVerified = $false; Endless31Verified = $false; FreshPrefixPreserved = $false
        BuildHash = $null; ContentHash = $null; InformationProfile = $null; ExecutionProfile = $null
        CampaignLives = $null; Endless31Lives = $null; CampaignCredits = $null; Endless31Credits = $null
        CampaignTowers = $null; Endless31Towers = $null; CombatSales = $null; IntermissionSales = $null
        CampaignTrace = $paths.CampaignTrace; CampaignReceipt = $paths.CampaignReceipt
        Endless31Trace = $paths.Endless31Trace; Endless31Receipt = $paths.Endless31Receipt
        CampaignCheckpoint = $paths.CampaignCheckpoint; Endless31Checkpoint = $paths.Endless31Checkpoint
        Missing = @($missing.ToArray()); Problems = @(); Notes = Field $item 'Notes' ''
    }
    if ($missing.Count -eq 0) {
        try {
            $ct = Read-Json $paths.CampaignTrace; $cv = Read-Json $paths.CampaignReceipt
            $et = Read-Json $paths.Endless31Trace; $ev = Read-Json $paths.Endless31Receipt
            Assert-Trace $ct $cv $item 30 'Campaign' $problems
            Assert-Trace $et $ev $item 31 'Endless31' $problems
            foreach ($name in @('buildHash','contentHash')) {
                if ((Field $ct $name) -ne (Field $et $name)) { $problems.Add("Campaign and Endless31 $name differ.") }
                $expected = Field $inputData ('Expected' + $name)
                if (-not $record.Reused -and -not [string]::IsNullOrEmpty([string]$expected) -and (Field $ct $name) -ne $expected) { $problems.Add("New proof $name differs from the selected build/content.") }
            }
            foreach ($name in @('informationProfile','executionProfile')) {
                if ((Field (Field $ct 'scenario') $name) -ne (Field (Field $et 'scenario') $name)) { $problems.Add("Campaign and Endless31 $name differ.") }
            }
            $ca = @(Field $ct 'actions'); $ea = @(Field $et 'actions')
            $prefix = @($ea | Select-Object -First $ca.Count)
            $samePrefix = $ea.Count -ge $ca.Count -and (ConvertTo-Json -InputObject $ca -Depth 60 -Compress) -ceq (ConvertTo-Json -InputObject $prefix -Depth 60 -Compress)
            if (-not $samePrefix -or (Field $ct 'originSignature') -ne (Field $et 'originSignature')) { $problems.Add('Endless31 does not preserve the exact fresh campaign prefix.') }
            $campaignTowers = Tower-Count $ct; $endlessTowers = Tower-Count $et
            if ($campaignTowers -le 0 -or $endlessTowers -lt $campaignTowers) { $problems.Add('Standing tower count decreased at the Endless31 horizon.') }
            foreach ($pair in @(@('CampaignCheckpoint',$ct,$campaignTowers), @('Endless31Checkpoint',$et,$endlessTowers))) {
                $path = $paths[$pair[0]]
                if ([string]::IsNullOrEmpty($path)) { continue }
                if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $problems.Add("$($pair[0]) is missing."); continue }
                $cp = Read-Json $path
                $snapshot = ConvertFrom-Json -InputObject (Field $cp 'snapshotJson')
                if ((Field $cp 'stateSignature') -ne (Field $pair[1] 'finalSignature') -or @(Field $snapshot 'Towers').Count -ne $pair[2]) { $problems.Add("$($pair[0]) differs from trace signature or standing tower count.") }
            }
            $record.CampaignVerified = [bool](Field $cv 'verifiedWin' $false)
            $record.Endless31Verified = [bool](Field $ev 'horizonSurvived' $false)
            $record.FreshPrefixPreserved = $samePrefix
            $record.BuildHash = Field $ct 'buildHash'; $record.ContentHash = Field $ct 'contentHash'
            $record.InformationProfile = Field (Field $ct 'scenario') 'informationProfile'; $record.ExecutionProfile = Field (Field $ct 'scenario') 'executionProfile'
            $record.CampaignLives = Field $ct 'finalLives'; $record.Endless31Lives = Field $et 'finalLives'
            $record.CampaignCredits = Field $ct 'finalCredits'; $record.Endless31Credits = Field $et 'finalCredits'
            $record.CampaignTowers = $campaignTowers; $record.Endless31Towers = $endlessTowers
            $record.CombatSales = Field $ev 'combatSales'; $record.IntermissionSales = Field $ev 'intermissionSales'
            $record.Status = if ($problems.Count -eq 0) { 'verified' } else { 'invalid' }
        } catch { $problems.Add($_.Exception.Message); $record.Status = 'invalid' }
    } elseif ($problems.Count -gt 0) { $record.Status = 'invalid' }
    $record.Problems = @($problems.ToArray())
    $rows.Add([pscustomobject]$record)
}
$expectedCases = [int](Field $inputData 'ExpectedCases' $rows.Count)
if ($rows.Count -ne $expectedCases) { $matrixProblems.Add("Manifest contains $($rows.Count) rows; $expectedCases expected.") }
$verified = @($rows | Where-Object Status -eq 'verified')
$pending = @($rows | Where-Object Status -eq 'pending')
$invalid = @($rows | Where-Object Status -eq 'invalid')
$report = [ordered]@{
    SchemaVersion = 1; Manifest = $manifestPath; ExpectedCases = $expectedCases
    Complete = $verified.Count -eq $expectedCases -and $matrixProblems.Count -eq 0
    Verified = $verified.Count; VerifiedNew = @($verified | Where-Object { -not $_.Reused }).Count
    VerifiedReused = @($verified | Where-Object Reused).Count; Pending = $pending.Count; Invalid = $invalid.Count
    ExpectedBuildHash = Field $inputData 'ExpectedBuildHash'; ExpectedContentHash = Field $inputData 'ExpectedContentHash'
    Problems = @($matrixProblems.ToArray()); Rows = @($rows.ToArray())
}
ConvertTo-Json -InputObject $report -Depth 60 | Set-Content -LiteralPath (Join-Path $outputRoot 'matrix-summary.json') -Encoding utf8
$rows | Select-Object Map,Difficulty,Challenge,Seed,CoOp,Reused,Status,CampaignLives,Endless31Lives,CampaignCredits,Endless31Credits,CampaignTowers,Endless31Towers,CombatSales,IntermissionSales,BuildHash,ContentHash,CampaignTrace,CampaignReceipt,Endless31Trace,Endless31Receipt,@{Name='Problems';Expression={$_.Problems -join '; '}},@{Name='Missing';Expression={$_.Missing -join '; '}} | Export-Csv -LiteralPath (Join-Path $outputRoot 'matrix-summary.csv') -NoTypeInformation -Encoding utf8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Strategy matrix evidence')
$lines.Add('')
$lines.Add("Verified $($report.Verified)/$expectedCases selected scenarios: $($report.VerifiedNew) new, $($report.VerifiedReused) reused. Pending: $($report.Pending). Invalid: $($report.Invalid).")
$lines.Add('')
$lines.Add('This report audits existing replay receipts, scenario identities, exact campaign prefixes, selling restrictions, and standing defenses. It does not rerun simulations. A verified row proves one legal fresh campaign victory followed by survival through Endless wave 31 for the recorded seed and execution profile; it does not estimate a human win rate or survival beyond wave 31.')
$lines.Add('')
$lines.Add('| Map | Campaign + Endless31 proofs | New | Reused | Pending | Invalid |')
$lines.Add('|---|---:|---:|---:|---:|---:|')
foreach ($group in ($rows | Group-Object Map)) {
    $wins = @($group.Group | Where-Object Status -eq 'verified')
    $new = @($wins | Where-Object { -not $_.Reused }).Count
    $reused = @($wins | Where-Object Reused).Count
    $mapPending = @($group.Group | Where-Object Status -eq 'pending').Count
    $mapInvalid = @($group.Group | Where-Object Status -eq 'invalid').Count
    $lines.Add("| $(Display-Name $group.Name) | $($wins.Count) / $($group.Count) | $new | $reused | $mapPending | $mapInvalid |")
}
$lines.Add('')
$lines.Add('| Map | Difficulty | Directive | Evidence | Campaign / 31 lives | Campaign / 31 towers | Proofs |')
$lines.Add('|---|---|---|---|---:|---:|---|')
foreach ($row in $rows) {
    $kind = $row.Status + $(if ($row.Reused) { ' (reused)' } else { '' })
    $proofs = if ($row.Status -eq 'verified') { "[campaign]($(Relative-Link $row.CampaignReceipt)), [31]($(Relative-Link $row.Endless31Receipt))" } else { '' }
    $lines.Add("| $(Display-Name $row.Map) | $(Display-Name $row.Difficulty) | $(Display-Name $row.Challenge) | $kind | $($row.CampaignLives) / $($row.Endless31Lives) | $($row.CampaignTowers) / $($row.Endless31Towers) | $proofs |")
}
if ($invalid.Count -gt 0 -or $matrixProblems.Count -gt 0) {
    $lines.Add(''); $lines.Add('## Evidence problems'); $lines.Add('')
    foreach ($problem in $matrixProblems) { $lines.Add("- $problem") }
    foreach ($row in $invalid) { $lines.Add("- $($row.Map)/$($row.Difficulty)/$($row.Challenge): $($row.Problems -join '; ')") }
}
[IO.File]::WriteAllLines((Join-Path $outputRoot 'matrix-summary.md'), $lines, [Text.UTF8Encoding]::new($false))
Write-Output "Verified $($report.Verified)/$expectedCases ($($report.VerifiedNew) new, $($report.VerifiedReused) reused); pending $($report.Pending); invalid $($report.Invalid)."
Write-Output (Join-Path $outputRoot 'matrix-summary.json')
if ($invalid.Count -gt 0 -or $matrixProblems.Count -gt 0 -or ($RequireComplete -and -not $report.Complete)) { exit 1 }
