// Real rclone/WebDAV transport over loopback; no external account or network destination.
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createServer} from 'node:net';
import {spawn} from 'node:child_process';
import assert from 'node:assert/strict';
import {run} from '../process.mjs';
import {syncFilters} from '../cloud.mjs';
import {PreferenceSync} from '../preferences.mjs';
const base=path.resolve(fileURLToPath(new URL('../..',import.meta.url)));
const root=path.resolve(process.argv[2]);await fs.mkdir(root,{recursive:true});
const reserve=createServer();await new Promise(r=>reserve.listen(0,'127.0.0.1',r));const port=reserve.address().port;await new Promise(r=>reserve.close(r));
const remote=path.join(root,'remote'),source=path.join(root,'source'),dest=path.join(root,'download');for(const p of [remote,source,dest])await fs.mkdir(p,{recursive:true});await fs.writeFile(path.join(source,'fixture.txt'),'LumaShot WebDAV roundtrip');
const rclone=path.join(base,'sync-tools','rclone.exe');const server=spawn(rclone,['serve','webdav',remote,'--addr','127.0.0.1:'+port,'--config','NUL'],{windowsHide:true,stdio:'ignore'});
const env={...process.env,RCLONE_CONFIG_TEST_TYPE:'webdav',RCLONE_CONFIG_TEST_URL:'http://127.0.0.1:'+port,RCLONE_CONFIG_TEST_VENDOR:'other'};
try{
 let ready=false;for(let i=0;i<40;i++){try{await fetch('http://127.0.0.1:'+port);ready=true;break;}catch{await new Promise(r=>setTimeout(r,50));}}assert(ready);
 let result=await run(rclone,['copy',source,'test:LumaShot','--config','NUL','--ignore-existing'],{env});assert.equal(result.code,0,result.err);
 result=await run(rclone,['copy','test:LumaShot',dest,'--config','NUL','--ignore-existing'],{env});assert.equal(result.code,0,result.err);assert.equal(await fs.readFile(path.join(dest,'fixture.txt'),'utf8'),'LumaShot WebDAV roundtrip');
 const prefs=new PreferenceSync(path.join(root,'prefs'));await prefs.publish({'hotkeys.windows.recordWindow':'Ctrl+Alt+9','hotkeys.macos.recordWindow':'Shift+Cmd+8','recording.systemAudio':'false'});await fs.writeFile(path.join(prefs.dir,'partial.tmp'),'incomplete');
 result=await run(rclone,['copy',prefs.dir,'test:LumaShot/settings','--config','NUL','--ignore-existing',...syncFilters],{env});assert.equal(result.code,0,result.err);
 const received=new PreferenceSync(path.join(root,'prefs-received'));await received.init();result=await run(rclone,['copy','test:LumaShot',path.dirname(received.dir),'--config','NUL','--ignore-existing',...syncFilters],{env});assert.equal(result.code,0,result.err);assert.deepEqual(await received.merge(),await prefs.merge());assert(!(await fs.readdir(received.dir)).includes('partial.tmp'));
 const report='PASS real rclone upload/download over local WebDAV\nPASS platform shortcuts and audio settings roundtrip through production transfer filters; partial files excluded\n';await fs.writeFile(path.join(root,'test-results.txt'),report);console.log(report);
}finally{server.kill();}
