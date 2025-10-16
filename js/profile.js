// profile.js
(function(){
  function activateTab(target){
    const tabBtn = document.querySelector('.tab-btn[data-tab="'+target+'"]');
    if(tabBtn){ tabBtn.click(); }
  }
  document.addEventListener('click',function(e){
    const b=e.target.closest('.tab-btn');
    if(b){
      const wrap=b.closest('.profile-content');
      if(!wrap) return;
      wrap.querySelectorAll('.tab-btn').forEach(x=>x.classList.toggle('active',x===b));
      const target='tab-'+b.dataset.tab;
      wrap.querySelectorAll('.tab-panel').forEach(p=>p.classList.toggle('active',p.id===target));
    }
    const statBtn = e.target.closest('.profile-stats button[data-tab-target]');
    if(statBtn){ e.preventDefault(); activateTab(statBtn.getAttribute('data-tab-target')); }
  });
  document.addEventListener('keydown',(e)=>{
    if(e.key==='Enter' || e.key===' '){
      const el=document.activeElement;
      if(el && el.matches('.profile-stats button[data-tab-target]')){ e.preventDefault(); activateTab(el.getAttribute('data-tab-target')); }
    }
  });
})();