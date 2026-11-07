param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sourceRoot = [IO.Path]::GetFullPath($Directory)
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Audit directory does not exist: $sourceRoot" }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $sourceRoot }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$rows = [Collections.Generic.List[object]]::new()
$witnesses = [Collections.Generic.List[object]]::new()
$issues = [Collections.Generic.List[object]]::new()
$matrices = [Collections.Generic.List[object]]::new()

function Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try {
        $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::Read -bor [IO.FileShare]::Delete))
        $reader = [IO.StreamReader]::new($stream)
        try { $json = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return ConvertFrom-Json -InputObject $json
    }
    catch {
        $issues.Add([pscustomobject]@{ Path = $Path; Reason = "Could not read JSON: $($_.Exception.Message)" })
        return $null
    }
}

function Relative-Link([string]$Path) {
    $base = [Uri]::new($outputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar)
    return $base.MakeRelativeUri([Uri]::new([IO.Path]::GetFullPath($Path))).ToString()
}

function Scenario-Key($Scenario) {
    if ($null -eq $Scenario) { return 'synthetic fixture suite' }
    $scope = if (Field $Scenario 'coOp' $false) { 'coop' } else { 'solo' }
    $mode = if (Field $Scenario 'endless' $false) { 'endless' } else { 'campaign' }
    $key = (@((Field $Scenario 'mapId'), (Field $Scenario 'difficultyId'), (Field $Scenario 'challengeId'),
        $scope, $mode, (Field $Scenario 'horizon'), (Field $Scenario 'startingProfile'),
        (Field $Scenario 'informationProfile'), (Field $Scenario 'executionProfile'), (Field $Scenario 'seed')) -join '/')
    if (Field $Scenario 'forbidCombatSales' $false) { $key += '/no-combat-sales' }
    return $key
}

function Read-Witness([string]$TracePath, [string]$VerificationPath, [string]$BuildHash,
    [string]$ContentHash, [string]$EvidenceKind, [bool]$Synthetic, $ExpectedScenario = $null) {
    $trace = Read-Json $TracePath
    $verification = Read-Json $VerificationPath
    $reason = $null
    if ($null -eq $trace -or $null -eq $verification) { $reason = 'Trace or independent verification receipt is missing.' }
    elseif (-not (Field $verification 'verified' $false)) { $reason = 'Verification receipt does not certify this trace.' }
    elseif ((Field $trace 'buildHash') -ne $BuildHash -or
            (-not [string]::IsNullOrEmpty($ContentHash) -and (Field $trace 'contentHash') -ne $ContentHash)) {
        $reason = 'Trace build/content identity differs from its experiment.'
    }
    elseif ([string]::IsNullOrEmpty([string](Field $trace 'finalSignature')) -or
            (Field $trace 'finalSignature') -ne (Field $verification 'finalSignature') -or
            (Field $trace 'finalTick') -ne (Field $verification 'ticksChecked') -or
            @(Field $trace 'actions').Count -ne (Field $verification 'actionsChecked')) {
        $reason = 'Verification receipt does not match the trace signature, tick or action count.'
    }
    elseif ($null -ne $ExpectedScenario -and (Scenario-Key (Field $trace 'scenario')) -ne (Scenario-Key $ExpectedScenario)) {
        $reason = 'Trace scenario or selling restriction differs from its experiment.'
    }
    if ($null -eq $reason -and $null -ne (Field $verification 'combatSales') -and $null -ne (Field $verification 'intermissionSales')) {
        $sales = @(Field $trace 'actions' | Where-Object {
            (Field $_ 'accepted' $false) -and (Field (Field $_ 'command') 'type') -in @('SellTower', 'SellGenerator')
        })
        $combatSales = @($sales | Where-Object { Field $_ 'duringWave' $false }).Count
        if ($combatSales -ne (Field $verification 'combatSales') -or
            $sales.Count - $combatSales -ne (Field $verification 'intermissionSales')) {
            $reason = 'Verification receipt sale counts differ from the accepted trace commands.'
        }
    }
    $normalStart = $null -eq (Field $trace 'originSaveJson')
    if ($null -eq $reason -and $normalStart -ne [bool](Field $verification 'fromNormalStart' $false)) {
        $reason = 'Verification receipt and trace disagree about fresh/imported origin.'
    }
    if ($null -eq $reason -and [bool](Field $verification 'verifiedWin' $false) -ne [bool](Field $trace 'campaignVictory' $false)) {
        $reason = 'Verification receipt and trace disagree about campaign victory.'
    }
    if ($null -eq $reason -and [bool](Field $verification 'horizonSurvived' $false) -ne [bool](Field $trace 'horizonSurvived' $false)) {
        $reason = 'Verification receipt and trace disagree about horizon survival.'
    }
    if ($null -ne $reason) {
        $issues.Add([pscustomobject]@{ Path = $TracePath; Reason = $reason })
        return $null
    }
    $win = [bool](Field $verification 'verifiedWin' $false)
    $horizon = [bool](Field $verification 'horizonSurvived' $false)
    $classification = if ($win) { 'VERIFIED WIN' } elseif ($horizon) { 'VERIFIED HORIZON SURVIVAL' } else { 'VERIFIED LEGAL PARTIAL TRACE' }
    $row = [pscustomobject]@{
        Kind = $EvidenceKind; Synthetic = $Synthetic; FromNormalStart = $normalStart
        BuildHash = [string](Field $trace 'buildHash'); ContentHash = [string](Field $trace 'contentHash')
        Scenario = Scenario-Key (Field $trace 'scenario'); Classification = $classification
        FinalSignature = Field $trace 'finalSignature'; Wave = Field $trace 'finalWave'; Lives = Field $trace 'finalLives'
        Credits = Field $trace 'finalCredits'; Actions = Field $verification 'actionsChecked'; Ticks = Field $verification 'ticksChecked'
        CombatSales = Field $verification 'combatSales'; IntermissionSales = Field $verification 'intermissionSales'
        Trace = Relative-Link $TracePath; Verification = Relative-Link $VerificationPath
    }
    $witnesses.Add($row)
    return $row
}

