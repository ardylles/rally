import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../', import.meta.url));
function run(command, args, cwd = root) {
  const result = spawnSync(command, args, { cwd, stdio: 'inherit', shell: false });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
// npm_execpath works on Windows without interpolating paths into a shell.
run(process.execPath, [process.env.npm_execpath, '--prefix', 'web', 'run', 'build']);
run('dotnet', ['publish', 'server/Rally.Api/Rally.Api.csproj', '-c', 'Release',
  '-o', '.local/publish', '/p:UseAppHost=false', '--disable-build-servers', '-p:UseSharedCompilation=false']);
console.log('Rally build passed: React + ASP.NET Core Release.');
