import fs from 'node:fs/promises';
import {createReadStream} from 'node:fs';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const hash=s=>createHash('sha256').update(s).digest('hex');
const guid=/^[a-f0-9]{32}$/;
const digest=/^[a-f0-9]{64}$/;
const fields=['Id','ParentId','Title','Tags','Notes','Created','Kind','Star','Deleted','Width','Height','OcrText','AiHistory'];
const strings=['ParentId','Title','Tags','Notes','Created','Kind','OcrText','AiHistory'];
export function metadata(x){
 if(!x||!guid.test(x.Id)||!['image','video','gif'].includes(x.Kind))throw new Error('同步记录格式无效。');
 const result={};for(const k of fields)result[k]=strings.includes(k)?String(x[k]??''):k==='Star'||k==='Deleted'?x[k]===true:k==='Width'||k==='Height'?Number(x[k]||0):x[k];
 if(JSON.stringify(result).length>2*1024*1024)throw new Error('同步记录过大。');
 return result;
}
function stamp(x){return hash(JSON.stringify({...metadata(x),File:x.File,Original:x.Original,Thumb:x.Thumb}));}
async function fileHash(file){const h=createHash('sha256');for await(const chunk of createReadStream(file))h.update(chunk);return h.digest('hex');}
async function writeAtomic(file,value){const tmp=file+'.'+randomUUID()+'.tmp';await fs.writeFile(tmp,value);await fs.rename(tmp,file);}
export class SyncStore {
 constructor(root){this.root=path.resolve(root);this.dir=path.join(this.root,'sync-store');this.statePath=path.join(this.root,'sync-state.json');this.state={last:{},heads:{},records:{},assets:{},clock:0};}
 async init(){for(const dir of ['objects','revisions'])await fs.mkdir(path.join(this.dir,dir),{recursive:true});try{this.state=JSON.parse(await fs.readFile(this.statePath,'utf8'));}catch(e){if(e.code!=='ENOENT')throw new Error('同步状态损坏。请保留资料库并从备份恢复。');}}
 local(relative){if(typeof relative!=='string'||! /^(captures|thumbs)[\\/][^:]+$/.test(relative))throw new Error('同步文件路径无效。');const p=path.resolve(this.root,relative);if(!p.toLowerCase().startsWith(this.root.toLowerCase()+path.sep))throw new Error('同步文件路径越界。');return p;}
 async asset(relative){
  const file=this.local(relative);const ext=path.extname(file).toLowerCase();if(!['.png','.jpg','.jpeg','.bmp','.gif','.mp4','.webm','.avi'].includes(ext))throw new Error('同步文件类型不受支持。');
  const sha=await fileHash(file);const dest=path.join(this.dir,'objects',sha);try{await fs.access(dest);}catch{await fs.copyFile(file,dest+'.tmp');await fs.rename(dest+'.tmp',dest);}return {sha,ext};
 }
 async publish(records){
  await this.init();if(!Array.isArray(records)||records.length>100000)throw new Error('同步记录数量异常。');
  for(const x of records){const value=stamp(x);if(this.state.last[x.Id]===value)continue;
    const assets={};for(const name of ['File','Original','Thumb'])assets[name]=await this.asset(x[name]);
    const record=metadata(x),previous=this.state.records[x.Id];
    if(this.state.assets[x.Id]&&JSON.stringify(this.state.assets[x.Id])!==JSON.stringify(assets))throw new Error('截图文件不可覆盖。编辑后请保存为新的截图记录。');
    const changes=fields.filter(k=>k!=='Id'&&(!previous||previous[k]!==record[k]));
    const doc={version:2,clock:++this.state.clock,nonce:randomUUID(),parents:[...(this.state.heads[x.Id]||[])].sort(),record,assets,changes};
    const data=JSON.stringify(doc),id=hash(data);await writeAtomic(path.join(this.dir,'revisions',id+'.json'),data);this.state.heads[x.Id]=[id];this.state.last[x.Id]=value;this.state.records[x.Id]=record;this.state.assets[x.Id]=assets;
  }
  await writeAtomic(this.statePath,JSON.stringify(this.state));
 }
 async merge(){
  const all=new Map(),groups=new Map();
  const files=await fs.readdir(path.join(this.dir,'revisions'));if(files.length>200000)throw new Error('同步历史过大，需要归档后再同步。');
  for(const file of files){
   if(!/^[a-f0-9]{64}\.json$/.test(file))continue;
   const source=path.join(this.dir,'revisions',file);if((await fs.stat(source)).size>3*1024*1024)throw new Error('远端同步记录过大。');
   const data=await fs.readFile(source,'utf8');const id=file.slice(0,-5);if(hash(data)!==id)throw new Error('同步记录校验失败，已停止合并。');
   const d=JSON.parse(data);if(d.version!==2||!Number.isSafeInteger(d.clock)||d.clock<1||!Array.isArray(d.changes)||d.changes.some(k=>k==='Id'||!fields.includes(k))||!Array.isArray(d.parents)||d.parents.length>100||d.parents.some(p=>!digest.test(p)))throw new Error('远端同步版本无效。');metadata(d.record);
   for(const k of ['File','Original','Thumb']){const a=d.assets?.[k];if(!a||!digest.test(a.sha)||!['.png','.jpg','.jpeg','.bmp','.gif','.mp4','.webm','.avi'].includes(a.ext))throw new Error('远端媒体引用无效。');}
   all.set(id,d);if(!groups.has(d.record.Id))groups.set(d.record.Id,new Set());groups.get(d.record.Id).add(id);
  }
  const versions=new Map();for(const [id,d] of all){this.state.clock=Math.max(this.state.clock,d.clock);if(!versions.has(d.record.Id))versions.set(d.record.Id,[]);versions.get(d.record.Id).push([id,d]);for(const parent of d.parents){if(!all.has(parent))throw new Error('同步历史尚未下载完整，稍后将重试。');if(all.get(parent).record.Id!==d.record.Id||all.get(parent).clock>=d.clock)throw new Error('同步历史关联无效。');groups.get(d.record.Id).delete(parent);}}
  const records=new Map(),verified=new Set();
  const materialize=async(d)=>{
   const record=metadata(d.record);record.SourceKey=null;
   for(const key of ['File','Original','Thumb']){
    const a=d.assets[key],src=path.join(this.dir,'objects',a.sha);
    if(!verified.has(a.sha)){try{if(await fileHash(src)!==a.sha)throw new Error('hash');}catch{throw new Error('同步媒体缺失或校验失败，稍后将重试。');}verified.add(a.sha);}
    const relative=(key==='Thumb'?'thumbs':'captures')+'/sync-'+a.sha+a.ext;const dest=this.local(relative);
    await fs.mkdir(path.dirname(dest),{recursive:true});let matches=false;try{matches=await fileHash(dest)===a.sha;}catch{}
    if(!matches){await fs.copyFile(src,dest+'.tmp');await fs.rename(dest+'.tmp',dest);}record[key]=relative;
   }return record;
  };
  for(const [recordId,heads] of groups){
   const sorted=[...heads].sort();if(!sorted.length)throw new Error('同步历史包含循环。');
   // Captures are immutable. Edits have new IDs/files; only metadata for the same capture merges.
   // Per-field Lamport order preserves independent changes without relying on device wall clocks.
   const ordered=versions.get(recordId).sort((a,b)=>a[1].clock-b[1].clock||a[0].localeCompare(b[0]));
   const merged=structuredClone(ordered[0][1]);merged.record=metadata(merged.record);
   const immutableAssets=JSON.stringify(merged.assets),messages=new Map();
   for(const [,d] of ordered){if(JSON.stringify(d.assets)!==immutableAssets)throw new Error('同一截图出现了不同文件。请将编辑结果作为新截图导入。');for(const k of d.changes)merged.record[k]=metadata(d.record)[k];
    if(d.record.AiHistory)try{for(const m of JSON.parse(d.record.AiHistory)){if(m&&typeof m.content==='string')messages.set(hash(JSON.stringify(m)),m);}}catch{}
   }
   if(messages.size)merged.record.AiHistory=JSON.stringify([...messages.values()].sort((a,b)=>String(a.created||'').localeCompare(String(b.created||''))));
   const record=await materialize(merged);records.set(recordId,record);this.state.records[recordId]=metadata(record);this.state.assets[recordId]=merged.assets;
   this.state.heads[recordId]=sorted;
  }
  const result=[...records.values()];for(const r of result)this.state.last[r.Id]=stamp(r);
  await writeAtomic(this.statePath,JSON.stringify(this.state));return result;
 }
}
