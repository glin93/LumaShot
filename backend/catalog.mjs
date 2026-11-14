// Reuse DeepSeek Harness's pi-ai catalog source; never guess image support from model names.
import {getBuiltinModels} from '@earendil-works/pi-ai/providers/all';
export function eligibleModels(provider,available){
 const list=getBuiltinModels(provider==='gemini'?'google':provider);
 const allowed=new Set(available);
 const result=list.filter(m=>m.input?.includes('image')&&allowed.has(m.id)&&
   (provider==='gemini'?m.api==='google-generative-ai':m.api==='openai-responses'))
   .map(m=>({id:m.id,name:m.name||m.id,contextWindow:m.contextWindow,maxOutputTokens:m.maxTokens}));
 if(!result.length)throw new Error('此账号没有目录中已验证的图像分析模型。请检查权限或更新模型目录。');
 return result;
}
