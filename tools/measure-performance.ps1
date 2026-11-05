<#
.SYNOPSIS
    Measures a running Flylet dev build and saves the numbers as JSON so two
    builds can be compared.

.DESCRIPTION
    Takes a single snapshot of a running "Flylet" process whose Path contains
    "\Flylet.Package\bin\":
      - PrivateMemorySize64 and WorkingSet64 (MB, 1 decimal)
      - HandleCount
      - Threads.Count
      - GDI and USER object counts (GetGuiResources via Add-Type, flags 0 and 1)
    Measures idle CPU over -IdleSeconds (default 60) and reports the CPU
    milliseconds used and the percent of one core.
    Adds the size (MB) and file count of -BuildFolder.

    Results are written as JSON to -OutFile and printed.

    Use -Compare with two JSON files to print each number side by side with the
    difference and percent change.

    This script only reads from the matched process. It never starts, stops,
    or touches any other Flylet process, and performs no downloads, installs,
    or deletions.

.EXAMPLE
    pwsh -File tools/measure-performance.ps1 -IdleSeconds 5

.EXAMPLE
    pwsh -File tools/measure-performance.ps1 -Compare buildA.json buildB.json

.NOTES
    Runs on PowerShell 7 and Windows PowerShell 5.1.
#>
[CmdletBinding()]
param(
    [int]$IdleSeconds = 60,
    [string]$BuildFolder = "Flylet.Package\bin\x64\Release",
    [string]$OutFile,
    [switch]$Compare,
    [Parameter(Position = 0)][string]$FileA,
    [Parameter(Position = 1)][string]$FileB
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Compare mode: print two JSON snapshots side by side, then exit.
# ---------------------------------------------------------------------------
if ($Compare) {
    if (-not $FileA -or -not $FileB) {
        Write-Error "Compare mode needs two files: -Compare <a.json> <b.json>"
        exit 2
    }

    $aPath = (Resolve-Path -Path $FileA -ErrorAction Stop).Path
    $bPath = (Resolve-Path -Path $FileB -ErrorAction Stop).Path

    $a = Get-Content -LiteralPath $aPath -Raw | ConvertFrom-Json
    $b = Get-Content -LiteralPath $bPath -Raw | ConvertFrom-Json

    $ci = [System.Globalization.CultureInfo]::InvariantCulture

    # Each entry: Label, property name, and whether to format as MB (1 decimal).
    $metrics = @(
        [pscustomobject]@{ Label = 'ProcessId';        Prop = 'ProcessId'        }
        [pscustomobject]@{ Label = 'ProcessPath';      Prop = 'ProcessPath'      }
        [pscustomobject]@{ Label = 'PrivateMemory MB'; Prop = 'PrivateMemorySize64MB' }
        [pscustomobject]@{ Label = 'WorkingSet MB';    Prop = 'WorkingSet64MB'   }
        [pscustomobject]@{ Label = 'HandleCount';      Prop = 'HandleCount'      }
        [pscustomobject]@{ Label = 'Threads';          Prop = 'ThreadCount'      }
        [pscustomobject]@{ Label = 'GDI objects';      Prop = 'GDIObjects'       }
        [pscustomobject]@{ Label = 'USER objects';     Prop = 'UserObjects'      }
        [pscustomobject]@{ Label = 'Idle CPU ms';      Prop = 'IdleCpuMs'        }
        [pscustomobject]@{ Label = 'Idle CPU %';       Prop = 'IdleCpuPercent'   }
        [pscustomobject]@{ Label = 'BuildFolder MB';   Prop = 'BuildFolderMB'    }
        [pscustomobject]@{ Label = 'Build file count'; Prop = 'BuildFileCount'   }
    )

    Write-Output ("{0,-18} {1,14} {2,14} {3,12} {4,10}" -f 'Metric', $aPath, $bPath, 'Diff', '%Change')
    Write-Output ('{0,-18} {1,14} {2,14} {3,12} {4,10}' -f ('-'*18), ('-'*14), ('-'*14), ('-'*12), ('-'*10))

    foreach ($m in $metrics) {
        $va = $a.$($m.Prop)
        $vb = $b.$($m.Prop)

        if ($m.Prop -eq 'ProcessPath') {
            # String value: show both, no numeric diff.
            $diffStr = ''
            $pctStr = ''
            $vaStr = $va
            $vbStr = $vb
        } else {
            $vaNum = [double]($va -as [double])
            $vbNum = [double]($vb -as [double])
            $diff  = $vbNum - $vaNum
            $pct   = if ($vaNum -ne 0) { $diff / [math]::Abs($vaNum) * 100 } else { 0 }

            $vaStr = $vaNum.ToString('N1', $ci)
            $vbStr = $vbNum.ToString('N1', $ci)
            $diffStr = $diff.ToString('+0.0;-0.0;0.0', $ci)
            $pctStr  = ($pct / 100).ToString('+0.0%;-0.0%;0.0%', $ci)
        }

        Write-Output ("{0,-18} {1,14} {2,14} {3,12} {4,10}" -f $m.Label, $vaStr, $vbStr, $diffStr, $pctStr)
    }

    exit 0
}

# ---------------------------------------------------------------------------
# Snapshot mode.
# ---------------------------------------------------------------------------

# 1. Find the running Flylet dev process.
$flylet = Get-Process -Name 'Flylet' -ErrorAction SilentlyContinue `
    | Where-Object { $_.Path -and $_.Path -match '\\Flylet\.Package\\bin\\' } `
    | Select-Object -First 1

if (-not $flylet) {
    Write-Output 'Flylet (Dev) isn''t running'
    exit 1
}

# 2. One reading of that process.
$proc = $flylet
$privateMb = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
$workingMb = [math]::Round($proc.WorkingSet64 / 1MB, 1)
$handleCount = $proc.HandleCount
$threadCount = $proc.Threads.Count

try {
    Add-Type -Namespace Win32 -Name GuiResources -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetGuiResources(System.IntPtr hProcess, uint flags);
"@
} catch {
    # Type may already be defined (e.g. the script was dot-sourced twice).
}
try {
    $gdiObjects = [Win32.GuiResources]::GetGuiResources($proc.Handle, 0)
    $userObjects = [Win32.GuiResources]::GetGuiResources($proc.Handle, 1)
} catch {
    $gdiObjects = -1
    $userObjects = -1
}

# 3. Idle CPU over the window.
$t1 = $proc.TotalProcessorTime
Start-Sleep -Seconds $IdleSeconds
$t2 = $proc.TotalProcessorTime
$idleMs = [math]::Round(($t2 - $t1).TotalMilliseconds, 1)
$idlePercent = [math]::Round($idleMs / ($IdleSeconds * 1000) * 100, 2)

# 4. Build folder size and file count.
$fullBuild = Join-Path -Path $PWD -ChildPath $BuildFolder
$buildBytes = 0
$buildCount = 0
if (Test-Path -LiteralPath $fullBuild) {
    $files = Get-ChildItem -LiteralPath $fullBuild -Recurse -File -ErrorAction SilentlyContinue
    $buildBytes = ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
    $buildCount = $files.Count
    if ($null -eq $buildBytes) { $buildBytes = 0 }
}
$buildMb = [math]::Round($buildBytes / 1MB, 1)

# 5. Assemble, save and print.
$report = [pscustomobject]@{
    Timestamp             = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
    ProcessId             = $proc.Id
    ProcessPath           = $proc.Path
    PrivateMemorySize64MB = $privateMb
    WorkingSet64MB        = $workingMb
    HandleCount           = $handleCount
    ThreadCount           = $threadCount
    GDIObjects            = $gdiObjects
    UserObjects           = $userObjects
    IdleSeconds           = $IdleSeconds
    IdleCpuMs             = $idleMs
    IdleCpuPercent        = $idlePercent
    BuildFolder           = $fullBuild
    BuildFolderMB         = $buildMb
    BuildFileCount        = $buildCount
}

if (-not $OutFile) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutFile = "perf-$stamp.json"
}
$fullOut = Join-Path -Path $PWD -ChildPath $OutFile

$json = $report | ConvertTo-Json -Depth 10
$json | Out-File -FilePath $fullOut -Encoding utf8

Write-Output "Saved: $fullOut"
Write-Output $json
