[CmdletBinding()]
param(
    [ValidateSet("Mono", "Il2cpp")]
    [string]$Runtime = "Mono",

    [string]$MonoGamePath = "D:\SteamLibrary\steamapps\common\Schedule I_alternate",

    [string]$Il2CppGamePath = "D:\SteamLibrary\steamapps\common\Schedule I_public",

    [string]$OutputRoot = "",

    [ValidateRange(30, 300)]
    [int]$TimeoutSeconds = 210,

    [switch]$AllowLiveInstallMutation,

    [switch]$AvatarOnly,

    [switch]$PalletAlignmentOnly,

    [switch]$EmployeeTransitOnly,

    [switch]$LoadingDockOnly,

    [string]$SourceSavePath = "",

    [switch]$KeepProcess
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot "..\artifacts\smoke"
}

if (-not $AllowLiveInstallMutation) {
    throw "This runner temporarily replaces the selected live install's Mods directory. Re-run with -AllowLiveInstallMutation to acknowledge the reversible mutation."
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$gamePath = if ($Runtime -eq "Mono") { $MonoGamePath } else { $Il2CppGamePath }
$gamePath = [System.IO.Path]::GetFullPath($gamePath)
$gameExe = Join-Path $gamePath "Schedule I.exe"
$modsPath = Join-Path $gamePath "Mods"
$runId = "{0}-{1}" -f $Runtime.ToLowerInvariant(), (Get-Date -Format "yyyyMMdd-HHmmss")
$runDirectory = [System.IO.Path]::GetFullPath((Join-Path $OutputRoot $runId))
$backupModsPath = Join-Path $gamePath ("Mods.S1ForkliftBackup." + $runId)
$temporaryModsPath = Join-Path $gamePath ("Mods.S1ForkliftTemporary." + $runId)
$buildPath = if ($Runtime -eq "Mono") {
    Join-Path $projectRoot "bin\Mono\netstandard2.1\ForkliftandPallets_Mono.dll"
} else {
    Join-Path $projectRoot "bin\Il2cpp\net6.0\ForkliftandPallets_Il2Cpp.dll"
}
$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot ".."))
$s1ApiBuildPath = if ($Runtime -eq "Mono") {
    Join-Path $workspaceRoot "S1API\S1API\bin\MonoMelon\netstandard2.1\S1API.dll"
} else {
    Join-Path $workspaceRoot "S1API\S1API\bin\Il2CppMelon\net6.0\S1API.dll"
}
$s1ApiDeployName = if ($Runtime -eq "Mono") {
    "S1API.Mono.MelonLoader.dll"
} else {
    "S1API.Il2Cpp.MelonLoader.dll"
}
$resultPath = Join-Path $runDirectory "result.txt"
$playerLogPath = Join-Path $runDirectory "Player.log"
$process = $null
$movedMods = $false
$createdMods = $false

function Assert-DescendantPath {
    param(
        [Parameter(Mandatory)]
        [string]$Candidate,

        [Parameter(Mandatory)]
        [string]$Parent
    )

    $candidateFull = [System.IO.Path]::GetFullPath($Candidate)
    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (-not $candidateFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing filesystem mutation outside '$parentFull': '$candidateFull'."
    }
}

if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
    throw "Game executable not found: $gameExe"
}
if (-not (Test-Path -LiteralPath $buildPath -PathType Leaf)) {
    throw "Matching build output not found: $buildPath"
}
if (-not (Test-Path -LiteralPath $s1ApiBuildPath -PathType Leaf)) {
    throw "Matching S1API dependency not found: $s1ApiBuildPath"
}
if (($EmployeeTransitOnly -or $LoadingDockOnly) -and [string]::IsNullOrWhiteSpace($SourceSavePath)) {
    throw "Employee transit and loading-dock smokes require -SourceSavePath."
}
if (-not [string]::IsNullOrWhiteSpace($SourceSavePath)) {
    $SourceSavePath = [System.IO.Path]::GetFullPath($SourceSavePath)
    if (-not (Test-Path -LiteralPath $SourceSavePath -PathType Container)) {
        throw "Source save folder not found: $SourceSavePath"
    }
}
if (Test-Path -LiteralPath $backupModsPath) {
    throw "Run-specific backup path already exists: $backupModsPath"
}

$existingProcesses = Get-CimInstance Win32_Process |
    Where-Object {
        $_.ExecutablePath -and
        [string]::Equals(
            [System.IO.Path]::GetFullPath($_.ExecutablePath),
            [System.IO.Path]::GetFullPath($gameExe),
            [System.StringComparison]::OrdinalIgnoreCase)
    }
if ($existingProcesses) {
    throw "The selected Schedule I install is already running. Refusing to mutate its Mods directory."
}

New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
Set-Content -LiteralPath (Join-Path $runDirectory "parameters.txt") -Value @(
    "Runtime=$Runtime"
    "GamePath=$gamePath"
    "BuildPath=$buildPath"
    "RunId=$runId"
    "EmployeeTransitOnly=$EmployeeTransitOnly"
    "LoadingDockOnly=$LoadingDockOnly"
    "SourceSavePath=$SourceSavePath"
)

