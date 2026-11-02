(() => {
    const button=document.getElementById('site-menu-toggle'),nav=document.getElementById('site-navigation');
    if(!button||!nav||!globalThis.matchMedia)return;
    const small=globalThis.matchMedia('(max-width: 760px)');let expanded=false;
    function apply(){button.hidden=!small.matches;nav.hidden=small.matches&&!expanded;button.setAttribute('aria-expanded',String(!nav.hidden));}
    button.addEventListener('click',()=>{expanded=!expanded;apply();});
    small.addEventListener('change',()=>{expanded=false;apply();});
    document.addEventListener('keydown',event=>{if(event.key==='Escape'&&small.matches&&expanded){expanded=false;apply();button.focus();}});
    apply();
})();
