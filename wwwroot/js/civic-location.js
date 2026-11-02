(() => {
    const button=document.getElementById('civic-gps');if(!button)return;
    const status=document.getElementById('civic-location-status'),lat=document.getElementById('Input_Latitude'),lon=document.getElementById('Input_Longitude');let generation=0;
    for(const input of [lat,lon])input.addEventListener('input',()=>generation++);
    button.addEventListener('click',()=>{
        if(!navigator.geolocation){status.textContent='GPS unavailable. Enter coordinates manually.';return;}
        const current=++generation;button.disabled=true;status.textContent='Waiting for location…';
        navigator.geolocation.getCurrentPosition(position=>{
            button.disabled=false;if(current!==generation)return;const a=position.coords.latitude,b=position.coords.longitude;
            if(!Number.isFinite(a)||!Number.isFinite(b)||a<23.65||a>23.95||b<90.30||b>90.55){status.textContent='Location is outside the Dhaka service area. Enter a valid concern location.';return;}
            lat.value=a;lon.value=b;status.textContent='Location selected. Add a landmark, check the coordinates and submit.';
        },()=>{button.disabled=false;if(current===generation)status.textContent='GPS denied or unavailable. Enter coordinates manually.';},{timeout:10000,maximumAge:60000});
    });
})();
