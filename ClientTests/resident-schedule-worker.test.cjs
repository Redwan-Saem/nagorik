const {test}=require('node:test');const assert=require('node:assert/strict');const {readFileSync}=require('node:fs');const {runInNewContext}=require('node:vm');
function setup(){const handlers={},saved=new Map();let requests=async()=>({ok:true,status:200,clone(){return this;}});let activated=false,pushed;
    const cache={addAll:async paths=>{paths.forEach(path=>saved.set(path,{path}));},match:async path=>saved.get(path),put:async(path,value)=>saved.set(path,value)};
    const self={location:{origin:'https://nagorik.invalid'},addEventListener:(name,fn)=>handlers[name]=fn,skipWaiting:async()=>{activated=true;},clients:{claim:async()=>{}},registration:{showNotification:async(title,options)=>{pushed={title,options};}}};
    runInNewContext(readFileSync('wwwroot/sw.js','utf8'),{self,caches:{open:async()=>cache},fetch:(...args)=>requests(...args),URL});
    function fetchEvent(path,mode='navigate'){let response;const pending=[];handlers.fetch({request:{url:'https://nagorik.invalid'+path,method:'GET',mode},respondWith:p=>response=p,waitUntil:p=>pending.push(p)});return{response,pending};}
    return{handlers,saved,fetchEvent,setFetch:fn=>requests=fn,activated:()=>activated,pushed:()=>pushed};}
test('offline worker installs only generic public shell/assets and activates the update',async()=>{const s=setup();let ready;s.handlers.install({waitUntil:p=>ready=p});await ready;
    assert.equal(s.activated(),true);assert.equal(s.saved.has('/resident-loadshedding-offline.html'),true);
    assert.equal([...s.saved.keys()].some(path=>path.startsWith('/api/')||path==='/resident/loadshedding'),false);});
test('navigation authorization responses are never replaced with an offline page',async()=>{const s=setup();let ready;s.handlers.install({waitUntil:p=>ready=p});await ready;
    s.setFetch(async()=>({ok:false,status:401}));assert.equal((await s.fetchEvent('/resident/loadshedding').response).status,401);
    s.setFetch(async()=>{throw new Error('Offline');});assert.equal((await s.fetchEvent('/resident/loadshedding?zone=1').response).path,'/resident-loadshedding-offline.html');});
test('the worker never intercepts API/auth requests and preserves push notifications',async()=>{const s=setup();assert.equal(s.fetchEvent('/api/resident/loadshedding?zoneId=1','cors').response,undefined);
    assert.equal(s.fetchEvent('/login.html').response,undefined);let pending;s.handlers.push({data:{json:()=>({title:'Alert',body:'Body',url:'/map'})},waitUntil:p=>pending=p});await pending;
    assert.equal(s.pushed().title,'Alert');assert.equal(s.pushed().options.data.url,'/map');});
