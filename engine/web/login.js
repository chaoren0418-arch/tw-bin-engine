'use strict';
let loginPending=null,loginCharactersSignature='';
const loginElement=id=>document.getElementById(id);
const loginStateNames={Offline:'離線預覽',Connecting:'連線中',AwaitSeed:'等待連線交握',Authenticating:'驗證帳密中',AwaitList:'等待角色清單',ReadingCharacters:'接收角色清單',CharacterList:'請選擇角色',EnteringWorld:'進入世界中',InWorld:'已進入世界',Rejected:'登入遭拒絕',Disconnected:'連線已中斷',Cancelled:'已取消／斷線',Faulted:'連線發生錯誤'};
function loginNotice(text){loginElement('webLoginNotice').textContent=text;}
function loginEnabled(){
 const c=window.lastState?.loginControl,waiting=loginPending!==null;
 for(const id of ['webLoginProfile','webLoginPort','webLoginAccount','webLoginPassword','webLoginSubmit'])loginElement(id).disabled=!c?.canLogin||waiting;
 loginElement('webLoginCharacters').disabled=!c?.canEnter||waiting;
 loginElement('webEnterWorld').disabled=!c?.canEnter||waiting||!loginElement('webLoginCharacters').value;
 const stopping=loginPending?.command==='cancelLogin'||loginPending?.command==='offlinePreview';
 loginElement('webCancelLogin').disabled=!c?.canCancel||stopping;
 loginElement('webOfflinePreview').disabled=!c||stopping;
}
function renderLogin(login,control){
 if(!login||!control)return;
 const normal=window.lastState?.normalProduct===true;loginElement("connectionOptions").hidden=normal;
 if(normal){loginElement("webLoginProfile").value="p24-real-jar-d287-client2";loginElement("webLoginPort").replaceChildren(new Option("42061","42061"));loginElement("webLoginPort").value="42061";}
 const signature=JSON.stringify([login.state,login.characters.map(c=>c.Name)]);
 if(signature!==loginCharactersSignature){
  loginCharactersSignature=signature;const select=loginElement('webLoginCharacters');select.replaceChildren(new Option(login.characters.length?'請選擇角色':'尚無角色清單',''));
  for(const c of login.characters)select.add(new Option(`${c.Name} · Lv.${c.Level}`,c.Name));
 }
 if(!control.canLogin)loginElement('webLoginPassword').value='';
 loginNotice(`${loginStateNames[login.state]||'連線狀態更新'}${normal?'':' · '+login.detail}\n${control.notice}`);loginEnabled();
}
function handleLoginReply(reply){
 if(reply.id!==loginPending?.id)return;
 loginPending=null;loginNotice(reply.status);loginEnabled();
}
function loginCommand(command,fields={}){
 const revision=window.lastState?.loginControl?.sessionRevision,generation=window.lastState?.loginControl?.generation;
 const interrupt=command==='cancelLogin'||command==='offlinePreview';
 if((loginPending!==null&&!interrupt)||!Number.isInteger(revision)||!Number.isInteger(generation)){loginNotice('客戶端尚未就緒，請稍候。');return;}
 try{loginPending={id:send(command,{generation,revision,...fields}),command};loginEnabled();}
 catch{loginPending=null;loginNotice('操作尚未送出，請重新開啟本機面板後重試。');loginEnabled();}
}
loginElement('webLoginProfile').addEventListener('change',()=>{
 const real=loginElement('webLoginProfile').value==='p24-real-jar-d287-client2';
 loginElement('webLoginPort').replaceChildren(...(real?[42061]:[42062,42063]).map(p=>new Option(String(p),String(p))));
});
loginElement('loginForm').addEventListener('submit',event=>{
 event.preventDefault();const password=loginElement('webLoginPassword').value;loginElement('webLoginPassword').value='';
 if(!window.lastState?.loginControl?.canLogin){loginNotice('目前已有連線或登入作業，請先取消／斷線。');return;}
 loginCommand('login',{profile:loginElement('webLoginProfile').value,port:Number(loginElement('webLoginPort').value),account:loginElement('webLoginAccount').value,password});
});
loginElement('webLoginCharacters').addEventListener('change',loginEnabled);
loginElement('webEnterWorld').addEventListener('click',()=>{const character=loginElement('webLoginCharacters').value;if(character)loginCommand('enterWorld',{character});});
loginElement('webCancelLogin').addEventListener('click',()=>{loginElement('webLoginPassword').value='';loginCommand('cancelLogin');});
loginElement('webOfflinePreview').addEventListener('click',()=>{loginElement('webLoginPassword').value='';loginCommand('offlinePreview');});
for(const id of ['webLoginAccount','webLoginPassword','webLoginProfile','webLoginPort','webLoginCharacters'])loginElement(id).addEventListener('focus',()=>send('stop'));
window.addEventListener('pagehide',()=>{loginElement('webLoginPassword').value='';});
