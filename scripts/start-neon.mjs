import { readFileSync, existsSync } from 'node:fs';
import { parseEnv } from 'node:util';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const env = { ...process.env };
// The CLI writes .env if it exists, otherwise .env.local. Runtime env wins.
for (const file of ['.env.local', '.env']) {
  const path = new URL('../' + file, import.meta.url);
  if (existsSync(path)) {
    for (const [key, value] of Object.entries(parseEnv(readFileSync(path, 'utf8'))))
      if (!(key in env)) env[key] = value;
  }
}
if (!env.DATABASE_URL || !env.DATABASE_URL_UNPOOLED)
  throw new Error('Run neon deploy to populate DATABASE_URL and DATABASE_URL_UNPOOLED first.');
// This launcher explicitly targets Neon. The native and Compose launchers retain their configuration.
delete env.ConnectionStrings__Rally;
delete env.Database__Password;
const port = env.RALLY_PORT || '5082';
if (!/^\d+$/.test(port) || Number(port) < 1 || Number(port) > 65535)
  throw new Error('RALLY_PORT must be a valid port number.');
env.ASPNETCORE_ENVIRONMENT ||= 'Production';
env.DataProtection__KeysPath ||= fileURLToPath(new URL('../.local/neon-keys/', import.meta.url));
const published = new URL('../.local/publish/', import.meta.url);
if (!existsSync(new URL('Rally.Api.dll', published)))
  throw new Error('Run npm run build first.');
console.log('Starting Rally with Neon PostgreSQL at http://127.0.0.1:' + port);
console.log('Database: pooled requests; direct schema initialization; credentials not logged.');
const child = spawn('dotnet', ['Rally.Api.dll', '--urls', 'http://127.0.0.1:' + port], {
  cwd: fileURLToPath(published), env, stdio: 'inherit', shell: false
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => child.kill(signal));
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 0; });
