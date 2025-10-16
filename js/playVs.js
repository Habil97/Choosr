(function(){
  const cfg = window.__playData || {};
  const dataEl = document.getElementById('choicesData');
  let raw=[]; try{ raw=JSON.parse((dataEl?.textContent||'').trim()||'[]'); }catch(e){ console.warn('[VS] choices JSON parse error', e); }
  console.debug('[VS] init raw length:', raw.length);
  if(!Array.isArray(raw) || raw.length<2){ window.location.href = cfg.finishUrl || cfg.finishBase || '#'; return; }
  const pc = window.PlayCommon || {};
  const toThumb = pc.toThumb || (i=> i?.image || '/img/sample1.jpg');
  const toEmbed = pc.toEmbed || (()=> null);
  const leftMedia = document.getElementById('leftMedia');
  const rightMedia = document.getElementById('rightMedia');
  const leftCap = document.getElementById('leftCap');
  const rightCap = document.getElementById('rightCap');
  const leftCard = document.getElementById('leftCard');
  const rightCard = document.getElementById('rightCard');
  const vsSteps = document.getElementById('vsSteps');
  const vsStage = document.getElementById('vsStage');
  const overlay = document.getElementById('winnerOverlay');
  const overlayImg = document.getElementById('winnerOverlayImg');
  const overlayCap = document.getElementById('winnerOverlayCap');
  const btnUndo = document.getElementById('btnUndo');
  const btnRandom = document.getElementById('btnRandom');
  const btnShare = document.getElementById('btnShare');
  const btnFullscreen = document.getElementById('btnFullscreen');
  const leftBg = document.getElementById('leftBg');
  const rightBg = document.getElementById('rightBg');
  const PLACEHOLDER = '/img/sample1.jpg';
  const shuffle = (arr)=>{ for(let i=arr.length-1;i>0;i--){ const j=Math.floor(Math.random()*(i+1)); [arr[i],arr[j]]=[arr[j],arr[i]]; } return arr; };
  const seedParam = new URLSearchParams(window.location.search).get('seed');
  let arr = raw.map(c=>({ id:c.id, image:c.image||'', imageWidth:c.imageWidth||null, imageHeight:c.imageHeight||null, caption:c.caption||'', youtube:c.youtube||'' }));
  if(seedParam){ try{ const ids = seedParam.split(','); const mapped = ids.map(id=> arr.find(x=> String(x.id)===id)); if(mapped.filter(Boolean).length>=2) arr=mapped.filter(Boolean); }catch{} }
  arr = shuffle(arr);
  let winner = arr[0];
  let queue = arr.slice(1);
  let sticky = Math.random()<0.5? 'left':'right';
  const matches = [];
  const undoStack = [];
  let done = 0; const totalSelections = arr.length - 1;
  let overlayTimer = null; const ANIM_MS = 900;
  let autoRandom=false; let autoTimer=null; let holdTimer=null; // uzun basılı tutma ile random auto mode

  function stageText(){ if(totalSelections>=64) return 'Maraton'; if(totalSelections>=32) return 'Uzun Seri'; if(totalSelections>=16) return 'Orta Seri'; if(totalSelections>=8) return 'Kısa Seri'; return 'Hızlı'; }
  function updateRemain(){ const left = Math.max(0, totalSelections - done); if(vsSteps) vsSteps.textContent = left>0? `Son ${left} Seçim`:'Bitti'; if(vsStage){ vsStage.textContent=stageText(); } }
  const forceImage = !!cfg.forceImage;
  function renderMedia(el,item){ if(!el||!item){ return; } let emb=null; if(!forceImage){ try{ emb = toEmbed(item); }catch{} }
    if(emb){
      el.innerHTML = `<iframe src="${emb}" title="YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="width:100%;height:100%;display:block;background:#000;"></iframe>`;
    } else {
      const src = item.image || toThumb(item) || PLACEHOLDER; const wh=(item.imageWidth&&item.imageHeight)?` width="${item.imageWidth}" height="${item.imageHeight}"`:'';
      el.innerHTML = `<img src="${src}" alt=""${wh} decoding="async" loading="lazy" style="max-width:100%;max-height:100%;object-fit:contain;display:block;" onerror="this.onerror=null;this.src='${PLACEHOLDER}';"/>`;
    }
  }
  function clearOverlay(){ overlay.classList.remove('show'); overlay.style.pointerEvents='none'; }
  function renderPair(){ if(queue.length===0){ return finish(); } const challenger = queue[0]; console.debug('[VS] render pair winner', winner?.id, 'challenger', challenger?.id, 'sticky', sticky);
    if(sticky==='left'){ renderMedia(leftMedia, winner); renderMedia(rightMedia, challenger); leftCap.textContent = winner.caption || ''; rightCap.textContent = challenger.caption || ''; if(leftBg) leftBg.style.backgroundImage=`url('${toThumb(winner)}')`; if(rightBg) rightBg.style.backgroundImage=`url('${toThumb(challenger)}')`; } else { renderMedia(rightMedia, winner); renderMedia(leftMedia, challenger); rightCap.textContent = winner.caption || ''; leftCap.textContent = challenger.caption || ''; if(rightBg) rightBg.style.backgroundImage=`url('${toThumb(winner)}')`; if(leftBg) leftBg.style.backgroundImage=`url('${toThumb(challenger)}')`; } leftCard.classList.remove('winner','loser','disabled'); rightCard.classList.remove('winner','loser','disabled'); clearOverlay(); updateRemain(); }
  function advance(side){ const challenger = queue[0]; if(!challenger){ return finish(); }
    undoStack.push({ winner, queue:[...queue], sticky, done });
    try{ leftMedia?.querySelector('iframe')?.contentWindow?.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}), '*'); rightMedia?.querySelector('iframe')?.contentWindow?.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}), '*'); }catch{}
    const selectedIsWinner = (sticky==='left' && side==='left') || (sticky==='right' && side==='right');
    const challengerItem = challenger; let roundWinner, roundLoser; if(selectedIsWinner){ roundWinner=winner; roundLoser=challengerItem; } else { roundWinner=challengerItem; roundLoser=winner; }
    matches.push({ winner: roundWinner.id, loser: roundLoser.id });
    winner = roundWinner; queue = queue.slice(1); sticky = side; done++;
    if(queue.length===0){ return finish(); }
    setTimeout(renderPair, 80);
  }
  function pick(side){ if(!queue[0]) return finish(); leftCard.classList.add('disabled'); rightCard.classList.add('disabled'); if(side==='left'){ leftCard.classList.add('winner'); rightCard.classList.add('loser'); } else { rightCard.classList.add('winner'); leftCard.classList.add('loser'); }
    try{ const challenger = queue[0]; const isLeftWinner = (sticky==='left'); const leftItem = isLeftWinner? winner: challenger; const rightItem = isLeftWinner? challenger: winner; const selectedItem = side==='left'? leftItem: rightItem; overlayImg.src = toThumb(selectedItem); overlayCap.textContent = (side==='left'? leftCap.textContent: rightCap.textContent)||''; overlay.classList.add('show'); overlay.style.pointerEvents='auto'; }catch{}
    if(overlayTimer) clearTimeout(overlayTimer); overlayTimer=setTimeout(()=> advance(side), ANIM_MS);
  }
  function undo(){ if(!undoStack.length) return; winner=undoStack.at(-1).winner; queue=undoStack.at(-1).queue; sticky=undoStack.at(-1).sticky; done=undoStack.at(-1).done; undoStack.pop(); renderPair(); }
  function stopAuto(){ autoRandom=false; if(autoTimer){ clearInterval(autoTimer); autoTimer=null; } if(btnRandom){ btnRandom.classList.remove('active'); btnRandom.title='Zar at'; } }
  function startAuto(){ if(autoRandom||queue.length===0) return; autoRandom=true; if(btnRandom){ btnRandom.classList.add('active'); btnRandom.title='Otomatik seçim açık'; } autoTimer=setInterval(()=>{ if(queue.length===0){ stopAuto(); return; } pick(Math.random()<0.5?'left':'right'); }, 1400); }
  function finish(){ updateRemain(); stopAuto(); const championId = winner?.id||null; const recordBase = cfg.recordBase || cfg.recordUrl || ''; const finishBase = cfg.finishUrl || cfg.finishBase || ''; const anti=document.querySelector('meta[name="request-verification-token"]')?.content||''; console.debug('[VS] finish champion', championId);
    let seed=''; try{ const order=[winner.id].concat(queue.map(x=>x.id)); if(order.length>1) seed=order.join(','); }catch{}
    const challengeUrl = window.location.origin + window.location.pathname + (seed?('?seed='+encodeURIComponent(seed)):'');
    const go=()=>{ window.location.href = championId ? (finishBase + '?w='+championId + (seed?('&challenge='+encodeURIComponent(challengeUrl)):'') ): finishBase; };
    try{ fetch(recordBase+'?mode=vs', { method:'POST', headers:{ 'Content-Type':'application/json','RequestVerificationToken':anti }, credentials:'same-origin', keepalive:true, body: JSON.stringify({ champion: championId, matches }) }).catch(()=>{}).finally(go); }catch{ go(); }
  }
  // Events
  document.addEventListener('click', (e)=>{ const card=e.target.closest?.('.choice-card'); if(!card) return; e.preventDefault(); if(card.id==='leftCard') pick('left'); else if(card.id==='rightCard') pick('right'); });
  document.addEventListener('keydown', (e)=>{ if(e.key==='ArrowLeft'){ e.preventDefault(); pick('left'); } else if(e.key==='ArrowRight'){ e.preventDefault(); pick('right'); } else if(e.key==='Enter'){ e.preventDefault(); pick(sticky); } });
  overlay.addEventListener('click', ()=>{ if(overlayTimer){ clearTimeout(overlayTimer); overlayTimer=null; } advance(sticky); });
  btnUndo?.addEventListener('click', undo);
  if(btnRandom){
    btnRandom.addEventListener('click', ()=>{ if(autoRandom){ stopAuto(); } else { pick(Math.random()<0.5?'left':'right'); } });
    btnRandom.addEventListener('mousedown', ()=>{ holdTimer=setTimeout(startAuto,600); });
    ['mouseup','mouseleave','mouseout'].forEach(ev=> btnRandom.addEventListener(ev, ()=>{ if(holdTimer){ clearTimeout(holdTimer); holdTimer=null; } }));
  }
  btnShare?.addEventListener('click', async ()=>{ try{ const url=window.location.href; const shareData={ title: document.title, text: document.title, url }; if(navigator.share){ await navigator.share(shareData); } else { await navigator.clipboard.writeText(url); btnShare.title='Kopyalandı'; setTimeout(()=> btnShare.title='Paylaş', 1400); } }catch{} });
  btnFullscreen?.addEventListener('click', ()=>{ const el=document.documentElement; if(!document.fullscreenElement){ el.requestFullscreen?.(); } else { document.exitFullscreen?.(); } });
  renderPair();
  // Retry fallback: bazen ilk load sırasında (ör. cache race) media boş kalırsa tekrar dene
  function ensureRender(attempt){
    if(queue.length===0) return; // finished already
    const lmOk = !!leftMedia?.firstElementChild; const rmOk = !!rightMedia?.firstElementChild;
    if(lmOk && rmOk) return; // already fine
    console.debug('[VS] ensureRender attempt', attempt, 'lmOk', lmOk, 'rmOk', rmOk);
    renderPair();
  }
  setTimeout(()=> ensureRender(1), 250);
  setTimeout(()=> ensureRender(2), 800);
  document.addEventListener('visibilitychange', ()=>{ if(document.visibilityState==='visible'){ ensureRender('vis'); } });
  window.addEventListener('pageshow', (e)=>{ if(e.persisted){ ensureRender('pageshow'); } });
})();
