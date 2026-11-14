'use strict';
// Write text only when it changes: an equal textContent write still replaces the text node, and the
// accessibility node rebuilt with it can be read back as Name=null by UIA clients (#1173 G06, #1178).
function textChanged(current,value){return current!==value;}
function setText(id,value){const el=document.getElementById(id);if(textChanged(el.textContent,value))el.textContent=value;}
let serial=0;const send=(cmd,extra={})=>{const id=++serial;chrome.webview.postMessage({v:1,id,cmd,...extra});return id;};
for(let d=0;d<8;d++){const b=document.createElement('button');b.textContent=`D${d}`;b.id=`d${d}`;b.onclick=()=>send('face',{direction:d});document.querySelector('#directions').append(b);}
document.querySelector('#game').onclick=()=>send('focusGame');
document.addEventListener('keydown',e=>{if(e.key==='F10'){e.preventDefault();send('focusGame');}else if(e.key==='F11'){e.preventDefault();document.querySelector('#notes').focus();}});
chrome.webview.addEventListener('message',e=>{const s=e.data;if(!s||s.v!==1)return;if(s.kind==='state'){
 window.lastState=s;
 renderBonusStats(s.bonusStats);renderPrivateShop(s.privateShop);renderLogin(s.login,s.loginControl);renderInteraction(s.interaction);renderTrade(s.trade);
 renderMagic(s.magic);renderInventory(s.inventory);renderGround(s.ground);
 renderPresence(s.presence);renderAudioSettings(s.audio);
 const w=s.login?.world;
 const reward=w?.rewards, xp=reward?.experience, change=reward?.lastExperienceChange;
 document.querySelector('#experienceResult').textContent=xp?`Lv.${xp.Level} · 總 EXP ${xp.Total??'未知（低32位 '+xp.WireLow32+'）'}
${xp.Percent==null?xp.Limitation:'本級 '+xp.Percent.toFixed(6)+'% · 下一級 '+xp.NextLevel}${change?'\n原回包變化 '+(change.Delta==null?'未知':(change.Delta>=0?'+':'')+change.Delta):''}`:'等待原服 EXP';
 document.querySelector('#rewardChanges').textContent=(reward?.inventoryChanges||[]).slice(-8).map(i=>`${s.inventory?.items.find(v=>v.id===i.ObjectId)?.name??i.Name} · ${i.Before} → ${i.Count} (${i.Delta>0?'+':''}${i.Delta})`).join('\n')||'本次尚無變化';
 document.querySelector("#attack").disabled=!s.onlineMode||!w?.renderable||!w?.target||w.target.Dead||w.combat?.ownDead;
 document.querySelector("#combatResult").textContent=w?.combat?.notice||"等待原服回報";
 const target=w?.target;document.querySelector("#targetResult").textContent=target?`${s.targetName||target.Name} / ${target.Name}\nobject ${target.ObjectId} · gfx ${target.Gfx}\n${target.X}, ${target.Y} · D${target.Direction}\nHP ${target.HpPercent==null?"未提供":target.HpPercent+"%"} · 類型未確定\n旗標 ${target.Flags} / 原始等級欄 ${target.Level}`:"未選取";
 document.querySelector("#assetLabel").textContent=s.onlineMode?(w?.ready?`伺服器 GFX ${w.actor.Gfx}`:"等待伺服器外觀"):"離線素材 GFX 0 → SPRITE 3225";
 document.querySelector('#worldResult').textContent=w?.ready?`${w.actor.Name} / object ${w.actor.ObjectId}
${w.movement?.authoritativePosition?.displayText||w.movement?.authoritativePosition?.text||"原服座標：等待位置回包"}
畫面預測 map ${w.mapId} · ${w.actor.X}, ${w.actor.Y} · D${w.actor.Direction}
已載入範圍 ${w.loadedRegion?.minX}–${w.loadedRegion?.maxX} / ${w.loadedRegion?.minY}–${w.loadedRegion?.maxY}
gfx ${w.actor.Gfx} · 武器型態 ${w.actor.Weapon} / ${w.appearanceNotice??(w.renderable?'素材匹配':'素材未支援')}
Lv.${w.status.Level} HP ${w.status.Hp}/${w.status.MaxHp} MP ${w.status.Mp}/${w.status.MaxMp}
${w.sustenance??"等待原服飽食／負重"}
${w.magic?.acNotice??("AC "+w.status.Ac)} EXP ${w.rewards?.experience?.Total??"未知"} 正義 ${w.status.Lawful}
力 ${w.status.Strength} 敏 ${w.status.Dexterity} 體 ${w.status.Constitution} 智 ${w.status.Intelligence} 精 ${w.status.Wisdom} 魅 ${w.status.Charisma}
keepalive ${w.keepalivesSent} / 時間回包 ${w.timeReplies}
${w.movement.notice}\n預測步數 ${w.movement.predictedSteps} / 校正 ${w.movement.corrections}`:'尚未收到本次完整世界資料';
 if(s.login){const l=s.login;document.querySelector('#loginResult').textContent=`${l.profile}
${l.state} · ${l.detail}
${l.news||''}
`+l.characters.map(c=>`${c.Name} / ${c.Clan}
Lv.${c.Level}  HP ${c.Hp}  MP ${c.Mp}  AC ${c.Ac}
職業 ${c.Class} / 性別 ${c.Sex} / 正義 ${c.Lawful}
力 ${c.Strength} 敏 ${c.Dexterity} 體 ${c.Constitution} 智 ${c.Intelligence} 精 ${c.Wisdom} 魅 ${c.Charisma}`).join('\n\n');}document.querySelector('#position').textContent=s.onlineMode?(w?.movement?.authoritativePosition?.displayText||w?.movement?.authoritativePosition?.text||'原服座標：等待位置回包'):`${s.x}, ${s.y}`;document.querySelector('#direction').textContent=s.onlineMode?(w?.ready?`方向 D${w.actor.Direction} · 預測／伺服器校正`:'等待真實角色資料'):`方向 D${s.direction} · ${s.walking?'行走':'站立'} · 步數 ${s.steps}`;
 document.querySelector('#mode').textContent=s.onlineMode?'真實連線 · '+s.login.state:s.networkMode?'測試通訊 · 回覆後套用':'離線模式';document.querySelector('#mapResource').textContent=s.onlineMode?[w?.basicSupplyNotice,w?.announcement,w?.bonusStatsNotice,w?.ship?.notice,w?.movement?.speedNotice,w?.teleport?.notice,w?.mapResourceNotice].filter(Boolean).join('\n'):'';document.querySelector('#connection').textContent=`${s.onlineMode?s.login.state:s.connection} / ${s.busy?'等待有效回覆':s.gameFocus?'遊戲控制':'面板／其他焦點'}`;
 document.querySelector('#revision').textContent=`狀態版本 ${s.revision} · 已確認 ${s.confirmed}`;document.querySelectorAll('.directions button').forEach(b=>b.disabled=(s.onlineMode&&(!s.movementControl?.available||s.movementControl.busy||!w?.renderable||w?.teleport?.actionLocked||w?.combat?.ownDead||w?.status?.Hp===0))||s.busy||s.walking||(s.networkMode&&s.connection!=='Ready'));
 }else if(s.kind==='reply'){if(s.audio&&!handleAudioSettingsReply(s))return;window.lastReply=s;handleBonusStatsReply(s);handlePrivateShopReply(s);handleLoginReply(s);handleNpcReply(s);handleTradeReply(s);document.querySelector('#reply').textContent=`#${s.id} ${s.status}`;}});
