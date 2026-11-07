<#
.SYNOPSIS
Runs fixed solo campaign and Endless31 strategies from fresh starts.
.DESCRIPTION
The catalog describes legal campaign, fixed tactical-segment, and Endless steps.
No policy search or old checkpoint import is performed. Each attempt has its own
directory, proof paths, budgets, build identity, and comparison with the catalog
baseline. Resume skips completed cases only when all input identities agree,
including the planner, game assembly, runner, and frozen ContentData files.
Map filters permit independent workers sharing one output root; their reports
are isolated. SummarizeOnly merges existing case records without running games.
.EXAMPLE
powershell -File scripts/run-strategy-matrix.ps1 -PlannerAssembly .artifacts/planning/bin/MaximalBastion.Tests/release/MaximalBastion.Tests.dll -OutputDirectory .build/strategy-matrix -Maps crosswind_basin
#>
param(
    [Parameter(Mandatory=$true)][string]$PlannerAssembly,
    [string]$Catalog='tests/MaximalBastion.Tests/Fixtures/Matrix/catalog.json',
    [string]$OutputDirectory='.build/strategy-matrix',
    [string[]]$Maps=@(),
    [string[]]$Difficulties=@(),
    [string[]]$Challenges=@(),
    [switch]$Resume,
    [switch]$ValidateOnly,
    [switch]$SummarizeOnly
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path -Parent $PSScriptRoot
function Resolve-RepoPath([string]$Path){if([IO.Path]::IsPathRooted($Path)){return [IO.Path]::GetFullPath($Path)};return [IO.Path]::GetFullPath((Join-Path $repo $Path))}
function Read-Json([string]$Path){ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Path))}
function Field($Object,[string]$Name,$Default=$null){if($null -eq $Object){return $Default};$p=$Object.PSObject.Properties[$Name];if($null -eq $p -or $null -eq $p.Value){return $Default};return $p.Value}
function Hash-Text([string]$Value){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)))).Replace('-','')}finally{$sha.Dispose()}}
function Save-Json($Value,[string]$Path){
    $parent=Split-Path -Parent $Path;New-Item -ItemType Directory -Force $parent|Out-Null
    $tmp=Join-Path $parent ('.'+[IO.Path]::GetFileName($Path)+'.'+[Guid]::NewGuid().ToString('N')+'.tmp')
    [IO.File]::WriteAllText($tmp,(ConvertTo-Json -InputObject $Value -Depth 70),[Text.UTF8Encoding]::new($false))
    if([IO.File]::Exists($Path)){[IO.File]::Replace($tmp,$Path,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($tmp,$Path)}
}
function Child-Path([string]$Root,[string]$Relative){
    if([IO.Path]::IsPathRooted($Relative)){throw "Catalog paths must be relative: $Relative"}
    $path=[IO.Path]::GetFullPath((Join-Path $Root $Relative));$prefix=$Root.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if(!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw "Catalog path leaves its directory: $Relative"};return $path
}
$catalogPath=Resolve-RepoPath $Catalog;$catalogRoot=Split-Path -Parent $catalogPath
$outputRoot=Resolve-RepoPath $OutputDirectory;$plannerPath=Resolve-RepoPath $PlannerAssembly
$catalogData=Read-Json $catalogPath
if($catalogData.SchemaVersion -ne 1 -or @($catalogData.Cases).Count -eq 0){throw 'Unsupported or empty strategy catalog.'}
$keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$inputPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase);[void]$inputPaths.Add($catalogPath)
foreach($case in $catalogData.Cases){
    if(!$keys.Add($case.Id) -or $case.CoOp -or $case.Map -notin @('relay_divide','crosswind_basin','prism_circuit','foundry_loop') -or $case.Difficulty -notin @('easy','normal','hard','bastion') -or $case.Challenge -notin @('standard','close_quarters','core_six','no_reserves')){throw "Invalid or duplicate solo competitive case: $($case.Id)"}
    $caseRoot=Child-Path $catalogRoot $case.Directory
    foreach($name in @($case.Baseline)+@($case.Steps|ForEach-Object {foreach($field in @('Plan','Segments','Blueprint')){$v=Field $_ $field;if($v){$v}}})){
        $path=Child-Path $caseRoot $name;if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing recipe input: $path"};[void]$inputPaths.Add($path)
    }
    $prior=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($step in $case.Steps){
        if($step.Id -notmatch '^[a-z0-9-]+$' -or !$prior.Add($step.Id) -or $step.Kind -notin @('campaign','forecast','endless') -or $step.Trials -lt 1 -or $step.Seconds -le 0){throw "Invalid step in $($case.Id)"}
        if($step.Kind -eq 'campaign'){
            if($prior.Count -ne 1){throw 'Only the first step may create a campaign.'}
            $p=Read-Json (Child-Path $caseRoot $step.Plan)
            if($p.scenario.mapId -ne $case.Map -or $p.scenario.difficultyId -ne $case.Difficulty -or $p.scenario.challengeId -ne $case.Challenge -or $p.scenario.seed -ne $case.Seed -or $p.scenario.coOp -or $p.scenario.horizon -ne 30 -or $p.scenario.endless -or $p.scenario.startingProfile -ne 'fresh' -or !$p.scenario.forbidCombatSales){throw "Campaign scenario mismatch: $($case.Id)"}
            if(@($p.waves|Where-Object {@(Field $_ 'alternatives' @()).Count -gt 0}).Count){throw "Fixed recipes must not contain policy alternatives: $($case.Id)"}
        }elseif(!$prior.Contains($step.After) -or $step.After -eq $step.Id){throw "Step dependency is not an earlier step: $($case.Id)/$($step.Id)"}
        if($step.Kind -eq 'endless'){$p=Read-Json (Child-Path $caseRoot $step.Plan);if(@(Field $p.wave 'alternatives' @()).Count -gt 0){throw 'Fixed Endless recipes must contain one policy.'}}
    }
}
$cases=@($catalogData.Cases|Where-Object {($Maps.Count -eq 0 -or $_.Map -in $Maps) -and ($Difficulties.Count -eq 0 -or $_.Difficulty -in $Difficulties) -and ($Challenges.Count -eq 0 -or $_.Challenge -in $Challenges)})
if($cases.Count -eq 0){throw 'No catalog cases match the filters.'}
if($ValidateOnly){Write-Output "Validated $($catalogData.Cases.Count) catalog cases; $($cases.Count) selected.";exit 0}
if(!(Test-Path -LiteralPath $plannerPath -PathType Leaf)){throw "Planner assembly not found: $plannerPath"}
$gameAssembly=Join-Path (Split-Path -Parent $plannerPath) 'MaximalBastion.dll'
$buildHash=(Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash
$plannerHash=(Get-FileHash -LiteralPath $plannerPath -Algorithm SHA256).Hash
$runnerHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$contentRoot=Join-Path (Split-Path -Parent $plannerPath) 'ContentData'
function Read-ContentIdentity {
    $files=@(Get-ChildItem -LiteralPath $contentRoot -File -Recurse -Filter '*.json' | Sort-Object FullName)
    if($files.Count -eq 0){throw "No frozen content JSON found: $contentRoot"}
    $entries=@($files|ForEach-Object {[ordered]@{Path=$_.FullName.Substring($contentRoot.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    return [ordered]@{Identity=(Hash-Text (($entries|ForEach-Object {$_.Path+'='+$_.Sha256})-join "`n"));Files=$entries}
}
$contentLock=Read-ContentIdentity
$inputIdentity=Hash-Text (($inputPaths|Sort-Object|ForEach-Object {$_+'='+ (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}) -join "`n")
$identity=Hash-Text ('v2|'+$buildHash+'|'+$plannerHash+'|'+$runnerHash+'|'+$contentLock.Identity+'|'+$inputIdentity)
function Assert-FrozenInputs {
    if((Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash -ne $buildHash -or (Get-FileHash -LiteralPath $plannerPath -Algorithm SHA256).Hash -ne $plannerHash -or (Read-ContentIdentity).Identity -ne $contentLock.Identity){throw 'Frozen planner build or content changed during execution; use a fresh output root.'}
}
$configuration=[ordered]@{IdentityVersion=2;Identity=$identity;BuildHash=$buildHash;PlannerHash=$plannerHash;RunnerHash=$runnerHash;ContentIdentity=$contentLock.Identity;ContentFiles=$contentLock.Files;PlannerAssembly=$plannerPath;Catalog=$catalogPath;InputIdentity=$inputIdentity}
$lockPath=Join-Path $outputRoot 'input-lock.json'
if(Test-Path -LiteralPath $lockPath){
    if((Read-Json $lockPath).Identity -ne $identity){throw 'Output root has different build, content, runner, or recipe inputs; use a fresh output root.'}
}elseif($SummarizeOnly){throw "Output root has no input lock: $outputRoot"}
else{
    New-Item -ItemType Directory -Force $outputRoot|Out-Null
    $lockTemp=Join-Path $outputRoot ('.input-lock.'+[Guid]::NewGuid().ToString('N')+'.tmp')
    [IO.File]::WriteAllText($lockTemp,(ConvertTo-Json -InputObject $configuration -Depth 10),[Text.UTF8Encoding]::new($false))
    try{[IO.File]::Move($lockTemp,$lockPath)}catch{if(!(Test-Path -LiteralPath $lockPath)){throw}}finally{if(Test-Path -LiteralPath $lockTemp){Remove-Item -LiteralPath $lockTemp}}
    if((Read-Json $lockPath).Identity -ne $identity){throw 'Another worker initialized this output root with different inputs.'}
}
$scope=Hash-Text (($cases.Id|Sort-Object)-join '|')
$reportRoot=if($cases.Count -eq @($catalogData.Cases).Count){$outputRoot}else{Join-Path $outputRoot "_workers/$($scope.Substring(0,12))"}
$dotnet=Join-Path $repo '.dotnet/dotnet.exe';if(!(Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
function Proof-Path($case,$spec,[string]$FieldName,[string]$Attempt){Join-Path (Join-Path $Attempt $spec.Step) $spec.$FieldName}
function Read-Metrics([string]$Checkpoint){
    if(!(Test-Path -LiteralPath $Checkpoint)){return $null};$cp=Read-Json $Checkpoint;$s=$cp.snapshotJson|ConvertFrom-Json
    [ordered]@{BuildHash=$cp.buildHash;ContentHash=$cp.contentHash;Tick=$cp.tick;StateSignature=$cp.stateSignature;Wave=$s.Waves.CurrentWaveNumber;Lives=$s.Economy.Lives;Credits=$s.Economy.Credits;Leaks=$s.Economy.EscapedEnemies;Kills=$s.Economy.TotalKills;Towers=@($s.Towers).Count;Apex=@($s.Towers|Where-Object IsApex).Count;TowerInvestment=($s.Towers|Measure-Object InvestedCredits -Sum).Sum;PaidPlates=$s.Statistics.EmergencyDirectPurchases;PlateDeployments=$s.Statistics.EmergencyDeployments;GeneratedCharges=$s.Statistics.GeneratedCharges;ForgePurchases=$s.Statistics.GeneratorPurchases;ForgeUpgrades=$s.Statistics.GeneratorUpgrades;TowerMetrics=$s.Statistics.Towers}
}
function Delta($Before,$After){
    if($null -eq $After){return $null};$values=[ordered]@{}
    foreach($name in @('Tick','Lives','Credits','Leaks','Kills','Towers','Apex','TowerInvestment','PaidPlates','PlateDeployments','GeneratedCharges')){$values[$name]=$After[$name]-(Field $Before $name 0)};return $values
}
if(!$SummarizeOnly){
    New-Item -ItemType Directory -Force $reportRoot|Out-Null
    $workerConfiguration=[ordered]@{};foreach($key in $configuration.Keys){$workerConfiguration[$key]=$configuration[$key]};$workerConfiguration.Cases=$cases.Id
    Save-Json $workerConfiguration (Join-Path $reportRoot 'configuration.json')
    $completed=0
    foreach($case in $cases){
        $caseOutput=Child-Path $outputRoot $case.Id;$rowPath=Join-Path $caseOutput 'row.json';$statusPath=Join-Path $caseOutput 'status.json'
        if(Test-Path -LiteralPath $rowPath){
            $old=Read-Json $rowPath
            if(!$Resume){throw "Existing result for $($case.Id); use a fresh output directory or -Resume."}
            if($old.Identity -ne $identity){throw "Resume input identity differs for $($case.Id); use a fresh output root."}
            $missingProofs=@(@('CampaignTrace','CampaignReceipt','CampaignCheckpoint','Endless31Trace','Endless31Receipt','Endless31Checkpoint')|Where-Object {!(Test-Path -LiteralPath $old.$_ -PathType Leaf)})
            if($old.CampaignVerified -and $old.Endless31Verified -and $missingProofs.Count -eq 0){$completed++;Write-Output "SKIP $($case.Id) already complete";Save-Json ([ordered]@{Identity=$identity;Stage='resuming';Case=$case.Id;Completed=$completed;Total=$cases.Count;UpdatedUtc=[DateTime]::UtcNow.ToString('o')}) (Join-Path $reportRoot 'status.json');continue}
        }
        if(Test-Path -LiteralPath $statusPath){$prior=Read-Json $statusPath;if($prior.Identity -ne $identity){throw "Unfinished case has different inputs: $($case.Id)"};if(!$Resume){throw "Unfinished case exists: $($case.Id); use -Resume."}}
        New-Item -ItemType Directory -Force $caseOutput|Out-Null
        $number=@(Get-ChildItem -LiteralPath $caseOutput -Directory|Where-Object Name -like 'attempt-*').Count+1
        $attempt=Join-Path $caseOutput ('attempt-{0:D3}' -f $number);New-Item -ItemType Directory $attempt|Out-Null
        $recipeRoot=Child-Path $catalogRoot $case.Directory;$stepRecords=[Collections.Generic.List[object]]::new();$errorText=$null;$deepest=0
        foreach($step in $case.Steps){
            Assert-FrozenInputs
            $stageOutput=Join-Path $attempt $step.Id
            Save-Json ([ordered]@{Identity=$identity;Case=$case.Id;Stage=$step.Id;Attempt=$number;UpdatedUtc=[DateTime]::UtcNow.ToString('o')}) $statusPath
            Save-Json ([ordered]@{Identity=$identity;Stage='running';Case=$case.Id;Step=$step.Id;Completed=$completed;Total=$cases.Count;StatusPath=$statusPath;ProcessId=$PID;UpdatedUtc=[DateTime]::UtcNow.ToString('o')}) (Join-Path $reportRoot 'status.json')
            $arguments=@('--no-combat-sales','--simulations',"$($step.Trials)",'--seconds',"$($step.Seconds)",'--output',$stageOutput)
            if($step.Kind -eq 'campaign'){$arguments=@('--planner-layout-campaign','--plan',(Child-Path $recipeRoot $step.Plan),'--beam','1')+$arguments}
            else{
                $checkpoint=Join-Path (Join-Path $attempt $step.After) $step.Checkpoint
                if($step.Kind -eq 'endless'){$arguments=@('--planner-endless-continuation','--checkpoint',$checkpoint,'--plan',(Child-Path $recipeRoot $step.Plan))+$arguments}
                else{$segments=Read-Json (Child-Path $recipeRoot $step.Segments);$arguments=@('--planner-tactical-forecast','--checkpoint',$checkpoint,'--segments',(Child-Path $recipeRoot $step.Segments),'--phases',"$(@($segments).Count)",'--beam','1')+$arguments;if(Field $step 'FirstEndless' $false){$arguments+='--first-endless-wave'};if(Field $step 'Blueprint'){$arguments+=@('--blueprint',(Child-Path $recipeRoot $step.Blueprint))}}
            }
            $timer=[Diagnostics.Stopwatch]::StartNew();$passed=$false
            try{
                & $dotnet $plannerPath @arguments > (Join-Path $attempt ($step.Id+'.log')) 2>&1
                if($LASTEXITCODE -ne 0){throw "Planner exit $LASTEXITCODE"}
                if($step.Kind -eq 'campaign'){$result=Read-Json (Join-Path $stageOutput 'results.json');$deepest=$result.highestCompletedWave;$passed=$deepest -ge $step.RequiredWave}
                elseif($step.Kind -eq 'endless'){$result=Read-Json (Join-Path $stageOutput 'results.json');$passed=$result.verification.verified -and $result.verification.horizonSurvived;if($passed){$deepest=31}}
                else{$result=Read-Json (Join-Path $stageOutput 'forecast.json');$clear=Join-Path $stageOutput 'clear.json';if(Test-Path -LiteralPath $clear){$state=(Read-Json $clear).snapshotJson|ConvertFrom-Json;$passed=!$state.IsDefeat -and $state.Waves.CurrentWaveNumber -eq $step.RequiredWave -and $state.Waves.ActiveWaveNumber -eq 0;if($passed){$deepest=$step.RequiredWave}}}
                if(!$passed){$errorText="Fixed strategy did not clear $($step.Id)."}
            }catch{$errorText=$_.Exception.Message}
            Assert-FrozenInputs
            $stepRecords.Add([ordered]@{Id=$step.Id;Kind=$step.Kind;Arguments=$arguments;TrialBudget=$step.Trials;SecondsBudget=$step.Seconds;WallSeconds=$timer.Elapsed.TotalSeconds;Passed=$passed;Output=$stageOutput})
            if(!$passed){break}
        }
        $cTrace=Proof-Path $case $case.CampaignProof 'Trace' $attempt;$cReceipt=Proof-Path $case $case.CampaignProof 'Receipt' $attempt;$cCheckpoint=Proof-Path $case $case.CampaignProof 'Checkpoint' $attempt
        $eTrace=Proof-Path $case $case.Endless31Proof 'Trace' $attempt;$eReceipt=Proof-Path $case $case.Endless31Proof 'Receipt' $attempt;$eCheckpoint=Proof-Path $case $case.Endless31Proof 'Checkpoint' $attempt
        $cv=$false;$ev=$false
        if(Test-Path -LiteralPath $cReceipt){$receipt=Read-Json $cReceipt;$cv=$receipt.verified -and $receipt.verifiedWin -and $receipt.fromNormalStart}
        if(Test-Path -LiteralPath $eReceipt){$receipt=Read-Json $eReceipt;$ev=$receipt.verified -and $receipt.horizonSurvived -and $receipt.fromNormalStart}
        $cm=Read-Metrics $cCheckpoint;$em=Read-Metrics $eCheckpoint;$baseline=Read-Json (Child-Path $recipeRoot $case.Baseline)
        $row=[ordered]@{Identity=$identity;Map=$case.Map;Difficulty=$case.Difficulty;Challenge=$case.Challenge;Seed=$case.Seed;CoOp=$false;Reused=$false;CampaignVerified=$cv;Endless31Verified=$ev;HighestCompletedWave=$deepest;CampaignTrace=$cTrace;CampaignReceipt=$cReceipt;Endless31Trace=$eTrace;Endless31Receipt=$eReceipt;CampaignCheckpoint=$cCheckpoint;Endless31Checkpoint=$eCheckpoint;Recipe=(Join-Path $recipeRoot 'campaign.json');Attempt=$attempt;Error=$errorText;Steps=@($stepRecords.ToArray());Baseline=$baseline;CampaignMetrics=$cm;Endless31Metrics=$em;CampaignDelta=(Delta $baseline.Campaign $cm);Endless31Delta=(Delta $baseline.Endless31 $em)}
        Save-Json $row (Join-Path $attempt 'result.json');Save-Json $row $rowPath;Save-Json ([ordered]@{Identity=$identity;Case=$case.Id;Stage=if($cv -and $ev){'complete'}else{'failed'};CampaignVerified=$cv;Endless31Verified=$ev;HighestCompletedWave=$deepest;Attempt=$number;Error=$errorText;UpdatedUtc=[DateTime]::UtcNow.ToString('o')}) $statusPath
        $completed++;Write-Output "$($case.Id): campaign=$cv endless31=$ev wave=$deepest"
    }
}
$rows=@(foreach($case in $cases){$path=Join-Path (Child-Path $outputRoot $case.Id) 'row.json';if(Test-Path -LiteralPath $path){$row=Read-Json $path;if($row.Identity -ne $identity){throw "Result input identity mismatch: $($case.Id)"};$row}})
$manifestRows=@($rows|Select-Object Map,Difficulty,Challenge,Seed,CoOp,Reused,CampaignTrace,CampaignReceipt,Endless31Trace,Endless31Receipt,CampaignCheckpoint,Endless31Checkpoint)
Save-Json ([ordered]@{SchemaVersion=1;ExpectedCases=$cases.Count;ExpectedBuildHash=$buildHash;Rows=$manifestRows}) (Join-Path $reportRoot 'selected-manifest.json')
Save-Json $rows (Join-Path $reportRoot 'results.json')
$failed=@($rows|Where-Object {!$_.CampaignVerified -or !$_.Endless31Verified}).Count
$stage=if($rows.Count -ne $cases.Count){'incomplete'}elseif($failed -gt 0){'finished-with-failures'}else{'complete'}
Save-Json ([ordered]@{Identity=$identity;Stage=$stage;Completed=$rows.Count;Total=$cases.Count;Failed=$failed;CampaignWins=@($rows|Where-Object CampaignVerified).Count;Endless31Wins=@($rows|Where-Object Endless31Verified).Count;Results=(Join-Path $reportRoot 'results.json');Manifest=(Join-Path $reportRoot 'selected-manifest.json');UpdatedUtc=[DateTime]::UtcNow.ToString('o')}) (Join-Path $reportRoot 'status.json')
if($rows.Count -ne $cases.Count -or @($rows|Where-Object {!$_.CampaignVerified -or !$_.Endless31Verified}).Count -gt 0){exit 2}
