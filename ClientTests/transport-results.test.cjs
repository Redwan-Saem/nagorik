const {test}=require('node:test'),assert=require('node:assert/strict');
const {fareText,mount}=require('../wwwroot/js/transport-results.js');
test('fare rendering distinguishes unavailable, exact and estimated ranges',()=>{
    assert.equal(fareText(null),'Fare unavailable');assert.equal(fareText({minimumBdt:10,maximumBdt:14,estimated:true}),'Estimated BDT 10–14');
    assert.equal(fareText({minimumBdt:20,maximumBdt:20,estimated:false}),'BDT 20');
});
test('options and detail render untrusted names as text, preserve safe URLs and draw selected legs',()=>{
    function node(tag='div'){return {tag,textContent:'',children:[],listeners:{},hidden:true,appendChild(n){this.children.push(n);},prepend(n){this.children.unshift(n);},replaceChildren(){this.children=[];},addEventListener(k,f){this.listeners[k]=f;},set innerHTML(_){throw Error('Unsafe HTML insertion');}};}
    const nodes=new Map();const doc={getElementById:id=>{if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);},createElement:node,addEventListener(){}};
    let drawn;const app=mount({document:doc,form:{draw:r=>drawn=r,getLatest:()=>null}});
    const option={id:'abc',minutes:30,distanceKm:7,transfers:0,isSample:true,fare:null,legs:[{mode:'Bus',direction:'<img src=x onerror=alert(1)>',source:'Fixture',distanceKm:7,minutes:30,isSample:true,fare:null}]};
    app.render({options:[option],query:'originLat=23.75',origin:{name:'A & B'},destination:{name:'C'},selectedId:'abc'});
    assert.equal(drawn,option);assert.equal(doc.getElementById('transport-detail').hidden,false);
    const card=doc.getElementById('transport-options').children[0],link=card.children.find(n=>n.tag==='a');
    assert.match(link.href,/^\/transport\/route\?/);assert.match(link.href,/originName=A\+%26\+B/);
    const list=doc.getElementById('transport-legs').children[1];assert.equal(list.children[0].textContent,option.legs[0].direction);
    app.render({options:[]});assert.equal(doc.getElementById('transport-options').children[0].textContent,'No routes found for these locations');
});
