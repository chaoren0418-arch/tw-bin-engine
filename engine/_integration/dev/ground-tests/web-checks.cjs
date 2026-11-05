const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const path=require('node:path');const repo=path.resolve(process.argv[2]);
const elements=new Map(),messages=[];let checks=0;
function element(){return {dataset:{},children:[],value:'1',textContent:'',append(...x){this.children.push(...x);},replaceChildren(...x){this.children=x;},addEventListener(){},focus(){}};}
function get(id){if(!elements.has(id))elements.set(id,element());return elements.get(id);}
const context=vm.createContext({document:{querySelector:get,getElementById:id=>get('#'+id),createElement:element,addEventListener(){}},window:{addEventListener(){}},chrome:{webview:{postMessage:m=>messages.push(m),addEventListener(){}}}});
vm.runInContext(fs.readFileSync(path.join(repo,'engine/web/panel.js'),'utf8'),context);
function evaluate(code){return vm.runInContext(code,context);}
function check(ok){checks++;assert.ok(ok);}
for(const key of ['inventory','ground']){
 const cmd=key==='ground'?'pickup':'drop',input=key==='ground'?'#pickupCount':'#dropCount';
 const s={epoch:7,selectedId:900101,selectionRevision:3,revision:9,items:[{id:900101,count:250,droppable:true,selected:true}]};
 context.s=s;evaluate(`rememberQuantity('${key}',s)`);
 for(const text of ['0','-1','1.0','1.5','251','2147483648','4294967296','999999999999999999999999999','',' ','+1','1e2',' 1','１']){
  get(input).value=text;const n=messages.length;evaluate(`countCommand('${cmd}','${key}','${input}')`);check(messages.length===n);
  const notice=get(key==='ground'?'#groundNotice':'#inventoryNotice').textContent;check(notice.includes('數量不合法')&&!notice.includes('重新選取'));
 }
 for(const text of ['1','250']){
  get(input).value=text;evaluate(`countCommand('${cmd}','${key}','${input}')`);const m=messages.at(-1);
  check(m.cmd===cmd&&m.count===text&&m.available===250&&m.revision===9&&m.selectionRevision===3&&m.itemId===900101&&m.epoch===7);
 }
 // A background update must not silently rebind the quantity to a fresh snapshot.
 s.items[0].count=249;s.revision=10;evaluate(`rememberQuantity('${key}',s)`);get(input).value='1';evaluate(`countCommand('${cmd}','${key}','${input}')`);
 check(messages.at(-1).revision===9&&messages.at(-1).available===250);
 s.selectionRevision++;evaluate(`rememberQuantity('${key}',s)`);evaluate(`countCommand('${cmd}','${key}','${input}')`);check(messages.at(-1).revision===10&&messages.at(-1).available===249);
 s.selectedId=null;evaluate(`rememberQuantity('${key}',s)`);const n=messages.length;evaluate(`countCommand('${cmd}','${key}','${input}')`);check(messages.length===n);
 check(get(key==='ground'?'#groundNotice':'#inventoryNotice').textContent==='未送出：請先選取物品');
}
// UI message refresh and inventory content are independent revisions.
context.s={epoch:8,selectedId:900101,selectionRevision:1,revision:20,quantityRevision:12,items:[{id:900101,count:250,droppable:true}]};
evaluate("rememberQuantity('inventory',s)");get('#dropCount').value='1';evaluate("countCommand('drop','inventory','#dropCount')");check(messages.at(-1).revision===12);
context.s.revision++;context.s.message='你不能夠放棄此樣物品。';evaluate("rememberQuantity('inventory',s)");
get('#dropCount').value='10';evaluate("countCommand('drop','inventory','#dropCount')");check(messages.at(-1).revision===12&&messages.at(-1).count==='10');
context.s.quantityRevision++;context.s.items[0].count=249;evaluate("rememberQuantity('inventory',s)");evaluate("countCommand('drop','inventory','#dropCount')");check(messages.at(-1).revision===12&&messages.at(-1).available===250);
context.s.selectionRevision++;evaluate("rememberQuantity('inventory',s)");evaluate("countCommand('drop','inventory','#dropCount')");check(messages.at(-1).revision===13&&messages.at(-1).available===249);
const result={status:'pass',checks,scope:'實際 panel.js＋明示 DOM 替身；無瀏覽器／遊戲／網路；宿主拒絕舊快照由 GroundChecks 驗證'};
fs.writeFileSync(process.argv[3],JSON.stringify(result,null,2)+'\n');console.log(`PASS ${checks} Web checks`);
