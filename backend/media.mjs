import fs from 'node:fs/promises';
import path from 'node:path';
import {randomUUID} from 'node:crypto';
import {run} from './process.mjs';
export async function inspectMedia(ffmpeg,file,signal) {
  const result=await run(ffmpeg,['-hide_banner','-i',file],{timeout:30000,signal});
  const m=result.err.match(/Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)/);
  const duration=m?Number(m[1])*3600+Number(m[2])*60+Number(m[3]):0;
  const br=result.err.match(/bitrate:\s*(\d+) kb\/s/);
  return {duration,bitrateKbps:br?Number(br[1]):null,bytes:(await fs.stat(file)).size};
}
export async function prepareMedia(ffmpeg,file,isVideo,tempRoot,signal) {
  const meta=await inspectMedia(ffmpeg,file,signal);
  if(isVideo&&(!meta.duration||meta.duration>600))throw new Error('视频需要可读取的时长，且不超过 10 分钟。请先截取片段。这是应用的抽帧上限。');
  const dir=path.join(tempRoot,randomUUID());await fs.mkdir(dir,{recursive:true});
  try {
    const count=isVideo?Math.min(24,Math.max(1,Math.ceil(meta.duration/5))):1;
    const frames=[];
    for(let i=0;i<count;i++){
      const time=isVideo?meta.duration*(i+.5)/count:0;
      const dest=path.join(dir,i+'.jpg');
      const r=await run(ffmpeg,['-hide_banner','-loglevel','error',...(isVideo?['-ss',String(time)]:[]),'-i',file,'-frames:v','1','-vf',"scale=w='min(1600,iw)':h='min(1600,ih)':force_original_aspect_ratio=decrease",'-q:v','3','-y',dest],{timeout:30000,signal});
      if(r.code!==0)throw new Error('无法解码所选媒体。');
      const data=await fs.readFile(dest);frames.push({time,data:data.toString('base64')});
    }
    if(frames.reduce((sum,f)=>sum+f.data.length,0)>18*1024*1024)throw new Error('图像请求过大，请裁剪截图或缩短视频。');
    return {frames,meta,isVideo};
  }finally{await fs.rm(dir,{recursive:true,force:true});}
}