send('ready');

for(let d=0;d<8;d++){const b=document.createElement('button');b.textContent=`走 D${d}`;b.id=`m${d}`;b.onclick=()=>send('move',{direction:d});document.querySelector('#moves').append(b);}
document.querySelector('#stop').onclick=()=>send('stop');
document.querySelector('#notes').addEventListener('focus',()=>send('stop'));
window.addEventListener('blur',()=>send('stop'));

document.querySelector("#attack").onclick=()=>send("attack");

const quantityErrors={};
let inventorySignature='';
function renderInventory(inv){
 rememberQuantity('inventory',inv);
 if(!inv)return;const signature=JSON.stringify(inv);if(signature===inventorySignature)return;inventorySignature=signature;
 const host=document.querySelector('#inventoryItems');host.replaceChildren();
 for(const i of inv.items){const b=document.createElement('button');b.className='inventoryItem'+(i.selected?' selected':'');b.dataset.itemId=i.id;
 if(i.icon){const img=document.createElement('img');img.src=i.icon;img.alt='原道具圖示';b.append(img);}else{const span=document.createElement('span');span.textContent='缺圖';b.append(span);}
 const text=document.createElement('span');text.textContent=`${i.name} × ${i.count}\n${i.state}`;b.append(text);b.onclick=()=>send('selectItem',{itemId:i.id,epoch:inv.epoch});host.append(b);}
 const selected=inv.items.find(i=>i.id===inv.selectedId);document.querySelector('#useItem').textContent=selected?.action||'單次使用';document.querySelector('#dropItem').disabled=!selected?.droppable||inv.busy;document.querySelector('#dropCount').max=selected?.count||1;document.querySelector('#useItem').disabled=!selected?.usable||inv.busy||inv.available===false;document.querySelector('#inventoryNotice').textContent=quantityErrors.inventory||inv.notice+(inv.message?'\n'+inv.message:'');
}
document.querySelector('#useItem').onclick=()=>{const inv=window.lastState?.inventory;if(inv?.selectedId)send('useItem',{itemId:inv.selectedId,epoch:inv.epoch,selectionRevision:inv.selectionRevision});};

