// Test-only fetch adapter. It never forwards requests to the network.
globalThis.fetch=async(input,options={})=>{
 const url=String(input.url||input);const headers=new Headers(options.headers||input.headers);
 if(url.includes('/models')&&!url.includes(':generateContent')){
  if(url.startsWith('https://api.openai.com/'))return Response.json({object:'list',data:[{id:'gpt-4.1',object:'model'},{id:'text-embedding-3-large',object:'model'}]});
  if(url.startsWith('https://generativelanguage.googleapis.com/'))return Response.json({models:[{name:'models/gemini-2.5-flash',displayName:'Gemini 2.5 Flash',supportedGenerationMethods:['generateContent'],inputTokenLimit:1048576,outputTokenLimit:65536}]});
 }
 if(url==='https://api.openai.com/v1/responses'){
  const request=JSON.parse(options.body);if(request.store!==false||request.stream!==true)throw new Error('Privacy/stream contract failed');
  if(!request.input.some(m=>Array.isArray(m.content)&&m.content.some(c=>c.type==='input_image')))throw new Error('Missing image');
  const incomplete=headers.get('authorization')?.includes('incomplete');
  const events=[{type:'response.output_text.delta',delta:'这是测试画面的总结。'},incomplete?{type:'response.incomplete',response:{status:'incomplete'}}:{type:'response.completed',response:{status:'completed'}}];
  return new Response(events.map(e=>'event: '+e.type+'\ndata: '+JSON.stringify(e)+'\n\n').join(''),{headers:{'content-type':'text/event-stream'}});
 }
 if(url.startsWith('https://generativelanguage.googleapis.com/')&&url.includes(':generateContent'))return Response.json({candidates:[{finishReason:'STOP',content:{parts:[{text:'Gemini 测试回答。'}]}}]});
 throw new Error('Unexpected network request in test: '+new URL(url).origin);
};
