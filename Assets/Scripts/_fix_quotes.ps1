param(
    [string]$FilePath
)
$content = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
$content = $content -replace 'return qqHitStartqq;', 'return " HitStart\;'
$content = $content -replace 'return qqHitEndqq;', 'return \HitEnd\;'
$content = $content -replace 'return qqFootStepqq;', 'return \FootStep\;'
$content = $content -replace 'return qqSpawnEffectqq;', 'return \SpawnEffect\;'
$content = $content -replace 'return qqShakeCameraqq;', 'return \ShakeCamera\;'
$content = $content -replace 'return qqTauntApplyqq;', 'return \TauntApply\;'
$content = $content -replace 'return qqVfxqq;', 'return \Vfx\;'
$content = $content -replace 'return qqPreCastOpenqq;', 'return \PreCastOpen\;'
$content = $content -replace 'return qqPreCastCloseqq;', 'return \PreCastClose\;'
[System.IO.File]::WriteAllText($FilePath, $content, [System.Text.Encoding]::UTF8)
