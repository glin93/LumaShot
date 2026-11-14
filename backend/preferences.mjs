import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const hash=s=>createHash('sha256').update(s).digest('hex');
export function validPreference(key,value){
 if(typeof value!=='string'||value.length>120)return false;
 if(/^hotkeys\.(windows|macos)\.(region|window|fullscreen|recordRegion|recordWindow|recordFullscreen|quickHistory|library)$/.test(key))return /^[A-Za-z0-9+]+$/.test(value)&&value.length>0;
 if(/^capture\.(image|video)\.copy$/.test(key))return ['true','false'].includes(value);
 if(/^capture\.(image|video)\.open$/.test(key))return ['none','library','folder'].includes(value);
 if(/^recording\.(microphone|systemAudio)$/.test(key))return ['true','false'].includes(value);
 return false;
}
// Shared settings use platform keys, never device IDs. Only explicit overrides are published.
export class PreferenceSync {
 constructor(root){this.dir=path.join(root,'sync-store','settings');this.statePath=path.join(root,'preferences-sync-state.json');this.state={clock:0,values:{}};}
 async init(){await fs.mkdir(this.dir,{recursive:true});try{this.state=JSON.parse(await fs.readFile(this.statePath,'utf8'));}catch(e){if(e.code!=='ENOENT')throw new Error('配置同步状态无法读取。');}}
 async save(){const tmp=this.statePath+'.tmp';await fs.writeFile(tmp,JSON.stringify(this.state));await fs.rename(tmp,this.statePath);}
 async publish(values){await this.init();if(!values||Array.isArray(values)||typeof values!=='object')throw new Error('同步配置格式无效。');
  for(const [key,value] of Object.entries(values)){if(!validPreference(key,value))throw new Error('同步配置包含无效设置。');if(this.state.values[key]===value)continue;const doc={version:1,key,value,clock:++this.state.clock,nonce:randomUUID()};const data=JSON.stringify(doc);const file=path.join(this.dir,hash(data)+'.json');await fs.writeFile(file+'.tmp',data);await fs.rename(file+'.tmp',file);this.state.values[key]=value;}await this.save();
 }
 async merge(){const winners=new Map();const files=await fs.readdir(this.dir);if(files.length>100000)throw new Error('配置历史过大。');
  for(const file of files){if(!/^[a-f0-9]{64}\.json$/.test(file))continue;const full=path.join(this.dir,file);if((await fs.stat(full)).size>2048)throw new Error('同步配置大小异常。');const data=await fs.readFile(full,'utf8');const id=file.slice(0,-5);if(hash(data)!==id)throw new Error('同步配置校验失败。');const d=JSON.parse(data);if(d.version!==1||!validPreference(d.key,d.value)||!Number.isSafeInteger(d.clock)||d.clock<1)throw new Error('同步配置格式无效。');this.state.clock=Math.max(this.state.clock,d.clock);const old=winners.get(d.key);if(!old||d.clock>old.clock||d.clock===old.clock&&id>old.id)winners.set(d.key,{...d,id});}
  const values=Object.fromEntries([...winners].map(([k,v])=>[k,v.value]));this.state.values=values;await this.save();return values;
 }
}