let groundSignature='';
function renderGround(g){if(!g)return;const sig=JSON.stringify(g);if(sig===groundSignature)return;groundSignature=sig;
 rememberQuantity('ground',g);
 const host=document.querySelector('#groundItems');host.replaceChildren();
 for(const i of g.items){const b=document.createElement('button');b.className='inventoryItem'+(i.selected?' selected':'');b.dataset.groundId=i.id;if(i.image){const img=document.createElement('img');img.src=i.image;img.alt='原地面外觀';b.append(img);}const t=document.createElement('span');t.textContent=i.name;b.append(t);b.onclick=()=>send('selectGround',{itemId:i.id,epoch:g.epoch});host.append(b);}
 const selected=g.items.find(i=>i.id===g.selectedId);document.querySelector('#pickup').disabled=!selected||g.busy;document.querySelector('#pickupCount').max=selected?.count||1;document.querySelector('#groundNotice').textContent=quantityErrors.ground||g.notice;
}
const quantitySelections={};
function rememberQuantity(key,s){
 if(!s)return;const old=quantitySelections[key],item=s.items.find(i=>i.id===s.selectedId);
 if(!old||old.epoch!==s.epoch||old.itemId!==s.selectedId||old.selectionRevision!==s.selectionRevision)
  {delete quantityErrors[key];quantitySelections[key]=item?{itemId:s.selectedId,epoch:s.epoch,selectionRevision:s.selectionRevision,revision:s.quantityRevision??s.revision,available:item.count}:null;}
}
function countCommand(cmd,key,input){
 const s=quantitySelections[key],text=document.querySelector(input).value;
 if(s&&/^[0-9]+$/.test(text)&&Number.isSafeInteger(Number(text))&&Number(text)>0&&Number(text)<=Math.min(s.available,2147483647)){delete quantityErrors[key];document.querySelector(key==='ground'?'#groundNotice':'#inventoryNotice').textContent='數量請求已提交，等待處理';inventorySignature=groundSignature='';send(cmd,{...s,count:text});}
 else document.querySelector(key==='ground'?'#groundNotice':'#inventoryNotice').textContent=quantityErrors[key]=s?'未送出：數量不合法，請輸入 1 到持有量（最多 2147483647）的整數':'未送出：請先選取物品';
}
document.querySelector('#dropItem').onclick=()=>countCommand('drop','inventory','#dropCount');
document.querySelector('#pickup').onclick=()=>countCommand('pickup','ground','#pickupCount');
for(const id of ['dropCount','pickupCount'])document.getElementById(id).addEventListener('focus',()=>send('stop'));