function Add-Experiment([string]$Kind, $Data, [string]$Path, [bool]$Synthetic, $Scenario,
    [string]$StartScope, [string]$Outcome, [int]$Simulations, [double]$Seconds, [int]$CompletedWave,
    $VerifiedRows, $Budget, $Restrictions, [string]$Notes) {
    $reportPath = Join-Path (Split-Path -Parent $Path) 'report.md'
    if ($Kind -eq 'trace-sensitivity') { $reportPath = Join-Path (Split-Path -Parent $Path) 'trace-experiments.md' }
    $rows.Add([pscustomobject]@{
        Kind = $Kind; Synthetic = $Synthetic; BuildHash = [string](Field $Data 'buildHash' 'unreported')
        ContentHash = [string](Field $Data 'contentHash' 'multiple synthetic fixtures')
        Scenario = Scenario-Key $Scenario; StartScope = $StartScope; RecordedOutcome = $Outcome
        Simulations = $Simulations; RecordedWallSeconds = $Seconds; CompletedWave = $CompletedWave
        VerifiedCampaignTraces = @($VerifiedRows | Where-Object { $null -ne $_ -and $_.Classification -eq 'VERIFIED WIN' }).Count
        VerifiedHorizonTraces = @($VerifiedRows | Where-Object { $null -ne $_ -and $_.Classification -eq 'VERIFIED HORIZON SURVIVAL' }).Count
        VerifiedPartialTraces = @($VerifiedRows | Where-Object { $null -ne $_ -and $_.Classification -eq 'VERIFIED LEGAL PARTIAL TRACE' }).Count
        Budget = $Budget; BudgetHistory = @(Field $Data 'budgetHistory'); Restrictions = $Restrictions
        ForbidCombatSales = [bool](Field $Scenario 'forbidCombatSales' $false)
        Backtracks = Field $Data 'backtracks' 0; SimulatedTicks = Field $Data 'simulatedTicks' 0
        CheckpointMilliseconds = Field $Data 'checkpointMilliseconds' 0
        StrategyClusters = Field $Data 'strategyClusters'; Notes = $Notes
        Artifact = Relative-Link $Path
        Report = if (Test-Path -LiteralPath $reportPath) { Relative-Link $reportPath } else { Relative-Link $Path }
    })
}

