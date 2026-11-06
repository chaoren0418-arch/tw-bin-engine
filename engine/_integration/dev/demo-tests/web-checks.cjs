'use strict';
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const repo=path.resolve(process.argv[2]),elements=new Map(),messages=[],listeners=[];let checks=0;
function element(){return {dataset:{},children:[],value:'',textContent:'',replacements:0,disabled:false,append(...x){this.children.push(...x);},add(x){this.children.push(x);},replaceChildren(...x){this.children=x;this.replacements++;},addEventListener(){},focus(){}};}
function get(id){if(!elements.has(id))elements.set(id,element());return elements.get(id);}
const context=vm.createContext({Option:function(text,value){this.text=text;this.value=value;},document:{querySelector:get,querySelectorAll:()=>[],getElementById:id=>get('#'+id),createElement:element,addEventListener(){}},window:{addEventListener(){}},chrome:{webview:{postMessage:m=>messages.push(m),addEventListener:(k,fn)=>listeners.push(fn)}}});
vm.runInContext(fs.readFileSync(path.join(repo,'engine/web/panel.js'),'utf8'),context);
const run=s=>vm.runInContext(s,context);function check(value,label){assert.ok(value,label);checks++;}
const inv={epoch:1,selectedId:900,selectionRevision:1,revision:1,quantityRevision:1,notice:'old',message:'你不能夠放棄此樣物品。',items:[{id:900,name:'肉',count:10,droppable:true,selected:true}]};
context.inv=inv;run('renderInventory(inv)');
for(const value of ['0','11']){
 get('#dropCount').value=value;const before=messages.length;get('#dropItem').onclick();
 check(messages.length===before,'invalid count never sent');
 check(get('#inventoryNotice').textContent.includes('數量不合法')&&!get('#inventoryNotice').textContent.includes('放棄'),'local error replaces old 125');
 inv.busy=!inv.busy;inv.revision++;run('renderInventory(inv)');
 check(get('#inventoryNotice').textContent.includes('數量不合法')&&!get('#inventoryNotice').textContent.includes('放棄'),'unrelated refresh cannot restore stale 125');
}
get('#dropCount').value='10';get('#dropItem').onclick();check(messages.at(-1).cmd==='drop'&&messages.at(-1).count==='10','valid quantity sent once');
check(get('#inventoryNotice').textContent==='數量請求已提交，等待處理','valid request clears displayed local error');
inv.selectionRevision++;run('renderInventory(inv)');check(!get('#inventoryNotice').textContent.includes('數量不合法'),'reselection ends local error');
const m={epoch:1,skills:[{Id:4,Name:'光箭',Target:'指定敵人',Supported:true,NominalMp:3}],selectedId:4,targetMode:'attack',targetId:456,targets:[{id:456,name:'牛'}],available:true,hp:100,mp:100,notice:'ready'};
context.m=m;run('window.lastState={magic:m};renderMagic(m)');
check(get('#magicTargets').children.length===2&&get('#magicTargets').value==='456','enemy dropdown and target id');
const old=get('#magicTargets').replacements,oldSkills=get('#magicSkills').replacements;
m.mp=99;m.notice='changed';run('renderMagic(m)');
check(get('#magicTargets').replacements===old&&get('#magicSkills').replacements===oldSkills,'vitals refresh preserves options');
get('#magicTargets').onchange({target:{value:'456'}});check(messages.at(-1).cmd==='selectMagicTarget'&&messages.at(-1).targetId===456,'dropdown sends explicit selected id');
get('#castMagic').onclick();check(messages.at(-1).cmd==='castMagic'&&messages.at(-1).skillId===4,'cast skill 4 command');
m.targets=[];m.targetId=null;m.available=false;run('renderMagic(m)');check(get('#magicTargets').children.length===1&&get('#castMagic').disabled,'removed target disables cast');
// Execute the real state message callback, with unrelated renderers stubbed only.
run("for(const name of ['renderBonusStats','renderPrivateShop','renderLogin','renderInteraction','renderTrade'])globalThis[name]=()=>{};");
const state={v:1,kind:'state',onlineMode:true,login:{state:'InWorld',characters:[],world:{ready:true,actor:{Name:'offline',ObjectId:123,X:999,Y:999},status:{},movement:{authoritativePosition:{text:'原服座標 map 0 / x 32600 / y 32940 · S87 #1 · 移動後尚無位置回包'}}}}};
listeners[0]({data:state});check(get('#position').textContent.includes('32600')&&!get('#position').textContent.includes('999'),'server coordinates independent from prediction');
check(get('#worldResult').textContent.includes('畫面預測')&&get('#worldResult').textContent.includes('尚無位置回包'),'world state labels prediction separately');
state.login.world.movement.authoritativePosition.displayText='目前位置 map 0 / x 32603 / y 32940（本地預測，非原服確認） · 原服座標 map 0 / x 32600 / y 32940';
listeners[0]({data:state});check(get('#position').textContent.includes('32603')&&get('#position').textContent.includes('32600')&&get('#position').textContent.includes('非原服確認'),'current prediction and last authority are both visible');
check(get('#worldResult').textContent.includes('32603')&&get('#worldResult').textContent.includes('32600'),'F9 details use same position display');
fs.writeFileSync(process.argv[3],JSON.stringify({status:'pass',checks,scope:'實際 panel.js；DOM 替身，非瀏覽器真機'},null,2)+'\n');console.log(`PASS ${checks} DEMO Web checks`);
