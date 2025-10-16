(function(){
 const draftIdInput=document.getElementById('draftIdInput');
 const changeCoverBtn=document.getElementById('changeCoverBtn');
 const coverOverride=document.getElementById('coverOverride');
 const step2Cover=document.getElementById('step2CoverPreview');
 if(changeCoverBtn && coverOverride && step2Cover){
   changeCoverBtn.addEventListener('click',()=>coverOverride.click());
   coverOverride.addEventListener('change',()=>{
     if(coverOverride.files && coverOverride.files[0]){
       const url=URL.createObjectURL(coverOverride.files[0]);
       step2Cover.src=url;
     }
   });
 }
 const fileInput=document.getElementById('fileInput');
 const selectBtn=document.getElementById('selectFiles');
 const grid=document.getElementById('choicesGrid');
 const dropArea=document.getElementById('imageUploadArea');
 const modeRadios=document.querySelectorAll('input[name=Mode]');
 const imageArea=document.getElementById('imageUploadArea');
 const videoArea=document.getElementById('videoLinksArea');
 const count=document.getElementById('count');
 const removeAll=document.getElementById('removeAll');
 const addVideos=document.getElementById('addVideos');
 const videoLinks=document.getElementById('videoLinks');
 const manifestInput=document.getElementById('manifestInput');
 const form=document.getElementById('choicesForm');
 const MAX=64, MIN=8;
 const choices=[]; // { kind:'image'|'video', fileIndex?:number, youtubeUrl?:string, caption?:string }
 const TEMP_KEY = 'createChoicesTemp';
 function setDraftId(id){
   if(!id) return;
   draftIdInput.value = id;
   try{ localStorage.setItem('draftId', id); }catch{}
   try{
     const url = new URL(window.location.href);
     url.searchParams.set('draftId', id);
     window.history.replaceState({}, '', url);
   }catch{}
 }
 function persistTemp(){
   const coverUrl = step2Cover?.src || null;
   try{
     const payload = { cover: coverUrl, choices: choices };
     localStorage.setItem(TEMP_KEY, JSON.stringify(payload));
   }catch{}
 }
 function updateCount(){ if(count) count.textContent = grid.children.length; }
 function renderCard(idx){
   const ch=choices[idx];
   const card=document.createElement('div');card.className='choice-item';card.dataset.idx=idx;
   const imgSrc = ch.kind==='image' ? (ch.imageUrl || ch.url || card.dataset.preview || '') : (ch.thumb || '');
   const input = `<input class='form-control form-control-sm mt-1 caption-input' placeholder='Başlık' value='${ch.caption||''}'/>`;
   const btn = `<button type='button' class='btn btn-sm btn-link text-danger p-0 removeChoice'>KALDIR</button>`;
   if(ch.kind==='image'){
     if(!imgSrc && fileInput?.files && ch.fileIndex!=null){ const f = fileInput.files[ch.fileIndex]; if(f){ const url = URL.createObjectURL(f); card.dataset.preview=url; } }
   }
   if(ch.kind==='video'){
     try{ const u=new URL(ch.youtubeUrl); const v=u.searchParams.get('v'); if(v){ ch.thumb=`https://img.youtube.com/vi/${v}/hqdefault.jpg`; } }catch{}
   }
   const imgHtml = (imgSrc || ch.thumb) ? `<img src='${imgSrc || ch.thumb}'/>` : '';
   card.innerHTML = `${imgHtml}${input}${btn}`;
   grid.appendChild(card);
 }
 function reRender(){
   grid.innerHTML='';
   for(let i=0;i<choices.length;i++) renderCard(i);
   updateCount();
   if(!(draftIdInput.value || localStorage.getItem('draftId'))){ persistTemp(); }
 }
 async function uploadAndAdd(files){
   if(!files || files.length===0) return;
   const fd = new FormData();
   Array.from(files).forEach(f=>fd.append('files', f));
   try{
     const r = await fetch('/Drafts/UploadChoices', { method:'POST', body: fd });
     if(!r.ok) return;
     const arr = await r.json();
     for(const it of arr){ if(choices.length>=MAX) break; choices.push({kind:'image', imageUrl: it.imageUrl}); }
     reRender();
     autosave();
     if(!(draftIdInput.value || localStorage.getItem('draftId'))){ persistTemp(); }
   }catch{}
 }
 function addImageChoices(files){ uploadAndAdd(files); }
 function addVideoChoices(urls){
   for(const l of urls){ if(grid.children.length>=MAX) break; choices.push({kind:'video', youtubeUrl:l}); }
   reRender();
 }
 if(selectBtn) selectBtn.addEventListener('click',()=>fileInput?.click());
 if(fileInput) fileInput.addEventListener('change',()=>{ addImageChoices(fileInput.files); });
 ['dragenter','dragover','dragleave','drop'].forEach(ev=>{ if(dropArea) dropArea.addEventListener(ev, e=>{ e.preventDefault(); e.stopPropagation(); }); });
 if(dropArea){
   dropArea.addEventListener('dragenter', ()=> dropArea.classList.add('border-warning'));
   dropArea.addEventListener('dragleave', ()=> dropArea.classList.remove('border-warning'));
   dropArea.addEventListener('drop', (e)=>{ dropArea.classList.remove('border-warning'); const dt=e.dataTransfer; if(dt && dt.files && dt.files.length){ uploadAndAdd(dt.files); } });
 }
 modeRadios.forEach(r=>r.addEventListener('change',()=>{
   if(r.checked && r.value==='videos'){imageArea?.classList.add('d-none');videoArea?.classList.remove('d-none');}
   if(r.checked && r.value==='images'){videoArea?.classList.add('d-none');imageArea?.classList.remove('d-none');}
 }));
 if(removeAll) removeAll.addEventListener('click',()=>{ choices.length=0; reRender(); });
 if(addVideos) addVideos.addEventListener('click',()=>{ const lines=videoLinks.value.split(/\n+/).map(s=>s.trim()).filter(Boolean); addVideoChoices(lines); videoLinks.value=''; });
 if(grid) grid.addEventListener('click',(e)=>{ const t=e.target; if(t && t.classList && t.classList.contains('removeChoice')){ const card=t.closest('.choice-item'); const idx=parseInt(card.dataset.idx,10); choices.splice(idx,1); reRender(); }});
 if(form) form.addEventListener('submit',()=>{
   const inputs=grid.querySelectorAll('.choice-item');
   inputs.forEach((card,i)=>{ const cap=card.querySelector('.caption-input'); if(choices[i]) choices[i].caption = cap ? cap.value : choices[i].caption; });
   if(manifestInput) manifestInput.value = JSON.stringify(choices);
   try{ localStorage.removeItem(TEMP_KEY); }catch{}
 });
 function debounce(fn,ms){ let to; return (...a)=>{ clearTimeout(to); to=setTimeout(()=>fn(...a),ms); }; }
 let saving=false, pending=false;
 async function autosave(){
  try{
    if(saving){ pending=true; return; }
    saving=true; pending=false;
    const inputs=grid.querySelectorAll('.choice-item');
    inputs.forEach((card,i)=>{ const cap=card.querySelector('.caption-input'); if(choices[i]) choices[i].caption = cap ? cap.value : choices[i].caption; });
    let existingId = draftIdInput.value || localStorage.getItem('draftId');
    if(!existingId && choices.length>0){ existingId = '00000000-0000-0000-0000-000000000000'; }
    if(!existingId) { return; }
    const coverUrl = step2Cover?.src || null;
    const isServerCover = coverUrl && (coverUrl.startsWith('/uploads/') || coverUrl.startsWith('http://') || coverUrl.startsWith('https://'));
    const draft = { id: existingId, title: '', description: '', category: '', visibility: 'public', isAnonymous: false, tags: [], coverImageUrl: isServerCover ? coverUrl : null, choices: choices };
    const r = await fetch('/Drafts/Autosave',{ method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify(draft) });
    if(r.ok){ const res = await r.json(); setDraftId(res.id); }
  }catch{}
  finally{ saving=false; if(pending){ pending=false; autosave(); } }
 }
 const saveNow = debounce(autosave, 800);
 [videoLinks, removeAll, addVideos].forEach(el=>{ if(el){ el.addEventListener('change', saveNow); el.addEventListener('click', saveNow); }});
 if(grid){ grid.addEventListener('input', saveNow); grid.addEventListener('click', (e)=>{ const t=e.target; if(t && t.classList && t.classList.contains('removeChoice')) saveNow(); }); }
 const fromQs = new URLSearchParams(location.search).get('draftId') || localStorage.getItem('draftId');
 if(fromQs){
   draftIdInput.value = fromQs;
   fetch(`/Drafts/Get?id=${encodeURIComponent(fromQs)}`)
     .then(r=> r.ok ? r.json() : null)
     .then(d=>{
       if(!d) return;
       if(d.coverImageUrl){ const el=document.getElementById('step2CoverPreview'); if(el) el.src = d.coverImageUrl; }
       if(Array.isArray(d.choices) && d.choices.length>0){
          choices.length = 0;
          for(const c of d.choices){
            if(c.imageUrl){ choices.push({ kind:'image', imageUrl: c.imageUrl, caption: c.caption||'' }); }
            else if(c.youtubeUrl){ choices.push({ kind:'video', youtubeUrl: c.youtubeUrl, caption: c.caption||'' }); }
          }
          reRender();
        }
     })
     .catch(()=>{});
 } else {
   try{
     const raw = localStorage.getItem(TEMP_KEY);
     if(raw){
       const tmp = JSON.parse(raw);
       if(tmp && Array.isArray(tmp.choices) && tmp.choices.length>0){
         choices.length = 0;
         for(const c of tmp.choices){
           if(c.kind==='image' && c.imageUrl){ choices.push({ kind:'image', imageUrl: c.imageUrl, caption: c.caption||'' }); }
           else if(c.kind==='video' && c.youtubeUrl){ choices.push({ kind:'video', youtubeUrl: c.youtubeUrl, caption: c.caption||'' }); }
         }
         if(tmp.cover){ step2Cover.src = tmp.cover; }
         reRender();
       }
     }
   }catch{}
 }
 window.addEventListener('beforeunload',()=>{ if(!(draftIdInput.value || localStorage.getItem('draftId'))){ try{ persistTemp(); }catch{} } });
})();