try {
    Assert-DescendantPath -Candidate $modsPath -Parent $gamePath
    Assert-DescendantPath -Candidate $backupModsPath -Parent $gamePath
    Assert-DescendantPath -Candidate $temporaryModsPath -Parent $gamePath

    if (Test-Path -LiteralPath $modsPath -PathType Container) {
        Get-ChildItem -LiteralPath $modsPath -File |
            Select-Object Name, Length, LastWriteTime |
            Format-Table -AutoSize |
            Out-String |
            Set-Content -LiteralPath (Join-Path $runDirectory "original-mods.txt")
        Move-Item -LiteralPath $modsPath -Destination $backupModsPath
        $movedMods = $true
    }

    New-Item -ItemType Directory -Path $modsPath | Out-Null
    $createdMods = $true
    Copy-Item -LiteralPath $buildPath -Destination (Join-Path $modsPath ([System.IO.Path]::GetFileName($buildPath)))
    Copy-Item -LiteralPath $s1ApiBuildPath -Destination (Join-Path $modsPath $s1ApiDeployName)

    $deployed = @(Get-ChildItem -LiteralPath $modsPath -File)
    if ($deployed.Count -ne 2 -or
        $deployed.Name -notcontains [System.IO.Path]::GetFileName($buildPath) -or
        $deployed.Name -notcontains $s1ApiDeployName) {
        throw "Install audit failed: the temporary Mods directory does not contain exactly the forklift and S1API DLLs."
    }

    $arguments = @(
        "--s1forklift-smoke",
        "--s1forklift-smoke-dir",
        "`"$runDirectory`"",
        "-screen-width",
        "1280",
        "-screen-height",
        "720",
        "-screen-fullscreen",
        "0",
        "-logFile",
        "`"$playerLogPath`""
    )
    if ($AvatarOnly) {
        $arguments += "--s1forklift-avatar-smoke"
    }
    if ($PalletAlignmentOnly) {
        $arguments += "--s1forklift-pallet-alignment-smoke"
    }
    if ($EmployeeTransitOnly) {
        $arguments += "--s1forklift-employee-transit-smoke"
    }
    if ($LoadingDockOnly) {
        $arguments += "--s1forklift-loading-dock-smoke"
    }
    if ($EmployeeTransitOnly -or $LoadingDockOnly) {
        $arguments += "--s1forklift-smoke-source-save"
        $arguments += "`"$SourceSavePath`""
    }
    $process = Start-Process `
        -FilePath $gameExe `
        -ArgumentList $arguments `
        -WorkingDirectory $gamePath `
        -WindowStyle Hidden `
        -PassThru

    $started = Get-Date
    $lastProgress = [datetime]::MinValue
    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds) {
        if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
            break
        }

        $process.Refresh()
        if ($process.HasExited) {
            throw "Game exited before writing a smoke result. ExitCode=$($process.ExitCode)."
        }

        if (((Get-Date) - $lastProgress).TotalSeconds -ge 5) {
            $lastProgress = Get-Date
            $latestLog = Join-Path $gamePath "MelonLoader\Latest.log"
            $state = if (Test-Path -LiteralPath $latestLog) {
                Get-Content -LiteralPath $latestLog -Tail 200 |
                    Select-String -Pattern "\[S1ForkliftSmoke\].*STATE" |
                    Select-Object -Last 1 |
                    ForEach-Object { $_.Line }
            }
            $stateText = if ($state) { $state } else { "Starting" }
            Write-Host ("WAIT|Elapsed={0:N1}s|PID={1}|State={2}" -f
                ((Get-Date) - $started).TotalSeconds,
                $process.Id,
                $stateText)
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Timed out waiting for '$resultPath'."
    }

    $result = Get-Content -Raw -LiteralPath $resultPath
    Write-Host $result
    if (-not $result.StartsWith("PASS|", [System.StringComparison]::Ordinal)) {
        throw "Smoke probe reported failure: $result"
    }
}
finally {
    $latestLog = Join-Path $gamePath "MelonLoader\Latest.log"
    if (Test-Path -LiteralPath $latestLog -PathType Leaf) {
        Copy-Item -LiteralPath $latestLog -Destination (Join-Path $runDirectory "MelonLoader-Latest.log") -Force
    }

    if ($process -and -not $KeepProcess) {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id
            $process.WaitForExit(15000)
        }
    }

    if ($createdMods -and (Test-Path -LiteralPath $modsPath -PathType Container)) {
        Assert-DescendantPath -Candidate $modsPath -Parent $gamePath
        Move-Item -LiteralPath $modsPath -Destination $temporaryModsPath
    }

    if ($movedMods -and (Test-Path -LiteralPath $backupModsPath -PathType Container)) {
        Assert-DescendantPath -Candidate $backupModsPath -Parent $gamePath
        Move-Item -LiteralPath $backupModsPath -Destination $modsPath
    }

    if (Test-Path -LiteralPath $temporaryModsPath -PathType Container) {
        for ($attempt = 1; $attempt -le 10; $attempt++) {
            try {
                Remove-Item -LiteralPath $temporaryModsPath -Recurse -Force
                break
            }
            catch {
                if ($attempt -eq 10) {
                    Write-Warning "Temporary Mods cleanup remains at '$temporaryModsPath': $($_.Exception.Message)"
                }
                else {
                    Start-Sleep -Milliseconds 500
                }
            }
        }
    }
}
