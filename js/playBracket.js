(function(){
  const dataEl = document.getElementById('choicesData');
  let raw=[]; try{ raw = JSON.parse((dataEl?.textContent||'').trim()||'[]'); }catch{}
  const pc = window.PlayCommon;
  const { toThumb, toEmbed } = pc;
  const cfg = window.__playData || {};
  const PLACEHOLDER = '/img/sample1.jpg';
  // Primary media containers; fallback to legacy img holders if needed
  const leftMedia = document.querySelector('#leftMedia') || document.querySelector('#leftImg');
  const rightMedia = document.querySelector('#rightMedia') || document.querySelector('#rightImg');
  const leftCap = document.getElementById('leftCap');
  const rightCap = document.getElementById('rightCap');
  const prog = document.getElementById('progressCount');
  const stagePill = document.getElementById('stagePill');
  const leftCard = document.getElementById('leftCard');
  const rightCard = document.getElementById('rightCard');
  const overlay = document.getElementById('winnerOverlay');
  const overlayImg = document.getElementById('winnerOverlayImg');
  const overlayCap = document.getElementById('winnerOverlayCap');
  const leftBg = document.getElementById('leftBg');
  const rightBg = document.getElementById('rightBg');
  const leftAudioBtn = document.getElementById('leftAudioBtn');
  const rightAudioBtn = document.getElementById('rightAudioBtn');
  // Topbar buttons
  const btnUndo = document.getElementById('btnUndo');
  const btnRandom = document.getElementById('btnRandom');
  const btnShare = document.getElementById('btnShare');
  const btnFullscreen = document.getElementById('btnFullscreen');
  let leftMuted=true, rightMuted=true;
  function setBtn(btn, mut){ if(!btn) return; btn.setAttribute('aria-pressed', (!mut).toString()); const ico = btn.querySelector('.ico'); const txt = btn.querySelector('.txt'); if(ico) ico.textContent = mut? '🔇':'🔊'; if(txt) txt.textContent = mut? 'Sessiz':'Ses Açık'; }
  function stageLabel(n){ if(n>=64) return 'Son 64'; if(n>=32) return 'Son 32'; if(n>=16) return 'Son 16'; if(n>=8) return 'Çeyrek Final'; if(n>=4) return 'Yarı Final'; if(n===2) return 'Final'; return 'Tur'; }
  const shuffle = (arr)=>{ for(let i=arr.length-1;i>0;i--){ const j=Math.floor(Math.random()*(i+1)); [arr[i],arr[j]]=[arr[j],arr[i]]; } return arr; };
  let input = raw.map(c=>({ id:c.id, image:c.image||'', imageWidth:c.imageWidth||null, imageHeight:c.imageHeight||null, caption:c.caption||'', youtube:c.youtube||'' }));
  const url = new URL(window.location.href);
  let rounds = parseInt(url.searchParams.get('rounds')||'8',10); if(!Number.isFinite(rounds)||rounds<2) rounds=8; rounds = Math.min(64, rounds);
  let pool = shuffle(input.slice(0, Math.min(rounds, input.length)));
  if(pool.length<2){ window.location.href = cfg.finishBase; return; }
  if(pool.length %2 ===1) pool = pool.slice(0,pool.length-1);
  let current = pool.slice(); let next=[]; let pairIndex=0; let locked=false; const winnersPath=[]; const matchResults=[]; const undoStack=[];
  let autoMode=false; let autoTimer=null; let holdTimer=null;
  let pendingSide='left';
  function renderPair(){
    const i = pairIndex*2; const a=current[i]; const b=current[i+1]; if(!a||!b){ return finish(); }
    const leftEmbed = toEmbed(a), rightEmbed = toEmbed(b);
  if(leftMedia){ if(leftEmbed){ leftMedia.innerHTML = `<iframe src="${leftEmbed}" title="YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="width:100%;height:100%;display:block;"></iframe>`; } else { const wh=(a.imageWidth&&a.imageHeight)?` width="${a.imageWidth}" height="${a.imageHeight}"`:''; leftMedia.innerHTML = `<img src="${a.image||PLACEHOLDER}" alt=""${wh} decoding="async" loading="lazy"/>`; } }
  if(rightMedia){ if(rightEmbed){ rightMedia.innerHTML = `<iframe src="${rightEmbed}" title="YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="width:100%;height:100%;display:block;"></iframe>`; } else { const wh=(b.imageWidth&&b.imageHeight)?` width="${b.imageWidth}" height="${b.imageHeight}"`:''; rightMedia.innerHTML = `<img src="${b.image||PLACEHOLDER}" alt=""${wh} decoding="async" loading="lazy"/>`; } }
  try{ const hasLeft = !!leftMedia?.querySelector?.('iframe'); const hasRight = !!rightMedia?.querySelector?.('iframe'); if(leftAudioBtn) leftAudioBtn.style.display = hasLeft? 'inline-flex':'none'; if(rightAudioBtn) rightAudioBtn.style.display = hasRight? 'inline-flex':'none'; leftMuted=true; rightMuted=true; setBtn(leftAudioBtn,true); setBtn(rightAudioBtn,true);}catch{}
    leftCap.textContent = a.caption||'Seçenek A'; rightCap.textContent = b.caption||'Seçenek B';
    if(leftBg) leftBg.style.backgroundImage = `url('${toThumb(a)}')`; if(rightBg) rightBg.style.backgroundImage = `url('${toThumb(b)}')`;
    prog.textContent = `${pairIndex+1}/${Math.floor(current.length/2)}`; stagePill.textContent = stageLabel(current.length);
  }
  function advance(selectedSide){
    const i = pairIndex*2; const winner = selectedSide==='left'? current[i]: current[i+1]; const loser = selectedSide==='left'? current[i+1]: current[i];
    if(!winner){ cleanup(); return; }
    // push undo snapshot BEFORE mutating state
    undoStack.push({ current:[...current], next:[...next], pairIndex, winnersPath:[...winnersPath], matchResults:[...matchResults] });
    next.push(winner); winnersPath.push(winner.id); if(winner&&loser){ matchResults.push({ winner: winner.id, loser: loser.id }); }
    pairIndex++; const totalPairs = Math.floor(current.length/2);
    if(pairIndex>=totalPairs){ current=shuffle(next); next=[]; pairIndex=0; if(current.length<=1){ cleanup(); return finish(); } }
    cleanup(); setTimeout(()=>{ renderPair(); locked=false; },120);
  }
  function cleanup(){ leftCard.classList.remove('winner','loser','disabled'); rightCard.classList.remove('winner','loser','disabled'); overlay.classList.remove('show'); overlay.style.pointerEvents='none'; }
  function pick(side){ if(locked) return; locked=true; pendingSide=side; leftCard.classList.add('disabled'); rightCard.classList.add('disabled'); if(side==='left'){ leftCard.classList.add('winner'); rightCard.classList.add('loser'); } else { rightCard.classList.add('winner'); leftCard.classList.add('loser'); }
    try{ leftMedia?.querySelector?.('iframe')?.contentWindow?.postMessage(JSON.stringify({ event:'command', func:'pauseVideo', args:[] }), '*'); rightMedia?.querySelector?.('iframe')?.contentWindow?.postMessage(JSON.stringify({ event:'command', func:'pauseVideo', args:[] }), '*'); }catch{}
    try{ const idx=pairIndex*2; const a=current[idx], b=current[idx+1]; const src = side==='left'? toThumb(a): toThumb(b); const cap = side==='left'? leftCap.textContent: rightCap.textContent; overlayImg.src=src||overlayImg.src; overlayCap.textContent=cap||''; overlay.classList.add('show'); }catch{}
    setTimeout(()=> advance(side), 1500);
  }
  overlay?.addEventListener('click', ()=>{ if(!locked||!overlay.classList.contains('show')) return; advance(pendingSide||'left'); });
  document.addEventListener('click', (e)=>{ const card = e.target.closest?.('.choice-card'); if(!card) return; pick(card.id==='leftCard'?'left':'right'); });
  document.addEventListener('keydown', (e)=>{ if(e.key==='ArrowLeft'){ pick('left'); } else if(e.key==='ArrowRight'){ pick('right'); } });
  function finish(){
    try{
      const championId = (current && current[0] && current[0].id) ? current[0].id : (winnersPath[winnersPath.length-1]||null);
      const payload = { champion: championId, matches: matchResults };
      let seed=''; try{ const ids=current.concat(next).map(x=>x.id); if(ids.length>0) seed=ids.join(','); }catch{}
      const challengeUrl = window.location.origin + cfg.bracketUrl + (seed? ('?seed='+encodeURIComponent(seed)):'');
      const anti = document.querySelector('meta[name="request-verification-token"]')?.content || '';
      fetch(cfg.recordUrl+'?mode=bracket', { method:'POST', headers:{ 'Content-Type':'application/json','RequestVerificationToken':anti }, credentials:'same-origin', keepalive:true, body: JSON.stringify(payload) })
        .catch(()=>{})
        .finally(()=>{ window.location.href = championId ? (cfg.finishBase + '?w='+championId + '&challenge=' + encodeURIComponent(challengeUrl)) : cfg.finishBase; });
    }catch{ window.location.href = cfg.finishBase; }
  }
  // Audio toggle wiring (optional if YouTube present)
  leftAudioBtn?.addEventListener('click', (e)=>{ e.stopPropagation(); const iframe = leftMedia?.querySelector('iframe'); if(!iframe) return; leftMuted=!leftMuted; setBtn(leftAudioBtn,leftMuted); try{ const cmd = leftMuted? 'mute':'unMute'; iframe.contentWindow?.postMessage(JSON.stringify({ event:'command', func:cmd, args:[] }), '*'); if(!leftMuted) iframe.contentWindow?.postMessage(JSON.stringify({ event:'command', func:'setVolume', args:[80] }), '*'); }catch{} });
  rightAudioBtn?.addEventListener('click', (e)=>{ e.stopPropagation(); const iframe = rightMedia?.querySelector('iframe'); if(!iframe) return; rightMuted=!rightMuted; setBtn(rightAudioBtn,rightMuted); try{ const cmd = rightMuted? 'mute':'unMute'; iframe.contentWindow?.postMessage(JSON.stringify({ event:'command', func:cmd, args:[] }), '*'); if(!rightMuted) iframe.contentWindow?.postMessage(JSON.stringify({ event:'command', func:'setVolume', args:[80] }), '*'); }catch{} });
  // Undo
  btnUndo?.addEventListener('click', ()=>{ if(!undoStack.length || locked) return; const snap = undoStack.pop(); current=[...snap.current]; next=[...snap.next]; pairIndex=snap.pairIndex; winnersPath.length=0; snap.winnersPath.forEach(x=> winnersPath.push(x)); matchResults.length=0; snap.matchResults.forEach(x=> matchResults.push(x)); locked=false; renderPair(); });
  // Random / auto mode
  function stopAuto(){ autoMode=false; if(autoTimer){ clearInterval(autoTimer); autoTimer=null; } btnRandom?.classList.remove('active'); }
  function startAuto(){ if(autoMode) return; autoMode=true; btnRandom?.classList.add('active'); autoTimer=setInterval(()=>{ if(locked) return; pick(Math.random()<0.5?'left':'right'); if(current.length<=1){ stopAuto(); } }, 1500); }
  btnRandom?.addEventListener('click', ()=>{ if(autoMode){ stopAuto(); } else { pick(Math.random()<0.5?'left':'right'); } });
  btnRandom?.addEventListener('mousedown', ()=>{ holdTimer=setTimeout(startAuto,600); });
  ['mouseup','mouseleave','mouseout'].forEach(ev=> btnRandom?.addEventListener(ev, ()=>{ if(holdTimer){ clearTimeout(holdTimer); holdTimer=null; } }));
  // Share
  btnShare?.addEventListener('click', async ()=>{ try{ const url=window.location.href; const shareData={ title: document.title, text: document.title, url }; if(navigator.share){ await navigator.share(shareData); } else { await navigator.clipboard.writeText(url); btnShare.title='Kopyalandı'; setTimeout(()=> btnShare.title='Paylaş', 1400); } }catch{} });
  // Fullscreen
  btnFullscreen?.addEventListener('click', ()=>{ const el=document.documentElement; if(!document.fullscreenElement){ el.requestFullscreen?.(); } else { document.exitFullscreen?.(); } });
  renderPair();
})();
