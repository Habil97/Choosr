(function(){
  console.debug('[RANK] init');
  const cfg = window.__playData || {};
  const dataEl = document.getElementById('choicesData');
  let raw=[]; try{ raw = JSON.parse((dataEl?.textContent||'').trim()||'[]'); }catch(e){ console.warn('[RANK] parse error', e); }
  console.debug('[RANK] choices length', raw.length);
  if(!raw.length){ return; }
  const pc = window.PlayCommon || {};
  const toThumb = pc.toThumb || (i=> i?.image || '/img/sample1.jpg');
  const toEmbed = pc.toEmbed || (()=> null);
  const bsMedia = document.getElementById('bsMedia');
  const bsCap = document.getElementById('bsCap');
  const rankBg = document.getElementById('rankBg');
  const btnFullscreen = document.getElementById('btnFullscreen');
  const btnUndo = document.getElementById('btnUndo');
  const btnRandom = document.getElementById('btnRandom');
  const btnShare = document.getElementById('btnShare');
  // slots are already rendered server-side (.slot elements). We'll assign results sequentially by user clicking an empty slot after roulette stops.
  const slotEls = Array.from(document.querySelectorAll('.slot'));
  let order = new Array(slotEls.length).fill(null);
  // pool limited to slot count
  let pool = raw.map(c=>({ id:c.id, image:c.image||'', imageWidth:c.imageWidth||null, imageHeight:c.imageHeight||null, caption:c.caption||'', youtube:c.youtube||'' }));
  if(pool.length > slotEls.length) pool = pool.slice(0, slotEls.length);
  let available = pool.slice();
  let current=null; let spinning=false; let spinTimer=null; let spinStop=null; const history=[]; // for undo
  const forceImage = !!cfg.forceImage;
  function renderCurrent(item){ if(!bsMedia||!item) return; let emb=null; if(!forceImage){ try{ emb = toEmbed(item); }catch{} } if(emb){ bsMedia.innerHTML = `<iframe src="${emb}" title="YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="width:100%;height:100%;display:block;background:#000;"></iframe>`; } else { const wh=(item.imageWidth&&item.imageHeight)?` width="${item.imageWidth}" height="${item.imageHeight}"`:''; const src=item.image||toThumb(item)||'/img/sample1.jpg'; bsMedia.innerHTML = `<img src="${src}" alt=""${wh} decoding="async" style="width:100%;height:100%;object-fit:contain;display:block;" onerror="this.onerror=null;this.src='/img/sample1.jpg';"/>`; } bsCap.textContent = item.caption||''; if(rankBg) rankBg.style.backgroundImage = `url('${toThumb(item)}')`; }
  function startSpin(accel){
    if(!available.length){ return finish(); }
    if(available.length===1){ current=available[0]; return renderCurrent(current); }
    if(spinning){ return; }
    // güvenlik: eski zamanlayıcıları temizle
    if(spinTimer){ try{ clearInterval(spinTimer); }catch{} spinTimer=null; }
    if(spinStop){ try{ clearTimeout(spinStop); }catch{} spinStop=null; }
    spinning=true; let idx=0; const interval = accel?60:100; const stopAfter = accel?600:1100;
    console.debug('[RANK] start spin accel=',accel,'avail=',available.length);
    spinTimer=setInterval(()=>{
      if(!available.length) return;
      const item = available[idx % available.length];
      bsMedia.innerHTML = `<img src="${toThumb(item)}" alt="" decoding="async" style="width:100%;height:100%;object-fit:contain;display:block;"/>`;
      if(rankBg) rankBg.style.backgroundImage = `url('${toThumb(item)}')`;
      bsCap.textContent=''; idx++;
    },interval);
    spinStop=setTimeout(()=>{
      if(spinTimer){ clearInterval(spinTimer); spinTimer=null; }
      const item = available[Math.floor(Math.random()*available.length)];
      current=item; spinning=false; console.debug('[RANK] spin stop choose', item?.id); renderCurrent(item);
    }, stopAfter);
  }
  function place(index){ if(spinning || !current) return; const i=index-1; if(i<0||i>=order.length) return; if(order[i]) return; order[i]=current.id; history.push(i); const slot=slotEls[i]; slot.classList.add('filled'); console.debug('[RANK] place', current.id, 'at', index);
    const it=current; const wh=(it.imageWidth&&it.imageHeight)?` width="${it.imageWidth}" height="${it.imageHeight}"`:''; slot.innerHTML = `<span class="n">${index}</span><img src="${toThumb(it)}" alt=""${wh}/><span class="t">${(it.caption||'').replace(/&/g,'&amp;').replace(/</g,'&lt;') }</span>`;
    const pos = available.findIndex(x=>x.id===current.id); if(pos>-1) available.splice(pos,1); current=null; if(order.every(x=>!!x)){ finish(); } else { startSpin(); }
    if(btnUndo) btnUndo.disabled = history.length===0; }
  function undo(){ if(!history.length || spinning) return; // remove last placed
    const lastIndex = history.pop(); const slot = slotEls[lastIndex]; const choiceId = order[lastIndex]; order[lastIndex]=null; slot.classList.remove('filled'); slot.innerHTML = `<span class="n">${lastIndex+1}</span>`; // restore to available
    const found = raw.find(x=> x.id===choiceId); if(found && !available.find(a=>a.id===found.id)){ available.push(found); }
    if(btnUndo) btnUndo.disabled = history.length===0; if(!current) startSpin(true); }
  function finish(){ // submit ordering
    try{ if(!order.every(x=>!!x)) return; console.debug('[RANK] finish order', order); const anti=document.querySelector('meta[name="request-verification-token"]')?.content||''; fetch(cfg.recordRankUrl || (cfg.recordUrl+'?mode=rank'), { method:'POST', headers:{ 'Content-Type':'application/json','RequestVerificationToken':anti }, credentials:'same-origin', keepalive:true, body: JSON.stringify({ order }) }).catch(()=>{}) .finally(()=>{ const url = new URL(cfg.rankResultBase || window.location.href, window.location.origin); order.forEach(o=> url.searchParams.append('order', o)); window.location.href = url.toString(); }); }catch(e){ console.warn('[RANK] finish error', e); }
  }
  document.addEventListener('click', (e)=>{ const s=e.target.closest?.('.slot'); if(!s) return; const idx=parseInt(s.dataset.index||'0',10); place(idx); });
  btnFullscreen?.addEventListener('click', ()=>{ const el=document.documentElement; if(!document.fullscreenElement){ el.requestFullscreen?.(); } else { document.exitFullscreen?.(); } });
  btnRandom?.addEventListener('click', ()=>{ if(spinning){ // force early stop
      if(spinStop){ clearTimeout(spinStop); spinStop=null; }
      if(spinTimer){ clearInterval(spinTimer); spinTimer=null; }
      const item = available[Math.floor(Math.random()*available.length)]; current=item; spinning=false; renderCurrent(item);
    } else { startSpin(true); }
  });
  btnShare?.addEventListener('click', async ()=>{ try{ const url=window.location.href; const shareData={ title: document.title, text: document.title, url }; if(navigator.share){ await navigator.share(shareData); } else { await navigator.clipboard.writeText(url); btnShare.title='Kopyalandı'; setTimeout(()=> btnShare.title='Paylaş', 1400); } }catch{} });
  btnUndo?.addEventListener('click', undo);
  if(btnUndo) btnUndo.disabled = true;
  startSpin();
  // Retry fallback: spin başlamasına rağmen current render edilmemiş olabilir
  function ensureSpin(attempt){ if(current || spinning || !available.length) return; console.debug('[RANK] ensureSpin attempt', attempt); startSpin(true); }
  setTimeout(()=> ensureSpin(1), 300);
  setTimeout(()=> ensureSpin(2), 900);
  document.addEventListener('visibilitychange', ()=>{ if(document.visibilityState==='visible'){ ensureSpin('vis'); } });
  window.addEventListener('pageshow', (e)=>{ if(e.persisted){ ensureSpin('pageshow'); } });
})();