$files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
    $_.Name -in @('results.json', 'diagnostics.json', 'alternatives.json', 'allocations.json', 'forecast.json', 'trace-experiments.json', 'matrix.json')
} | Sort-Object FullName)
foreach ($file in $files) {
    $data = Read-Json $file.FullName
    if ($null -eq $data) { continue }
    $folder = $file.DirectoryName
    $scenario = Field $data 'scenario'
    $synthetic = ([string](Field $scenario 'mapId')).StartsWith('fixture-', [StringComparison]::OrdinalIgnoreCase)
    $build = [string](Field $data 'buildHash')
    $content = [string](Field $data 'contentHash')
    $verified = [Collections.Generic.List[object]]::new()
    switch ($file.Name) {
        'results.json' {
            if ($null -ne (Field $data 'campaignSignature') -and $null -ne (Field $data 'transitionSignature')) {
                $row = Read-Witness (Join-Path $folder 'trace.json') (Join-Path $folder 'verification.json') $build $content 'endless-continuation' $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
                $scope = if (Field (Field $data 'verification') 'fromNormalStart' $false) { 'fresh' } else { 'imported' }
                $completed = if (Field (Field $data 'verification') 'horizonSurvived' $false) { Field $scenario 'horizon' 31 } else { 30 }
                Add-Experiment 'endless-continuation' $data $file.FullName $synthetic $scenario $scope (Field $data 'outcome' 'unreported') `
                    @(Field $data 'trials').Count (Field $data 'wallSeconds' 0) $completed $verified (Field $data 'options') $null `
                    ("Campaign prefix $(Field $data 'campaignSignature'); verified transition $(Field $data 'transitionSignature').")
                break
            }
            $isLayout = $null -ne (Field $data 'planId')
            $kind = if ($isLayout) { 'layout-campaign' } else { 'search' }
            $names = @(Field $data 'witnessFiles')
            if ($isLayout -and (Field (Field $data 'verification') 'verifiedWin' $false)) { $names = @('witness.json') }
            foreach ($name in $names) {
                $receipt = [IO.Path]::GetFileNameWithoutExtension($name) + '-verification.json'
                $row = Read-Witness (Join-Path $folder $name) (Join-Path $folder $receipt) $build $content $kind $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
            }
            $scope = if ($synthetic) { 'synthetic' } elseif (Field $data 'importedStart' $false) { 'imported' } else { 'fresh' }
            $budget = if ($isLayout) { Field $data 'options' } else { Field $data 'budget' }
            $notes = if ($isLayout) { "Supplied goal-layout and conditional policy prior; plan hash $(Field $data 'planHash')." } else { Field $data 'stopReason' '' }
            Add-Experiment $kind $data $file.FullName $synthetic $scenario $scope (Field $data 'outcome' 'unreported') `
                @(Field $data 'trials').Count (Field $data 'wallSeconds' 0) (Field $data 'highestCompletedWave' 0) `
                $verified $budget (Field $data 'restrictions') $notes
        }
        'allocations.json' {
            $variants = @(Field $data 'trials')
            for ($index = 0; $index -lt $variants.Count; $index++) {
                if (-not (Field (Field $variants[$index] 'verification') 'verifiedWin' $false)) { continue }
                $stem = 'witness-' + ($index + 1).ToString('D6')
                $row = Read-Witness (Join-Path $folder ($stem + '.json')) (Join-Path $folder ($stem + '-verification.json')) $build $content 'joint-allocation' $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
            }
            $scope = if (Field $data 'fromNormalStart' $false) { 'fresh' } else { 'imported' }
            $completed = 0
            foreach ($variant in $variants) { $completed = [Math]::Max($completed, [int](Field $variant 'wave' 0) - [int](-not (Field $variant 'waveCleared' $false))) }
            Add-Experiment 'joint-allocation' $data $file.FullName $synthetic $scenario $scope (Field $data 'outcome') `
                $variants.Count (Field $data 'wallSeconds' 0) $completed $verified (Field $data 'options') $null (Field $data 'stopReason' '')
        }
        'forecast.json' {
            $outcome = [string](Field $data 'outcome')
            $stem = if ($outcome -eq 'VERIFIED WIN') { 'witness' } elseif ($outcome -in @('NEXT WAVE CLEARED', 'VERIFIED HORIZON SURVIVAL')) { 'clear-trace' } else { '' }
            if ($stem.Length -gt 0) {
                $row = Read-Witness (Join-Path $folder ($stem + '.json')) (Join-Path $folder ($stem + '-verification.json')) $build $content 'tactical-forecast' $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
            }
            $scope = if (Field $data 'fromNormalStart' $false) { 'fresh' } else { 'imported' }
            $variants = @(Field $data 'trials')
            $completed = 0
            foreach ($variant in $variants) { $completed = [Math]::Max($completed, [int](Field $variant 'wave' 0) - [int](-not (Field $variant 'waveCleared' $false))) }
            Add-Experiment 'tactical-forecast' $data $file.FullName $synthetic $scenario $scope $outcome `
                $variants.Count (Field $data 'wallSeconds' 0) $completed $verified (Field $data 'options') $null (Field $data 'stopReason' '')
        }
        'diagnostics.json' {
            foreach ($traceFile in @(Get-ChildItem -LiteralPath $folder -Filter 'fixture-witness-*.json' -File | Where-Object { $_.Name -match '^fixture-witness-\d+\.json$' })) {
                $receipt = [IO.Path]::GetFileNameWithoutExtension($traceFile.Name) + '-verification.json'
                $row = Read-Witness $traceFile.FullName (Join-Path $folder $receipt) $build '' 'synthetic-diagnostics' $true
                if ($null -ne $row) { $verified.Add($row) }
            }
            $notes = "Finite oracle: $(Field $data 'exhaustiveSequences' 0) sequences, $(Field $data 'legalSequences' 0) legal, $(Field $data 'winningSequences' 0) winning; earlier revisions $(Field $data 'earlierWaveRevisions' 0); planner agreement $(Field $data 'plannerAgreesWithFiniteOracle' $false)."
            Add-Experiment 'synthetic-diagnostics' $data $file.FullName $true $null 'synthetic' 'SYNTHETIC COMPETENCE EVIDENCE' `
                @(Field $data 'cases').Count (Field $data 'wallSeconds' 0) 0 $verified $null $null $notes
        }
        'alternatives.json' {
            $variants = @(Field $data 'results')
            foreach ($variant in $variants) {
                $traceName = [string](Field $variant 'trace')
                if ([string]::IsNullOrEmpty($traceName)) { continue }
                $row = Read-Witness (Join-Path $folder $traceName) (Join-Path $folder ((Field $variant 'id') + '.verification.json')) `
                    $build $content 'checkpoint-allocation' $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
            }
            $seconds = ($variants | Measure-Object -Property wallSeconds -Sum).Sum
            $notes = "Source hash $(Field $data 'sourceSha256'); initial credits $(Field $data 'startingCredits'); $(Field $data 'commonContinuation')."
            Add-Experiment 'checkpoint-allocation' $data $file.FullName $synthetic $scenario 'imported' 'MATCHED IMPORTED ALLOCATIONS' `
                $variants.Count $seconds 0 $verified $null (Field $data 'commonContinuation') $notes
        }
        'trace-experiments.json' {
            $variants = @(Field $data 'experiments')
            foreach ($variant in $variants) {
                $traceName = [string](Field $variant 'witnessFile')
                if ([string]::IsNullOrEmpty($traceName)) { continue }
                $row = Read-Witness (Join-Path $folder $traceName) (Join-Path $folder ((Field $variant 'id') + '-verification.json')) `
                    $build $content 'trace-sensitivity' $synthetic $scenario
                if ($null -ne $row) { $verified.Add($row) }
            }
            $scope = if (@($verified | Where-Object { $_.FromNormalStart }).Count -gt 0) { 'fresh' } elseif ($verified.Count -gt 0) { 'imported' } else { 'unverified origin' }
            $budget = [pscustomobject]@{ Simulations = Field $data 'maximumRollouts'; Seconds = Field $data 'maximumWallSeconds' }
            Add-Experiment 'trace-sensitivity' $data $file.FullName $synthetic $scenario $scope 'FIXED-SCHEDULE SENSITIVITY' `
                $variants.Count ($variants | Measure-Object -Property wallSeconds -Sum).Sum 0 $verified $budget `
                (Field $data 'scope') ("Successful removed action indices: " + (@(Field $data 'successfulRemovals') -join ', '))
        }
        'matrix.json' {
            $matrices.Add([pscustomobject]@{ Requested = Field $data 'requested' 0; Completed = Field $data 'completed' 0; Artifact = Relative-Link $file.FullName })
        }
    }
}