let magicSignature='',magicSkillsSignature='',magicTargetsSignature='';
function renderMagic(m){if(!m)return;const sig=JSON.stringify(m);if(sig===magicSignature)return;magicSignature=sig;
const skills=document.getElementById('magicSkills');const sk=JSON.stringify(m.skills);if(sk!==magicSkillsSignature){magicSkillsSignature=sk;skills.replaceChildren(new Option('請選擇已學魔法',''));for(const x of m.skills){const op=new Option(`${x.Name} · ${x.Target} · 原模板MP${x.NominalMp}`,x.Id);op.disabled=!x.Supported;skills.add(op);}}skills.value=m.selectedId||'';
const targets=document.getElementById('magicTargets');const tk=JSON.stringify([m.epoch,m.targetMode,m.targets]);if(tk!==magicTargetsSignature){magicTargetsSignature=tk;targets.replaceChildren(new Option(m.targetMode==='attack'?'選擇可見敵人':'自己',m.targetMode==='attack'?'':'0'));for(const x of m.targets)targets.add(new Option(x.name+' #'+x.id,x.id));}targets.disabled=m.targetMode==='none';targets.value=m.targetId==null?'':String(m.targetId);
setText('magicVitals',m.hp==null?'等待原服 HP／MP':`HP ${m.hp}/${m.maxHp} · MP ${m.mp}/${m.maxMp}`);document.getElementById('castMagic').disabled=!m.available;setText('magicNotice',(m.targetName?'對象：'+m.targetName+'\n':'')+(m.acNotice||m.ac!=null?(m.acNotice||'AC '+m.ac)+' · '+([m.fire,m.water,m.wind,m.earth].every(v=>v!=null)?'抗性 火'+m.fire+' 水'+m.water+' 風'+m.wind+' 地'+m.earth:'抗性：等待原服資料')+'\n':'')+(m.buffs?m.buffs+'\n':'')+m.notice+'\n'+(m.message||''));}
document.getElementById('magicSkills').onchange=e=>{const m=window.lastState?.magic;if(m&&e.target.value)send('selectMagic',{skillId:Number(e.target.value),epoch:m.epoch});};
document.getElementById('magicTargets').onchange=e=>{const m=window.lastState?.magic;if(m&&e.target.value)send('selectMagicTarget',{targetId:Number(e.target.value),epoch:m.epoch});};
document.getElementById('castMagic').onclick=()=>{const m=window.lastState?.magic;if(m?.selectedId)send('castMagic',{skillId:m.selectedId,epoch:m.epoch});};

chrome.webview.addEventListener('message',async e=>{
 const s=e.data;if(!s||s.v!==1||s.kind!=='scene')return;
 const img=document.getElementById('sceneImage');window.lastScene=s;window.renderedScene=null;
 if(!s.available){img.removeAttribute('src');setText('sceneStatus','尚未進入世界');return;}
 img.src=s.png;
 try{
   await img.decode();
   const bytes=Uint8Array.from(atob(s.png.split(',')[1]),c=>c.charCodeAt(0));
   const actualHash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),b=>b.toString(16).padStart(2,'0')).join('').toUpperCase();
   if(window.lastScene!==s||img.src!==s.png)return;
   if(actualHash!==s.sha256)throw new Error('scene source hash mismatch');
   img.dataset.sequence=String(s.sequence);img.dataset.sha256=actualHash;img.dataset.revision=String(s.opacityRevision);
   window.renderedScene={sequence:s.sequence,epoch:s.epoch,sha256:actualHash,opacityRevision:s.opacityRevision,actor:s.actor};
   setText('sceneStatus',`${s.actor.X}, ${s.actor.Y} · ${s.actor.Name}`);
 }catch(error){if(window.lastScene===s)window.sceneDecodeError=String(error);}
});

