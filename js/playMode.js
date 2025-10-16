(function(){
  const qs = (s,root=document)=>root.querySelector(s);
  const qsa = (s,root=document)=>Array.from(root.querySelectorAll(s));
  const modes = qsa('.mode-card');
  let selectedMode = 'bracket';
  // rounds default server-side embed via data attr if provided
  let rounds = (function(){ try{ const el=document.getElementById('roundsArea'); const act=el?.querySelector('.round-pill.active'); return parseInt(act?.dataset.rounds||'8',10);}catch{return 8;} })();
  let slots = 5;
  const roundsArea = document.getElementById('roundsArea');
  const slotsArea = document.getElementById('slotsArea');

  modes.forEach(m=>{
    m.addEventListener('click',()=>{
      modes.forEach(x=>x.classList.remove('active'));
      m.classList.add('active');
      selectedMode = m.dataset.mode;
      if(selectedMode==='rank'){
        if(roundsArea) roundsArea.style.display='none';
        if(slotsArea) slotsArea.style.display='block';
      } else if(selectedMode==='bracket' || selectedMode==='vs'){
        if(roundsArea) roundsArea.style.display='block';
        if(slotsArea) slotsArea.style.display='none';
      } else {
        if(roundsArea) roundsArea.style.display='none';
        if(slotsArea) slotsArea.style.display='none';
      }
    });
  });
  document.addEventListener('click',(e)=>{
    const btn = e.target.closest?.('.round-pill');
    if(!btn) return;
    const wrap = btn.parentElement?.id;
    if(wrap==='roundsArea'){
      qsa('#roundsArea .round-pill').forEach(x=>x.classList.remove('active'));
      btn.classList.add('active');
      rounds = parseInt(btn.dataset.rounds||'8',10);
    }
    if(wrap==='slotsArea'){
      qsa('#slotsArea .round-pill').forEach(x=>x.classList.remove('active'));
      btn.classList.add('active');
      slots = parseInt(btn.dataset.slots||'5',10);
    }
  });
  const closePopovers = () => qsa('.info-pop').forEach(p=>p.remove());
  document.addEventListener('click', (e)=>{
    const info = e.target.closest?.('.info');
    const pop = e.target.closest?.('.info-pop');
    if(info){
      e.stopPropagation();
      const already = info.parentElement.querySelector('.info-pop');
      closePopovers();
      if(!already){
        const div = document.createElement('div');
        div.className = 'info-pop';
        div.setAttribute('role','dialog');
        div.setAttribute('aria-modal','false');
        div.innerText = info.dataset.info || '';
        info.parentElement.appendChild(div);
        setTimeout(()=>div.focus?.(),0);
      }
      return;
    }
    if(!pop){ closePopovers(); }
  });
  document.addEventListener('keydown', (e)=>{ if(e.key==='Escape'){ closePopovers(); }});
  const startBtn = qs('.pm-start');
  startBtn?.addEventListener('click',()=>{
    const id = startBtn.getAttribute('data-quiz-id') || (document.querySelector('[data-quiz-id]')?.getAttribute('data-quiz-id')) || '';
    if(!id) return;
    let base='';
    if(selectedMode==='bracket'){ base = startBtn.getAttribute('data-bracket-url'); }
    else if(selectedMode==='vs'){ base = startBtn.getAttribute('data-vs-url'); }
    else { base = startBtn.getAttribute('data-rank-url'); }
    if(!base) return;
    const url = new URL(base, window.location.origin);
    if(selectedMode==='rank'){ url.searchParams.set('slots', String(slots)); }
    else { url.searchParams.set('rounds', String(rounds)); }
    window.location.href = url.toString();
  });
})();