$uniqueWitnesses = @($witnesses | Sort-Object BuildHash, ContentHash, Scenario, FinalSignature, Trace |
    Group-Object -Property BuildHash, ContentHash, Scenario, FinalSignature | ForEach-Object { $_.Group[0] })
$productionWitnesses = @($uniqueWitnesses | Where-Object { -not $_.Synthetic })
$freshWins = @($productionWitnesses | Where-Object { $_.FromNormalStart -and $_.Classification -eq 'VERIFIED WIN' })
$importedWins = @($productionWitnesses | Where-Object { -not $_.FromNormalStart -and $_.Classification -eq 'VERIFIED WIN' })
$groups = @($rows | Group-Object -Property BuildHash, ContentHash, Synthetic | ForEach-Object {
    $first = $_.Group[0]
    [pscustomobject]@{ BuildHash = $first.BuildHash; ContentHash = $first.ContentHash; Synthetic = $first.Synthetic;
        Experiments = $_.Count; Artifacts = @($_.Group | ForEach-Object { $_.Artifact }) }
})
$summary = [pscustomobject]@{
    SchemaVersion = 1; GeneratedAtUtc = [DateTime]::UtcNow.ToString('o'); SourceDirectory = $sourceRoot
    Scope = 'Summary of recorded experiments. Independent replay receipts are checked against their trace; this script does not rerun simulation. Hash groups are not pooled as one unchanged build.'
    ExperimentReports = $rows.Count; ProductionSearchReports = @($rows | Where-Object { $_.Kind -eq 'search' -and -not $_.Synthetic }).Count
    FreshCampaignWitnesses = $freshWins.Count; ImportedCampaignWitnesses = $importedWins.Count
    FreshWinningScenarioBuilds = @($freshWins | Group-Object -Property BuildHash, ContentHash, Scenario).Count
    ImportedWinningScenarioBuilds = @($importedWins | Group-Object -Property BuildHash, ContentHash, Scenario).Count
    VerifiedProductionHorizonTraces = @($productionWitnesses | Where-Object { $_.Classification -eq 'VERIFIED HORIZON SURVIVAL' }).Count
    SyntheticVerifiedTerminalTraces = @($uniqueWitnesses | Where-Object { $_.Synthetic -and $_.Classification -ne 'VERIFIED LEGAL PARTIAL TRACE' }).Count
    HashGroups = $groups; Matrices = $matrices.ToArray(); Experiments = $rows.ToArray(); Witnesses = $witnesses.ToArray(); Issues = $issues.ToArray()
}
$jsonPath = Join-Path $outputRoot 'audit-report.json'
$summary | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath ($jsonPath + '.tmp') -Encoding UTF8
Move-Item -LiteralPath ($jsonPath + '.tmp') -Destination $jsonPath -Force
$rows | Select-Object Kind, Synthetic, BuildHash, ContentHash, Scenario, StartScope, RecordedOutcome, Simulations,
    RecordedWallSeconds, CompletedWave, VerifiedCampaignTraces, VerifiedHorizonTraces, VerifiedPartialTraces, ForbidCombatSales, Backtracks,
    @{ Name = 'Budget'; Expression = { $_.Budget | ConvertTo-Json -Depth 12 -Compress } },
    @{ Name = 'BudgetHistory'; Expression = { $_.BudgetHistory | ConvertTo-Json -Depth 16 -Compress } },
    @{ Name = 'Restrictions'; Expression = { $_.Restrictions | ConvertTo-Json -Depth 12 -Compress } }, Artifact, Report |
    Export-Csv -LiteralPath (Join-Path $outputRoot 'audit-report.csv') -NoTypeInformation -Encoding UTF8

