'use strict';
let bonusStatsPending=false,bonusStatsRequest=0,bonusStatsState=null,bonusStatsSignature='';
const bonusStamp=v=>({generation:v.Generation,epoch:v.Epoch,revision:v.Revision,actorId:v.ActorId});
function bonusStatsSend(v,cmd,attribute){
 if(bonusStatsPending)return;
 bonusStatsPending=true;document.querySelector('#bonusSubmit').disabled=true;document.querySelector('#bonusRefresh').disabled=true;
 document.querySelectorAll('#bonusChoices button').forEach(b=>b.disabled=true);
 try{bonusStatsRequest=send(cmd,{...bonusStamp(v),...(attribute==null?{}:{attribute})});}
 catch(e){bonusStatsPending=false;bonusStatsSignature='';document.querySelector('#bonusNotice').textContent='未送出，請重新取得能力點狀態。';}
}
function renderBonusStats(v){
 if(!v)return;const previous=bonusStatsState;bonusStatsState=v;
 if(!previous||previous.Generation!==v.Generation||previous.Epoch!==v.Epoch||previous.Revision!==v.Revision)bonusStatsPending=false;
 const signature=JSON.stringify(v)+bonusStatsPending;if(signature===bonusStatsSignature)return;bonusStatsSignature=signature;
 const choices=document.querySelector('#bonusChoices');choices.replaceChildren();
 for(const a of v.Attributes){const b=document.createElement('button');b.type='button';b.textContent=`${a.Selected?'已選擇：':'選擇：'}${a.Label} · 目前 ${a.Value??'未知'}`;b.disabled=!v.CanSelect||bonusStatsPending;b.onclick=()=>{if(!b.disabled)bonusStatsSend(v,'bonusSelect',a.Key);};choices.append(b);}
 const selected=v.Attributes.find(a=>a.Key===v.Selected);const submit=document.querySelector('#bonusSubmit');submit.textContent=selected?`確認將「${selected.Label}」提昇１點`:'請先選擇屬性';submit.disabled=!v.CanSubmit||bonusStatsPending;submit.onclick=()=>{if(!submit.disabled&&selected)bonusStatsSend(v,'bonusSubmit',selected.Key);};
 const refresh=document.querySelector('#bonusRefresh');refresh.disabled=!v.CanRefresh||bonusStatsPending;refresh.onclick=()=>{if(!refresh.disabled)bonusStatsSend(v,'bonusRefresh');};
 document.querySelector('#bonusNotice').textContent=v.Notice;document.querySelector('#bonusRemaining').textContent='剩餘點數：原服未提供';
}
function handleBonusStatsReply(reply){if(reply.id!==bonusStatsRequest)return;bonusStatsPending=false;bonusStatsSignature='';renderBonusStats(bonusStatsState);}
