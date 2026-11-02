param([switch]$SkipBuild,[string]$PostgresBin="C:\Program Files\PostgreSQL\17\bin")
$ErrorActionPreference="Stop"
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$localDir=Join-Path $projectRoot ".local"
$dbDir=Join-Path $localDir "postgres"
New-Item -ItemType Directory -Force -Path $localDir | Out-Null
$passwordPath=Join-Path $localDir "db-password.txt"
if(!(Test-Path -LiteralPath $passwordPath)) {
 $bytes=New-Object byte[] 32
 [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
 [System.IO.File]::WriteAllText($passwordPath,[Convert]::ToBase64String($bytes))
}
$dbPassword=[System.IO.File]::ReadAllText($passwordPath).Trim()
if(!(Test-Path -LiteralPath (Join-Path $dbDir "PG_VERSION"))) {
 & (Join-Path $PostgresBin "initdb.exe") -D $dbDir -U rally --auth=scram-sha-256 "--pwfile=$passwordPath" --encoding=UTF8 --locale=C
 if($LASTEXITCODE -ne 0){throw "Database initialization failed."}
 Add-Content -LiteralPath (Join-Path $dbDir "postgresql.conf") -Value "`nlisten_addresses = '127.0.0.1'`nport = 55432`ntimezone = 'UTC'`n"
}
& (Join-Path $PostgresBin "pg_isready.exe") -h 127.0.0.1 -p 55432 -t 2
if($LASTEXITCODE -ne 0) {
 $pgLog=Join-Path $localDir "postgres.log"
 $process=Start-Process -FilePath (Join-Path $PostgresBin "pg_ctl.exe") -ArgumentList @("-D",('"' + $dbDir + '"'),"-l",('"' + $pgLog + '"'),"-w","start") -WindowStyle Hidden -PassThru
 $process.WaitForExit()
 if($process.ExitCode -ne 0){throw "PostgreSQL could not start. See .local/postgres.log."}
}
$env:PGPASSWORD=$dbPassword
$exists=& (Join-Path $PostgresBin "psql.exe") -h 127.0.0.1 -p 55432 -U rally -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='rally'"
if($LASTEXITCODE -ne 0){throw "Cannot connect to the project database."}
if("$exists".Trim() -ne "1"){& (Join-Path $PostgresBin "createdb.exe") -h 127.0.0.1 -p 55432 -U rally rally;if($LASTEXITCODE -ne 0){throw "Database creation failed."}}
Remove-Item Env:PGPASSWORD
if(!$SkipBuild){
 Push-Location (Join-Path $projectRoot "web")
 try{
  if(!(Test-Path -LiteralPath "node_modules")){& npm.cmd ci;if($LASTEXITCODE -ne 0){throw "Frontend dependency installation failed."}}
  & npm.cmd run build;if($LASTEXITCODE -ne 0){throw "Frontend build failed."}
 }finally{Pop-Location}
 & dotnet build server/Rally.Api/Rally.Api.csproj
 if($LASTEXITCODE -ne 0){throw "Backend build failed."}
}
$env:ConnectionStrings__Rally="Host=127.0.0.1;Port=55432;Database=rally;Username=rally;Password=$dbPassword"
$env:ASPNETCORE_ENVIRONMENT="Development"
Write-Host "Rally: http://127.0.0.1:5080"
& dotnet run --no-build --project server/Rally.Api/Rally.Api.csproj --urls http://127.0.0.1:5080
