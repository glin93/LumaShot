export const syncFilters=['--filter','- *.tmp','--filter','+ /objects/**','--filter','+ /revisions/**','--filter','+ /settings/**','--filter','+ /[a-f0-9]*','--filter','- **'];
// rclone authorize emits either a JSON token or a base64 encoded config map.
export function parseDriveAuthorization(output){
 for(const line of output.split(/\r?\n/)){
  const s=line.trim();if(!s||s.length>1024*1024)continue;
  for(const candidate of [s, /^[A-Za-z0-9_+/=-]+$/.test(s)?Buffer.from(s,'base64url').toString('utf8'):'']){
   try{const value=JSON.parse(candidate);const token=value.token?(typeof value.token==='string'?JSON.parse(value.token):value.token):value;
    if(typeof token.access_token==='string'&&typeof token.refresh_token==='string'&&token.refresh_token)return token;
   }catch{}
  }
 }
 throw new Error('未收到可持续使用的 Google 授权，请重新连接。');
}
