// Real authorization startup; follows only the local redirect, never signs in.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {run} from '../process.mjs';
import {rcloneConfigPath} from '../cloud.mjs';
const base=path.resolve(fileURLToPath(new URL('../..',import.meta.url)));
const root=path.resolve(process.argv[2]);await fs.mkdir(root,{recursive:true});
const rclone=path.join(base,'sync-tools',process.platform==='win32'?'rclone.exe':'rclone');
const check=await run(rclone,['listremotes','--config',rcloneConfigPath]);
assert.equal(check.code,0,'rclone must accept the production config path');
assert.equal(check.out.trim(),'','the isolated config must not load user remotes');
const config=Buffer.from(JSON.stringify({scope:'drive.file'})).toString('base64url');
const child=spawn(rclone,['authorize','drive',config,'--config',rcloneConfigPath,'--auth-no-open-browser'],{windowsHide:true,stdio:['pipe','pipe','pipe']});
const exited=once(child,'close');child.stdin.end();let error='';
try {
 const url=await new Promise((resolve,reject)=>{
  const timer=setTimeout(()=>reject(new Error('OAuth listener did not start within 15 seconds')),15000);
  child.on('error',()=>{clearTimeout(timer);reject(new Error('Unable to launch rclone'));});
  child.on('close',code=>{clearTimeout(timer);reject(new Error('Authorization exited before browser handoff: '+code));});
  child.stderr.on('data',data=>{error+=data;const match=error.match(/http:\/\/127\.0\.0\.1:\d+\/auth\?[^\s"<>]+/);if(match){clearTimeout(timer);resolve(match[0]);}});
 });
 const response=await fetch(url,{redirect:'manual',signal:AbortSignal.timeout(5000)});
 assert.equal(response.status,307);
 const destination=new URL(response.headers.get('location'));
 assert.equal(destination.protocol,'https:');assert.equal(destination.hostname,'accounts.google.com');
 assert(destination.searchParams.get('scope').includes('drive.file'));
 assert.equal(new URL(destination.searchParams.get('redirect_uri')).hostname,'127.0.0.1');
 const report='PASS bundled rclone accepts the shared production empty-config path\nPASS Drive OAuth listener starts and redirects to Google with drive.file scope and loopback callback; no browser or account login used\n';
 await fs.writeFile(path.join(root,'test-results.txt'),report);console.log(report);
} finally {child.kill();await exited;}
