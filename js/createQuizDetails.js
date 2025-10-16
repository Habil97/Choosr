(function(){
  const title=document.getElementById('titleInput');
  const desc=document.getElementById('descInput');
  const cat=document.getElementById('categoryInput');
  const draftIdInput=document.getElementById('draftIdInput');
  const prevTitle=document.getElementById('prevTitle');
  const prevCat=document.getElementById('prevCategory');
  const prevTags=document.getElementById('prevTags');
  const prevAuthor=document.getElementById('prevAuthor');
  const cover=document.getElementById('coverInput');
  const prevCover=document.getElementById('prevCover');
  function sync(){ if(prevTitle) prevTitle.textContent = title.value || 'Başlık'; if(prevCat) prevCat.textContent = cat.value || 'Kategori'; }
  if(title) title.addEventListener('input',sync); if(cat) cat.addEventListener('change',sync);
  const authorName = document.body.getAttribute('data-author-name') || 'Kullanıcı';
  document.querySelectorAll('input[name=IsAnonymous]').forEach(r=>{
    r.addEventListener('change',()=>{
      const isAnon = document.querySelector('input[name=IsAnonymous]:checked')?.value === 'true';
      if(prevAuthor){ prevAuthor.textContent = isAnon ? 'Anonim' : authorName; }
    });
  });
  if(cover) cover.addEventListener('change',()=>{ if(cover.files && cover.files[0]){ const url = URL.createObjectURL(cover.files[0]); if(prevCover) prevCover.src = url; const cp=document.getElementById('coverPreview'); if(cp) cp.src = url; }});
  const tagsInput=document.getElementById('tagsInput');
  const addBtn=document.getElementById('addTagBtn');
  const suggestBtn=document.getElementById('suggestTagsBtn');
  const list=document.getElementById('tagsList');
  const tagsCsv=document.getElementById('tagsCsv');
  const store=[];
  function render(){
    if(prevTags) prevTags.textContent = store.length? store.map(x=>`#${x}`).join(' ') : 'etiket yok';
    if(list) list.innerHTML='';
    store.forEach((t,i)=>{
      const b=document.createElement('span'); b.className='tag-chip active'; b.textContent=t;
      const x=document.createElement('button'); x.type='button'; x.textContent='×'; x.className='btn btn-sm btn-link text-danger p-0 ms-1';
      x.onclick=()=>{store.splice(i,1); render();};
      const w=document.createElement('span'); w.className='d-inline-flex align-items-center border rounded-5 px-2'; w.append(b,x);
      if(list) list.append(w);
    });
    if(tagsCsv) tagsCsv.value = store.join(',');
  }
  function addTagFromInput(){
    if(!tagsInput) return;
    let v=tagsInput.value.trim(); if(!v) return; 
    v = v.toLowerCase();
    if(store.includes(v)) { tagsInput.value=''; return; }
    if(store.length>=24){ return; }
    store.push(v); tagsInput.value=''; render(); triggerAutosave();
    // fire-and-forget record
    try{ fetch('/Drafts/RecordSelectedTags', { method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify([v]) }); }catch{}
  }
  if(addBtn) addBtn.addEventListener('click',addTagFromInput);
  if(tagsInput) tagsInput.addEventListener('keydown',e=>{ if(e.key==='Enter'){ e.preventDefault(); addTagFromInput(); }});
  if(suggestBtn){
    suggestBtn.addEventListener('click', async ()=>{
      try{
        const qs = new URLSearchParams({ title: title?.value || '', description: desc?.value || '' });
        const r = await fetch('/Drafts/SuggestTags?'+qs.toString());
        if(!r.ok) return;
        const arr = await r.json();
        const newly = [];
        for(const t of arr){ if(!store.includes(t)) { store.push(t); newly.push(t); } }
        render();
        if(newly.length>0){ try{ fetch('/Drafts/RecordSelectedTags', { method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify(newly) }); }catch{} }
      }catch{}
    });
  }
  const form=document.getElementById('quizDetailsForm');
  if(form) form.addEventListener('submit',()=>{ if(tagsCsv) tagsCsv.value = store.join(','); });
  render();
  function debounce(fn,ms){ let to; return (...a)=>{ clearTimeout(to); to=setTimeout(()=>fn(...a),ms); }; }
  async function autosave(){
    try{
      const existingId = draftIdInput.value || localStorage.getItem('draftId');
      if(!existingId) return;
      const coverUrl = prevCover?.src || null;
      const isServerCover = coverUrl && (coverUrl.startsWith('/uploads/') || coverUrl.startsWith('http://') || coverUrl.startsWith('https://'));
      const draft = {
        id: existingId,
        title: title?.value || '',
        description: desc?.value || '',
        category: cat?.value || 'Genel',
        visibility: document.querySelector('input[name=Visibility]:checked')?.value || 'public',
        isAnonymous: document.querySelector('input[name=IsAnonymous]:checked')?.value === 'true',
        tags: store,
        coverImageUrl: isServerCover ? coverUrl : null,
      };
      const r = await fetch('/Drafts/Autosave',{ method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify(draft) });
      if(r.ok){ const res = await r.json(); draftIdInput.value = res.id; localStorage.setItem('draftId', res.id); }
    }catch(err){ console.warn('Autosave hata', err); }
  }
  const triggerAutosave = debounce(autosave, 600);
  [title, desc, cat, ...document.querySelectorAll('input[name=Visibility]'), ...document.querySelectorAll('input[name=IsAnonymous]')].forEach(el=>{
    if(!el) return;
    el.addEventListener('input', ()=>{ sync(); triggerAutosave(); });
    el.addEventListener('change', ()=>{ sync(); triggerAutosave(); });
  });
  if(addBtn) addBtn.addEventListener('click', ()=>triggerAutosave());
  if(suggestBtn) suggestBtn.addEventListener('click', ()=>triggerAutosave());
  window.__quizTagsStore = store;
  const fromQs = new URLSearchParams(location.search).get('draftId') || localStorage.getItem('draftId');
  if(fromQs){
    fetch('/Drafts/Get?id='+encodeURIComponent(fromQs)).then(async r=>{
      if(!r.ok) return; const d = await r.json(); draftIdInput.value=d.id; localStorage.setItem('draftId', d.id);
      if(title) title.value = d.title || ''; if(desc) desc.value = d.description || ''; if(cat) cat.value = d.category || '';
      if(Array.isArray(d.tags)){ store.splice(0, store.length, ...d.tags); }
      if(d.coverImageUrl){ if(prevCover) prevCover.src = d.coverImageUrl; const cp=document.getElementById('coverPreview'); if(cp) cp.src = d.coverImageUrl; }
      if(d.visibility){ const rv=document.querySelector('input[name=Visibility][value="'+d.visibility+'"]'); if(rv) rv.checked=true; }
      if(typeof d.isAnonymous === 'boolean'){ const ra=document.querySelector('input[name=IsAnonymous][value="'+(d.isAnonymous? 'true':'false')+'"]'); if(ra) ra.checked=true; }
      sync(); render();
    }).catch(()=>{});
  }
})();
