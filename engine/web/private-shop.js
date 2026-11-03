'use strict';
let privateShopPending=false,privateShopRequest=0,privateShopState=null,privateShopSignature='',privateShopQuantityKey='';
const privateStamp=v=>({generation:v.Generation,epoch:v.Epoch,revision:v.Revision,targetRevision:v.TargetRevision,actorId:v.ActorId,ownerId:v.OwnerId});
function privateShopSend(v,cmd,extra={}){
 if(privateShopPending)return;privateShopPending=true;
 document.querySelectorAll('#privateOwners button,#privateRows button,#privateBuy,#privateSell,#privateClose,#privateSubmit').forEach(b=>b.disabled=true);
 try{privateShopRequest=send(cmd,{...privateStamp(v),...extra});}catch(e){privateShopPending=false;privateShopSignature='';document.querySelector('#privateNotice').textContent='未送出；請重新取得原服狀態。';}
}
function updatePrivateShopQuantity(){
 const v=privateShopState?.view,row=v?.Rows.find(r=>r.Id===v.SelectedRow),input=document.querySelector('#privateQuantity'),count=Number(input.value);
 const valid=!!row&&Number.isSafeInteger(count)&&count>0&&count<=row.Maximum;
 document.querySelector('#privateSubmit').disabled=!v?.CanSubmit||privateShopPending||!valid;
 document.querySelector('#privateTotal').textContent=row?`數量 ${Number.isSafeInteger(count)&&count>=0?count:'無效'} · 合計 ${valid?BigInt(row.Price)*BigInt(count):'—'}\n貨幣種類：原服未提供`:'選一筆商品，再輸入數量。';
}
function renderPrivateShop(state){
 if(!state?.view)return;const v=state.view,previous=privateShopState?.view;privateShopState=state;
 if(!previous||previous.Generation!==v.Generation||previous.Epoch!==v.Epoch||previous.Revision!==v.Revision||previous.TargetRevision!==v.TargetRevision)privateShopPending=false;
 const signature=JSON.stringify(state)+privateShopPending;if(signature===privateShopSignature)return;privateShopSignature=signature;
 const owners=document.querySelector('#privateOwners');owners.replaceChildren();
 for(const owner of state.owners){const button=document.createElement('button');button.type='button';button.textContent=`選取 ${owner.name} (${owner.x}, ${owner.y})`;button.disabled=privateShopPending||v.ActorId===0;button.onclick=()=>{if(!button.disabled)privateShopSend(v,'privateOwner',{ownerId:owner.id});};owners.append(button);}
 document.querySelector('#privateOwner').textContent='店主：'+v.OwnerName;
 const host=document.querySelector('#privateRows');host.replaceChildren();
 for(const row of v.Rows){const button=document.createElement('button');button.type='button';button.textContent=`${v.SelectedRow===row.Id?'已選：':'選擇：'}${row.Name}\n單價 ${row.Price} · 清單數量 ${row.ListedCount} · 本次最多 ${row.Maximum}`;button.disabled=!v.CanChoose||privateShopPending||row.Maximum===0;button.onclick=()=>{if(!button.disabled)privateShopSend(v,'privateChoose',{row:row.Id});};host.append(button);}
 for(const [id,sell] of [['privateBuy',false],['privateSell',true]]){const b=document.querySelector('#'+id);b.disabled=!(sell?v.CanQuerySell:v.CanQueryBuy)||privateShopPending;b.onclick=()=>{if(!b.disabled)privateShopSend(v,'privateQuery',{sell});};}
 const close=document.querySelector('#privateClose');close.disabled=privateShopPending||!v.ActorId||!v.OwnerId;close.onclick=()=>{if(!close.disabled)privateShopSend(v,'privateClose');};
 const quantity=document.querySelector('#privateQuantity'),row=v.Rows.find(r=>r.Id===v.SelectedRow),key=`${v.Generation}/${v.Epoch}/${v.OwnerId}/${v.Sell}/${v.SelectedRow}`;
 if(key!==privateShopQuantityKey||!row){quantity.value='0';privateShopQuantityKey=key;}quantity.max=String(row?.Maximum??0);quantity.disabled=!v.CanSubmit||privateShopPending;quantity.oninput=updatePrivateShopQuantity;
 const submit=document.querySelector('#privateSubmit');submit.onclick=()=>{updatePrivateShopQuantity();if(!submit.disabled&&row)privateShopSend(v,'privateSubmit',{row:row.Id,count:Number(quantity.value),sell:v.Sell});};
 document.querySelector('#privateNotice').textContent=v.Notice+(v.ServerText?'\n原服訊息：'+v.ServerText:'');updatePrivateShopQuantity();
}
function handlePrivateShopReply(reply){if(reply.id!==privateShopRequest)return;privateShopPending=false;privateShopSignature='';renderPrivateShop(privateShopState);}
