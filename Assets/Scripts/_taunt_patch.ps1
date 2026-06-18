param(
    [string]$FilePath
)

$content = [System.IO.File]::ReadAllText($FilePath)
$content = $content -replace 'ShakeCamera = 14,\r\n\r\n    Vfx = 20,', 'ShakeCamera = 14,

    TauntApply = 30,

    Vfx = 20,'
$content = $content -replace 'case CombatTimelineEventName.Vfx:', 'case CombatTimelineEventName.TauntApply:
                return TauntApply;
            case CombatTimelineEventName.Vfx:'
[System.IO.File]::WriteAllText($FilePath, $content, [System.Text.Encoding]::UTF8)
Write-Output 'DONE'
