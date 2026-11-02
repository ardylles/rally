param([string]$PostgresBin="C:\Program Files\PostgreSQL\17\bin")
$projectRoot=Split-Path $PSScriptRoot -Parent
& (Join-Path $PostgresBin "pg_ctl.exe") -D (Join-Path $projectRoot ".local/postgres") -m fast -w stop
