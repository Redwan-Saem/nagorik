const { test } = require('node:test');const assert=require('node:assert/strict');const {readFileSync}=require('node:fs');const {runInNewContext}=require('node:vm');
const context={setTimeout,clearTimeout,AbortController};runInNewContext(readFileSync('wwwroot/js/resident-schedule-transport.js','utf8'),context);
const now=Date.parse('2026-10-24T06:00:00Z');
function plan(id=1){return {zoneId:id,zoneCode:'Z'+id,zoneName:'Zone '+id,timeZone:'Asia/Dhaka',today:'2026-10-24',fetchedAtUtc:new Date(now).toISOString(),lastUpdatedAtUtc:null,status:'NotPublished',nextOutage:null,
    days:Array.from({length:7},(_,i)=>({date:new Date(Date.parse('2026-10-24')+i*86400000).toISOString().slice(0,10),isToday:i===0,isPublished:false,publishedAtUtc:null,outages:[]}))};}
function setup(){const values=new Map();const storage={getItem:k=>values.get(k)||null,setItem:(k,v)=>values.set(k,v),removeItem:k=>values.delete(k)};
    let handler=async()=>({ok:true,status:200,json:async()=>plan()});const client=context.ResidentScheduleTransport.create({storage,now:()=>now,fetch:(...args)=>handler(...args)});
    return {client,values,storage,setHandler:value=>handler=value};}
const http=status=>async()=>({ok:false,status,json:async()=>({message:'HTTP '+status})});
test('successful data is cached per zone and offline keeps original timestamps',async()=>{const s=setup();const online=await s.client.read(1);assert.equal(online.offline,false);
    s.setHandler(async()=>{throw new TypeError('Network unavailable');});const offline=await s.client.read(1);assert.equal(offline.offline,true);assert.equal(offline.data.fetchedAtUtc,plan().fetchedAtUtc);
    await assert.rejects(s.client.read(2),/No usable cached/);});
test('401 and 403 do not use cached schedules and invalidate the relevant cache',async()=>{for(const code of [401,403]){const s=setup();await s.client.read(1);s.setHandler(http(code));
    await assert.rejects(s.client.read(1),error=>error.status===code);assert.equal(s.values.size,0);}});
test('400 and 404 are authoritative errors while server failure may use a recent cache',async()=>{const s=setup();await s.client.read(1);
    for(const code of [400,404]){s.setHandler(http(code));await assert.rejects(s.client.read(1),error=>error.status===code);}
    s.setHandler(http(503));assert.equal((await s.client.read(1)).offline,true);});
test('expired or corrupt cache and wrong-zone payloads are never displayed',async()=>{const s=setup();await s.client.read(1);const key=[...s.values.keys()][0];const old=JSON.parse(s.values.get(key));old.savedAt=now-8*86400000;s.values.set(key,JSON.stringify(old));
    s.setHandler(async()=>{throw new Error('Offline');});await assert.rejects(s.client.read(1),/No usable cached/);
    s.values.set(key,'broken json');await assert.rejects(s.client.read(1),/No usable cached/);
    s.setHandler(async()=>({ok:true,json:async()=>plan(2)}));await assert.rejects(s.client.read(1),/invalid/);});
test('zone catalog caches offline but authorization errors never fall back',async()=>{const s=setup();s.setHandler(async()=>({ok:true,json:async()=>[{id:1,code:'Z1',name:'Area'}]}));await s.client.zones();
    s.setHandler(async()=>{throw new Error('Offline');});assert.equal((await s.client.zones()).offline,true);
    s.setHandler(http(401));await assert.rejects(s.client.zones(),error=>error.status===401);assert.equal(s.values.size,0);});
test('storage failure cannot break an online read and unrelated fields are excluded',async()=>{const data=plan();data.authToken='must-not-cache';
    const s=setup();s.setHandler(async()=>({ok:true,json:async()=>data}));const result=await s.client.read(1);assert.equal(result.data.authToken,undefined);assert.doesNotMatch([...s.values.values()][0],/must-not-cache/);
    const client=context.ResidentScheduleTransport.create({now:()=>now,storage:{setItem(){throw new Error('Quota');},getItem(){throw new Error('Blocked');}},fetch:async()=>({ok:true,json:async()=>plan()})});
    assert.equal((await client.read(1)).offline,false);assert.equal(context.ResidentScheduleTransport.pollMilliseconds,30000);});
test('a late older response cannot overwrite a newer cached plan',async()=>{const s=setup();let resolveOld;
    s.setHandler(()=>new Promise(resolve=>{resolveOld=resolve;}));const oldRead=s.client.read(1);
    s.setHandler(async()=>({ok:true,json:async()=>plan()}));await s.client.read(1);
    const older=plan();older.fetchedAtUtc=new Date(now-60000).toISOString();resolveOld({ok:true,json:async()=>older});await oldRead;
    s.setHandler(async()=>{throw new Error('Offline');});assert.equal((await s.client.read(1)).data.fetchedAtUtc,plan().fetchedAtUtc);});
test('network timeouts are bounded and use the same honest cache fallback',async()=>{const s=setup();await s.client.read(1);
    const client=context.ResidentScheduleTransport.create({storage:s.storage,now:()=>now,timeoutMilliseconds:5,
        fetch:(url,options)=>new Promise((resolve,reject)=>options.signal.addEventListener('abort',()=>reject(new Error('Timed out'))))});
    assert.equal((await client.read(1)).offline,true);});
