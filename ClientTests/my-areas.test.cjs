const { test } = require('node:test'); const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs'); const { runInNewContext } = require('node:vm');
function setup() {
    const context = {}; runInNewContext(readFileSync('wwwroot/js/my-areas.js','utf8'),context);
    const nodes = new Map(), calls = []; let subscriptions = [], gps;
    const element = () => ({children:[],dataset:{},value:'',checked:true,appendChild(c){this.children.push(c);},replaceChildren(){this.children=[];},
        addEventListener(e,fn){this[e]=fn;},set innerHTML(x){assert.fail('Unsafe HTML');}});
    const get = id => { if(!nodes.has(id)) nodes.set(id,element()); return nodes.get(id); };
    const api = context.MyAreas.mount({document:{getElementById:get,createElement:element,querySelector:()=>({value:'csrf-token'})},
        navigator:{geolocation:{getCurrentPosition(ok){gps=ok;}}},locator:{contains:()=>true},
        fetcher:async(url,options={})=>{calls.push({url,options}); if(options.method==='PUT') subscriptions=[{zoneId:1,alertsEnabled:JSON.parse(options.body).alertsEnabled}];
            if(options.method==='DELETE')subscriptions=[];
            return {ok:true,json:async()=>url.startsWith('/api/public/')?{features:[{id:1,geometry:{},properties:{name:'Suggested'}}]}:
                {zones:[{id:1,name:'<script>zone</script>',code:'Z1'}],subscriptions,globalNotificationsEnabled:false}};}});
    return {api,get,calls,gps:()=>gps};
}
test('saves only the selected zone and alert flag with CSRF; renders safe text and removes',async()=>{
    const state=setup();await state.api.ready;assert.match(state.get('areas-global-status').textContent,/OFF/);
    await state.api.write(1,'PUT',true);const request=state.calls.find(c=>c.options.method==='PUT');
    assert.equal(request.options.headers.RequestVerificationToken,'csrf-token');assert.deepEqual(JSON.parse(request.options.body),{alertsEnabled:true});
    assert.match(state.get('areas-list').children[0].children[0].textContent,/<script>zone/);
    await state.api.write(1,'DELETE');assert.equal(state.get('areas-status').textContent,'Area removed.');
});
test('GPS suggests a selection without subscribing or uploading coordinates',async()=>{
    const state=setup();await state.api.ready;state.get('area-locate').click();await state.gps()({coords:{latitude:23.7,longitude:90.3}});
    assert.equal(state.get('area-zone').value,'1');assert.match(state.get('areas-status').textContent,/Review your choice/);
    assert.ok(state.calls.every(c=>!c.options.method));assert.ok(state.calls.every(c=>!c.url.includes('23.7')));
});
