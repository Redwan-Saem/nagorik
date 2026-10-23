const {test}=require('node:test');const assert=require('node:assert/strict');
const {mountNotificationSettings}=require('../wwwroot/js/notification-settings.js');
const {enablePush}=require('../wwwroot/js/push-subscribe.js');
test('global preferences use the real area API and never overwrite memberships',async()=>{
    const nodes=new Map(),calls=[];const get=id=>{if(!nodes.has(id))nodes.set(id,{addEventListener(e,fn){this[e]=fn;}});return nodes.get(id);};
    const view=mountNotificationSettings({document:{getElementById:get,querySelector:()=>({value:'csrf'})},fetcher:async(url,options={})=>{
        calls.push({url,options});return{ok:true,json:async()=>({globalNotificationsEnabled:false})};}});
    await view.ready;get('notifications-enabled').checked=true;await get('save-settings').click();
    const write=calls.find(c=>c.options.method==='PUT');assert.equal(write.url,'/api/resident/areas/global-preference');
    assert.deepEqual(JSON.parse(write.options.body),{enabled:true});assert.equal(write.options.headers.RequestVerificationToken,'csrf');
    assert.match(get('settings-status').textContent,/Saved areas are unchanged/);
});
test('already-granted push permission still saves an existing device subscription once',async()=>{
    const calls=[];let registrations=0;
    const subscription={toJSON:()=>({endpoint:'https://push.example.invalid/device',keys:{p256dh:'key',auth:'auth'}})};
    const registration={pushManager:{getSubscription:async()=>subscription,subscribe:async()=>{assert.fail('Must reuse device subscription');}}};
    const result=await enablePush({navigator:{serviceWorker:{register:async()=>{registrations++;},ready:Promise.resolve(registration)}},
        Notification:{permission:'granted',requestPermission:async()=>{assert.fail('Permission already granted');}},PushManager:function(){},
        fetcher:async(url,options={})=>{calls.push({url,options});return{ok:true,json:async()=>({key:'AQID'})};}});
    assert.equal(result.ok,true);assert.equal(registrations,1);assert.equal(calls.filter(c=>c.options.method==='POST').length,1);
});
test('denied push permission has explicit feedback without registering a worker',async()=>{
    const result=await enablePush({navigator:{serviceWorker:{register:async()=>assert.fail('Denied push permission must not register a worker')}},
        Notification:{permission:'default',requestPermission:async()=>'denied'},PushManager:function(){}});
    assert.equal(result.ok,false);assert.match(result.message,/permission was not granted/);
});
