(() => {
    function mount({document=globalThis.document,fetcher=(...args)=>globalThis.fetch(...args)}={}) {
        const get=id=>document.getElementById('air-'+id);if(!get('zone'))return;let sequence=0,active;
        async function load() {
            const current=++sequence;active?.abort();get('reading').replaceChildren();get('reading').hidden=true;
            const id=Number(get('zone').value);if(!id){get('status').textContent='Choose a zone to request an estimate.';return;}
            const controller=new AbortController();active=controller;const timeout=setTimeout(()=>controller.abort(),8000);get('status').textContent='Requesting provider estimate…';
            try {
                const response=await fetcher(`/api/air-quality?zoneId=${id}`,{signal:controller.signal,cache:'no-store',credentials:'omit'});const data=await response.json();if(!response.ok)throw new Error(data.message||'Request failed.');if(sequence!==current)return;
                get('status').textContent=`${data.status}. ${data.message}`;
                if(data.usAqi==null)return;
                const lines=[['h2',`${data.band} — US AQI ${data.usAqi}`],['p',`PM2.5: ${data.pm25} μg/m³ · PM10: ${data.pm10} μg/m³`],['p',`Source: ${data.source}`]];
                for(const [label,time] of [['Model time',data.atUtc],['Downloaded',data.fetchedAtUtc]])lines.push(['p',`${label}: ${new Date(time).toLocaleString('en-GB',{timeZone:'Asia/Dhaka'})} (Asia/Dhaka)`]);
                for(const [tag,text] of lines){const node=document.createElement(tag);node.textContent=text;get('reading').appendChild(node);}get('reading').hidden=false;
            } catch(error){if(sequence===current)get('status').textContent=`Estimate unavailable. Your zone is kept; refresh to retry. ${error.name==='AbortError'?'Request timed out.':error.message}`;}
            finally{clearTimeout(timeout);}
        }
        get('zone').addEventListener('change',load);get('refresh').addEventListener('click',load);return {load};
    }
    globalThis.AirQualityPage={mount};if(typeof module!=='undefined')module.exports=globalThis.AirQualityPage;if(typeof document!=='undefined')mount();
})();
