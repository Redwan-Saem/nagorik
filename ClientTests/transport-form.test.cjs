const {test}=require('node:test'), assert=require('node:assert/strict'), {readFileSync}=require('node:fs'), {runInNewContext}=require('node:vm');
function setup(fetcher=async()=>({ok:true,json:async()=>({options:[],message:'No routes found for these locations'})})) {
    const nodes=new Map(), handlers={};
    function node(){return {value:'',textContent:'',disabled:false,hidden:false,children:[],listeners:{},replaceChildren(){this.children=[];},appendChild(n){this.children.push(n);},addEventListener(k,fn){this.listeners[k]=fn;},emit(k){return this.listeners[k]?.({preventDefault(){}});}};}
    const doc={getElementById:id=>{if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);},createElement:()=>node(),dispatchEvent:e=>handlers[e.type]?.(e),addEventListener:(k,fn)=>handlers[k]=fn};
    const points={A:{name:'A',latitude:23.75,longitude:90.37},B:{name:'B',latitude:23.74,longitude:90.42}};
    const provider={load:async()=>Object.values(points),resolve:t=>points[t]||null,locate:async()=>points.A};
    const ctx={setTimeout,clearTimeout,AbortController,URLSearchParams,CustomEvent:class{constructor(type,args){this.type=type;this.detail=args.detail;}},TransportLocations:{inside:p=>!!p&&p.latitude>=23.6&&p.latitude<=23.95,mountMap:()=>null}};
    runInNewContext(readFileSync('wwwroot/js/transport-form.js','utf8'),ctx);
    doc.getElementById('transport-sort').value='fastest';
    const app=ctx.TransportForm.mount({document:doc,provider,fetcher,location:{search:''}});
    return {app,doc,provider,points,ctx,get:key=>doc.getElementById('transport-'+key)};
}
test('missing, same and outside points fail before any route request',async()=>{
    let calls=0;const s=setup(async()=>{calls++;throw Error('Should not request');});await s.app.ready;
    await s.app.search();assert.match(s.get('validation').textContent,/both origin/);assert.equal(calls,0);
    s.get('origin').value='A';s.get('destination').value='A';await s.app.search();assert.match(s.get('validation').textContent,/50 metres/);assert.equal(calls,0);
    assert.match(s.ctx.TransportForm.validate(s.points.A,{latitude:24,longitude:90.4}),/Dhaka/);
});
test('network error keeps both typed locations and retry succeeds',async()=>{
    let offline=true;const s=setup(async()=>{if(offline)throw new Error('Network lost');return {ok:true,json:async()=>({options:[],message:'No routes found for these locations'})};});await s.app.ready;
    s.get('origin').value='A';s.get('destination').value='B';await s.app.search();assert.match(s.get('status').textContent,/locations are kept/);
    assert.equal(s.get('origin').value,'A');assert.equal(s.get('destination').value,'B');assert.equal(s.get('search').disabled,false);
    offline=false;await s.get('retry').emit('click');assert.match(s.get('status').textContent,/No routes found/);
});