$markdown = [Text.StringBuilder]::new()
[void]$markdown.AppendLine('# Strategy and balance audit evidence')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("$($rows.Count) experiment reports. Production witnesses: **$($freshWins.Count) fresh-start campaign traces** across $($summary.FreshWinningScenarioBuilds) scenario/build combinations; **$($importedWins.Count) imported campaign continuations** across $($summary.ImportedWinningScenarioBuilds) scenario/build combinations. Verified horizon traces: $($summary.VerifiedProductionHorizonTraces). Synthetic terminal traces: $($summary.SyntheticVerifiedTerminalTraces), excluded from production wins.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine($summary.Scope)
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('Counts describe search outcomes and archived witnesses, not independent player attempts or win probabilities. Equal build/content/scenario/final-signature witnesses are counted once in totals, while all source links remain below. A failed search is not an impossibility certificate. Fixed-schedule delays and deletions do not establish a robust reactive policy.')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('## Build and content groups')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('| Build SHA-256 | Content SHA-256 | Scope | Reports |')
[void]$markdown.AppendLine('| --- | --- | --- | ---: |')
foreach ($group in $groups) {
    $scope = if ($group.Synthetic) { 'synthetic' } else { 'production' }
    [void]$markdown.AppendLine("| $($group.BuildHash) | $($group.ContentHash) | $scope | $($group.Experiments) |")
}
if ($matrices.Count -gt 0) {
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('## Content matrix coverage')
    [void]$markdown.AppendLine()
    foreach ($matrix in $matrices) { [void]$markdown.AppendLine("- $($matrix.Completed)/$($matrix.Requested) requested scenario rows evaluated: [matrix]($($matrix.Artifact)). Matrix rows are not counted again as experiments.") }
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('## Recorded experiments')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('| Scope | Scenario | Recorded result | Completed wave | Rollouts | Declared budget | Policy restrictions | Campaign / horizon receipts | Evidence |')
[void]$markdown.AppendLine('| --- | --- | --- | ---: | ---: | --- | --- | --- | --- |')
foreach ($row in $rows) {
    $budgetText = if ($null -eq $row.Budget) { 'fixed experiment' } else {
        "$(Field $row.Budget 'simulations' (Field $row.Budget 'maximumTrials')) rollouts / $(Field $row.Budget 'wallSeconds' (Field $row.Budget 'seconds' (Field $row.Budget 'maximumWallSeconds'))) s"
    }
    $policy = if ($row.Kind -eq 'search') {
        $disabled = @('placementHeuristics', 'combatPurchasing', 'supportReasoning', 'saving', 'backtracking') |
            Where-Object { -not (Field $row.Restrictions $_ $true) }
        $label = "$(Field $row.Restrictions 'baseline') / $(Field $row.Restrictions 'family')"
        if (@($disabled).Count -gt 0) { $label += '; disabled: ' + ($disabled -join ', ') }
        if (Field $row.Restrictions 'excludedTowerId') { $label += '; exclude ' + (Field $row.Restrictions 'excludedTowerId') }
        $label
    } else { 'see experiment scope' }
    if ($row.ForbidCombatSales) { $policy += '; intermission-only selling' }
    [void]$markdown.AppendLine("| $($row.StartScope) / $($row.Kind) | $($row.Scenario) | $($row.RecordedOutcome) | $($row.CompletedWave) | $($row.Simulations) | $budgetText | $policy | $($row.VerifiedCampaignTraces) / $($row.VerifiedHorizonTraces) | [report]($($row.Report)), [data]($($row.Artifact)) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('Budgets, invocation history when recorded, capability restrictions, source hashes, stop reasons, backtracking, checkpoint timings and strategy clusters are preserved in [JSON](audit-report.json); comparison rows are in [CSV](audit-report.csv). Recorded wall seconds are per experiment and may overlap across workers; summing them does not measure elapsed audit time.')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('## Independently verified production terminal traces')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('| Origin | Scenario | Result | Wave | Lives | Credits | Actions | Combat / intermission sales | Evidence |')
[void]$markdown.AppendLine('| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |')
foreach ($witness in $productionWitnesses | Where-Object { $_.Classification -ne 'VERIFIED LEGAL PARTIAL TRACE' }) {
    $scope = if ($witness.FromNormalStart) { 'fresh' } else { 'imported' }
    $combatSales = if ($null -eq $witness.CombatSales) { 'unreported' } else { $witness.CombatSales }
    $intermissionSales = if ($null -eq $witness.IntermissionSales) { 'unreported' } else { $witness.IntermissionSales }
    [void]$markdown.AppendLine("| $scope | $($witness.Scenario) | $($witness.Classification) | $($witness.Wave) | $($witness.Lives) | $($witness.Credits) | $($witness.Actions) | $combatSales / $intermissionSales | [trace]($($witness.Trace)), [verification]($($witness.Verification)) |")
}
if ($issues.Count -gt 0) {
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('## Missing or inconsistent evidence')
    [void]$markdown.AppendLine()
    foreach ($issue in $issues) { [void]$markdown.AppendLine("- $($issue.Reason) [artifact]($(Relative-Link $issue.Path))") }
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('No balance change follows automatically from these measurements. Imported states establish continuation only. Synthetic puzzles establish specific solver capabilities under their declared finite models. Investigate unresolved scenarios with broader placements, earlier spending revisions and execution alternatives before proposing production changes.')
$markdownPath = Join-Path $outputRoot 'audit-report.md'
$markdown.ToString() | Set-Content -LiteralPath ($markdownPath + '.tmp') -Encoding UTF8
Move-Item -LiteralPath ($markdownPath + '.tmp') -Destination $markdownPath -Force
Write-Host "Audit summary: $markdownPath"
Write-Host "Reports: $($rows.Count); fresh campaign traces: $($freshWins.Count); imported campaign traces: $($importedWins.Count); evidence issues: $($issues.Count)"
