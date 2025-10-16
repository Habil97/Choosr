(function(){
 // Reaksiyon çubuğu
 const bar = document.querySelector('[data-reactbar]');
 if(bar){
   const quizId = bar.getAttribute('data-quizid');
   const anti = document.querySelector('meta[name="request-verification-token"]')?.content || '';
   const counters = { like:0,love:0,haha:0,wow:0,sad:0,angry:0 };
   const setCounts = (obj)=>{
     for(const k in counters){ counters[k] = obj[k]||0; const el = bar.querySelector(`.rx[data-type="${k}"] span`); if(el) el.textContent = counters[k]; }
   };
   fetch(`/Quiz/Reactions/${quizId}`).then(r=>r.json()).then(obj=>{ setCounts(obj); if(obj.my){ const el = bar.querySelector(`.rx[data-type="${obj.my}"]`); if(el) el.classList.add('active'); } }).catch(()=>{});
   bar.addEventListener('click', (e)=>{
     const btn = e.target.closest('.rx'); if(!btn) return;
     const type = btn.getAttribute('data-type');
     const url = (btn.classList.contains('active') ? `/Quiz/Unreact/${quizId}` : `/Quiz/React/${quizId}`);
     const body = btn.classList.contains('active') ? '' : `type=${encodeURIComponent(type)}`;
     fetch(url, { method:'POST', headers:{ 'Content-Type':'application/x-www-form-urlencoded', 'RequestVerificationToken': anti }, body })
       .then(r=> r.ok ? r.json(): Promise.reject())
       .then(obj=>{ setCounts(obj); bar.querySelectorAll('.rx').forEach(x=>x.classList.remove('active')); if(body){ btn.classList.add('active'); } })
       .catch(()=>{});
   });
 }
 document.addEventListener('click',e=>{
   if(e.target.matches('[data-copy]')){
     navigator.clipboard.writeText(window.location.href);
     e.target.innerText='Kopyalandı';
     setTimeout(()=>e.target.innerText='Link',1500);
   }
   if(e.target.matches('[data-spoiler-toggle]')){
     const box = document.querySelector('[data-spoiler]');
     if(box){ box.classList.toggle('revealed'); }
   }
   if(e.target.matches('[data-similar-more]')){
     const grid = document.querySelector('[data-similar]'); if(!grid) return;
     const step = parseInt(grid.getAttribute('data-step')||'4');
     const items = Array.from(grid.querySelectorAll('.sim[style*="display:none"], .sim[style*="display: none"]'));
     items.slice(0, step).forEach(el=> el.style.display = '');
     if(items.length <= step){ e.target.closest('div')?.remove(); }
   }
   if(e.target.matches('[data-winners-more]')){
     const list = document.querySelector('[data-winners]'); if(!list) return;
     list.querySelectorAll('li[style*="display:none"], li[style*="display: none"]').forEach(li=> li.style.display = '');
     e.target.closest('div')?.remove();
   }
   if(e.target.matches('[data-report-quiz]')){
     const anti = document.querySelector('meta[name="request-verification-token"]')?.content || '';
     const cap = (document.querySelector('input[name="cf-turnstile-response"]')?.value)||'';
     const qid = e.target.getAttribute('data-quiz-id') || bar?.getAttribute('data-quizid');
     const reason = prompt('Lütfen rapor nedenini kısaca açıklayın (ör. uygunsuz içerik).');
     if(!reason) return;
     fetch(`/report/quiz/${qid}`, { method:'POST', headers:{ 'Content-Type':'application/x-www-form-urlencoded', 'RequestVerificationToken': anti }, body: `reason=${encodeURIComponent(reason)}&cf-turnstile-response=${encodeURIComponent(cap)}` })
       .then(r=> r.ok ? r.json() : Promise.reject())
       .then(()=> showToast('Teşekkürler. Raporunuz alındı.'))
       .catch(()=> showToast('Rapor gönderilemedi. Lütfen daha sonra tekrar deneyin.'));
   }
   if(e.target.matches('[data-report-comment]')){
     const anti = document.querySelector('meta[name="request-verification-token"]')?.content || '';
     const cap = (document.querySelector('input[name="cf-turnstile-response"]')?.value)||'';
     const cid = e.target.getAttribute('data-id');
     const reason = prompt('Bu yorumu neden rapor ediyorsunuz?');
     if(!reason) return;
     fetch(`/report/comment/${cid}`, { method:'POST', headers:{ 'Content-Type':'application/x-www-form-urlencoded', 'RequestVerificationToken': anti }, body: `reason=${encodeURIComponent(reason)}&cf-turnstile-response=${encodeURIComponent(cap)}` })
       .then(r=> r.ok ? r.json() : Promise.reject())
       .then(()=> showToast('Teşekkürler. Raporunuz alındı.'))
       .catch(()=> showToast('Rapor gönderilemedi. Lütfen daha sonra tekrar deneyin.'));
   }
 });
 // Yorumlar
 (function(){
   const list = document.querySelector('[data-cl]'); if(!list) return;
   const cntEl = document.querySelector('[data-cnt]');
   const cf = document.querySelector('[data-cf]');
   const quizId = document.querySelector('[data-reactbar]')?.getAttribute('data-quizid');
   const anti = document.querySelector('meta[name="request-verification-token"]')?.content || '';
   const emptyEl = list.querySelector('[data-empty]');
   const escapeHtml = (s)=> (s||'').replace(/[&<>"]+/g, (m)=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;"}[m]||m));
   const formatTime = (iso)=>{ try{ const d=new Date(iso); return d.toLocaleString(); }catch{ return ''; } };
   const render = (c)=>{
     const li = document.createElement('div');
     li.className = 'comment-item';
     li.setAttribute('data-id', c.id);
     li.innerHTML = `<div class="meta"><span class="author">${escapeHtml(c.userName||'')}</span> · <span>${formatTime(c.createdAt)}</span></div><div class="text">${escapeHtml(c.text||'')}</div><div class="actions mt-1"><button type="button" class="btn btn-link btn-sm text-danger p-0" data-report-comment data-id="${c.id}">Rapor et</button></div>`;
     return li;
   };
   fetch(`/Quiz/Comments/${quizId}`).then(r=>r.json()).then(arr=>{ if(Array.isArray(arr) && arr.length){ emptyEl?.remove(); arr.forEach(c=> list.appendChild(render(c))); if(cntEl) cntEl.textContent = arr.length; } }).catch(()=>{});
   if(cf){
     cf.addEventListener('submit', (e)=>{
       e.preventDefault();
       const fd = new URLSearchParams(new FormData(cf));
       const text = (fd.get('text')||'').toString().trim();
       if(!text){ return; }
       const cap = (fd.get('cf-turnstile-response')||'').toString();
       fetch(`/Quiz/Comment/${quizId}`, { method:'POST', headers:{ 'Content-Type':'application/x-www-form-urlencoded', 'RequestVerificationToken': anti, 'Accept':'application/json' }, body: `text=${encodeURIComponent(text)}&cf-turnstile-response=${encodeURIComponent(cap)}` })
         .then(r=> r.ok ? r.json() : Promise.reject())
         .then(res=>{ const c=res.comment||res; emptyEl?.remove(); list.prepend(render(c)); cf.querySelector('textarea').value=''; if(window.turnstile){ try{ turnstile.reset(); }catch{} } if(cntEl){ cntEl.textContent = (res.quizComments ?? (parseInt(cntEl.textContent||'0')+1)); } const qc=document.querySelector('[data-qc]'); if(qc&&res.quizComments!=null){ qc.textContent=res.quizComments; } })
         .catch(()=>{});
     });
   }
 })();
})();
function showToast(msg){ const t=document.createElement('div'); t.className='toast-lite'; t.textContent=msg; document.body.appendChild(t); setTimeout(()=>t.classList.add('show'),10); setTimeout(()=>{ t.classList.remove('show'); setTimeout(()=>t.remove(),300); }, 2500); }
