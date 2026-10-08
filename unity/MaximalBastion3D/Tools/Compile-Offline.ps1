[CmdletBinding()]
param(
    [ValidateSet('All', 'Runtime', 'Editor', 'Smoke')]
    [string]$Task = 'All',
    [string]$EditorPath,
    [string]$OutputDirectory,
    [string[]]$AdditionalReferences = @()
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$editor = & (Join-Path $PSScriptRoot 'Resolve-Unity.ps1') -EditorPath $EditorPath
$editorData = Join-Path (Split-Path $editor -Parent) 'Data'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot '.artifacts/3d-development/offline-compile' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$compiler = Join-Path $editorData 'DotNetSdkRoslyn/csc.dll'
$dotnet = Join-Path $editorData 'NetCoreRuntime/dotnet.exe'
$packageAssemblies = Join-Path $projectRoot 'Library/ScriptAssemblies'
$referenceSource = 'project Library/ScriptAssemblies'
if (-not (Test-Path -LiteralPath (Join-Path $packageAssemblies 'Unity.RenderPipelines.Universal.Runtime.dll'))) {
    $templateRoot = Join-Path $editorData 'Resources/PackageManager/ProjectTemplates/libcache'
    $template = Get-ChildItem -LiteralPath $templateRoot -Directory -Filter 'com.unity.template.3d-cross-platform-*' |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.dll') } |
        Sort-Object Name -Descending | Select-Object -First 1
    if (-not $template) { throw 'No compiled URP reference assemblies were found. Import the project in Unity first.' }
    $packageAssemblies = Join-Path $template.FullName 'ScriptAssemblies'
    $referenceSource = 'installed editor template cache (preliminary API validation)'
}

$references = [Collections.Generic.List[string]]::new()
$references.Add((Join-Path $editorData 'NetStandard/ref/2.1.0/netstandard.dll'))
Get-ChildItem (Join-Path $editorData 'NetStandard/compat/2.1.0/shims/netfx') -Filter '*.dll' | ForEach-Object { $references.Add($_.FullName) }
Get-ChildItem (Join-Path $editorData 'Managed/UnityEngine') -Filter '*.dll' | ForEach-Object { $references.Add($_.FullName) }
$references.Add((Join-Path $editorData 'Managed/UnityEngine.dll'))
$references.Add((Join-Path $editorData 'Managed/UnityEditor.dll'))
$references.Add((Join-Path $editorData 'Managed/UnityEditor.CoreModule.dll'))
Get-ChildItem -LiteralPath $packageAssemblies -Filter '*.dll' | Where-Object { $_.Name -match '^Unity\.' -and $_.Name -notmatch '\.CodeGen\.' } | ForEach-Object { $references.Add($_.FullName) }
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Filter '*.dll' -Recurse | ForEach-Object { $references.Add($_.FullName) }
foreach ($reference in $AdditionalReferences) { $references.Add([IO.Path]::GetFullPath($reference)) }
$references = @($references | Where-Object { Test-Path -LiteralPath $_ } | Sort-Object -Unique)

function Invoke-Compiler {
    param([string]$AssemblyName, [string[]]$Sources, [string[]]$ExtraReferences = @())
    if (-not $Sources.Count) { throw "No source files found for $AssemblyName." }
    $output = Join-Path $OutputDirectory "$AssemblyName.dll"
    $response = Join-Path $OutputDirectory "$AssemblyName.rsp"
    $arguments = @('/nostdlib+', '/target:library', '/langversion:9', '/unsafe+', '/nullable:disable', '/deterministic+', '/nologo', ('/out:"' + $output + '"'), '/define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE;UNITY_STANDALONE_WIN;UNITY_6000_3;UNITY_6000_3_OR_NEWER;UNITY_6000_0_OR_NEWER;NET_STANDARD_2_1;ENABLE_LEGACY_INPUT_MANAGER')
    foreach ($reference in ($references + $ExtraReferences)) { $arguments += '/reference:"' + $reference + '"' }
    foreach ($source in $Sources) { $arguments += '"' + $source + '"' }
    $arguments | Set-Content -LiteralPath $response -Encoding UTF8
    $diagnostics = & $dotnet $compiler /noconfig "@$response" 2>&1
    $exitCode = $LASTEXITCODE
    $diagnostics | Set-Content (Join-Path $OutputDirectory "$AssemblyName.log")
    $diagnostics | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "Offline compile failed for $AssemblyName. See $OutputDirectory" }
    Write-Host "Compiled $AssemblyName ($($Sources.Count) source files)."
    return $output
}

Write-Host "URP references: $referenceSource"
if ($Task -eq 'Smoke') {
    $smoke = Join-Path $OutputDirectory 'CompileSmoke.cs'
    @'
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public sealed class CompileSmoke : MonoBehaviour
{
    public void Verify() {
        var label = new Label("API references");
        var camera = GetComponent<Camera>();
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        var bloom = ScriptableObject.CreateInstance<Bloom>();
        bloom.intensity.Override(0.5f);
        Debug.Log(label.text + Time.deltaTime);
    }
}
'@ | Set-Content -LiteralPath $smoke -Encoding UTF8
    Invoke-Compiler -AssemblyName 'Bastion3D.CompileSmoke' -Sources @($smoke) | Out-Null
} else {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Filter '*.cs' -Recurse)
    $runtimeSources = @($sources | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' } | Select-Object -ExpandProperty FullName)
    $editorSources = @($sources | Where-Object { $_.FullName -match '[\\/]Editor[\\/]' } | Select-Object -ExpandProperty FullName)
    $runtime = Invoke-Compiler -AssemblyName 'Bastion3D.Offline.Runtime' -Sources $runtimeSources
    if ($Task -ne 'Runtime' -and $editorSources.Count) {
        Invoke-Compiler -AssemblyName 'Bastion3D.Offline.Editor' -Sources $editorSources -ExtraReferences @($runtime) | Out-Null
    }
}
Write-Host 'Offline C# compilation passed. This does not verify Unity import, shaders, serialization, play mode, or a player build.'