function renderPresence(info){
 setText('incomingAttack',info?.IncomingAttack||'');
 setText('missingObjects',(info?.Labels||[]).filter(x=>x.Missing).map(x=>`${x.Name} #${x.Id} · ${x.X}, ${x.Y}\n${x.Detail}`).join('\n')||'');
}

// Both controls submit one complete settings value; host state is authoritative.
let audioRendering=false,audioEditing=false,audioPending=null,audioLatest=null;
function renderAudioSettings(state){
 if(!state)return;
 audioLatest=state;
 if(!state.editable){audioEditing=false;audioPending=null;}
 audioRendering=true;
 try{
  if(!audioEditing&&!audioPending){
   document.querySelector('#audioGain').value=String(state.gain*100);
   document.querySelector('#audioGainValue').textContent=`${Math.round(state.gain*10000)/100}%`;
  }
  if(!audioPending)document.querySelector('#audioMuted').checked=state.muted;
  document.querySelector('#audioGain').disabled=!state.editable;
  document.querySelector('#audioMuted').disabled=!state.editable;
  document.querySelector('#audioNotice').textContent=audioEditing||audioPending?'設定尚未套用':state.applied?(state.muted?'已套用靜音':'音量已套用'):state.status==='pending'?'正在套用聲音設定…':state.status==='failed'?'聲音設定未套用':state.status==='closed'?'聲音設定暫不可用':'聲音尚未就緒';
 }finally{audioRendering=false;}
}
function sendAudioSettings(){
 if(audioRendering||document.querySelector('#audioGain').disabled||document.querySelector('#audioMuted').disabled)return;
 const gain=Number(document.querySelector('#audioGain').value)/100;
 if(!Number.isFinite(gain)||gain<0||gain>1)return;
 const muted=document.querySelector('#audioMuted').checked;
 audioEditing=false;
 if(audioPending&&audioPending.gain===gain&&audioPending.muted===muted)return;
 audioPending={gain,muted,id:null};
 document.querySelector('#audioGainValue').textContent=`${Math.round(gain*100)}%`;
 document.querySelector('#audioNotice').textContent='設定尚未套用';
 audioPending.id=send('audioSettings',{gain,muted});
}
function handleAudioSettingsReply(reply){
 if(!audioPending||reply.id!==audioPending.id||!reply.audio)return false;
 audioPending=null;
 // A newer external native state may already have arrived; acknowledgement is not a rollback.
 const acknowledged=reply.audio;
 const latest=audioLatest;
 const newer=latest&&(!latest.editable||latest.revision>acknowledged.revision||
  (latest.revision===acknowledged.revision&&latest.configurationRevision>acknowledged.configurationRevision));
 renderAudioSettings(newer?latest:acknowledged);
 return true;
}
document.querySelector('#audioGain').oninput=()=>{
 if(audioRendering||document.querySelector('#audioGain').disabled)return;
 audioEditing=true;
 document.querySelector('#audioGainValue').textContent=`${document.querySelector('#audioGain').value}%`;
 document.querySelector('#audioNotice').textContent='設定尚未套用';
};
document.querySelector('#audioGain').onchange=sendAudioSettings;
document.querySelector('#audioMuted').onchange=sendAudioSettings;
document.querySelector('#audioGain').onblur=()=>{if(audioEditing)sendAudioSettings();};
document.querySelector('#audioGain').onkeydown=e=>{
 if(e.key==='Enter'&&audioEditing){e.preventDefault();sendAudioSettings();}
 else if(e.key==='Escape'&&audioEditing){e.preventDefault();audioEditing=false;if(audioLatest)renderAudioSettings(audioLatest);}
};
