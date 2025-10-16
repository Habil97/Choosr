// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Genel küçük UX / Erişilebilirlik geliştirmeleri
document.addEventListener('keydown',e=>{
	if(e.key==='/' && document.activeElement===document.body){
		const search=document.querySelector('header input[type=search]');
		if(search){search.focus();e.preventDefault();}
	}
});
// Kartlara klavye ile enter ile git
document.addEventListener('keydown',e=>{
	if(e.key==='Enter'){
		const el=document.activeElement;
		if(el && el.classList.contains('quiz-card')){el.click();}
	}
});

// Basit slider logic (prev/next butonları, drag, wheel)
document.addEventListener('DOMContentLoaded',()=>{
		document.querySelectorAll('[data-slider]').forEach(slider=>{
			const track=slider.querySelector('.qs-track');
			const scroller=slider.querySelector('.qs-window');
			const prev=slider.querySelector('.qs-prev');
			const next=slider.querySelector('.qs-next');
			const progress=slider.querySelector('.qs-progress .bar');
			if(!track||!scroller||!prev||!next) return;
			const items=[...track.querySelectorAll('.qs-item')];
			// Eğer toplam item sayısı breakpoint'te normal görünenden azsa alanı doldur
			function applyFewClass(){
				let visibleTarget=5;
				const w=window.innerWidth;
				if(w<1400 && w>=992) visibleTarget=4; else if(w<992 && w>=600) visibleTarget=3; else if(w<600) visibleTarget=2;
				if(items.length<=visibleTarget){slider.classList.add('few-items');} else {slider.classList.remove('few-items');}
			}
			applyFewClass();
			const scrollAmount=()=> scroller.clientWidth * 0.8; // yaklaşık 4 kartlık kaydırma
			function update(){
				prev.disabled = scroller.scrollLeft < 5;
				const maxScroll = scroller.scrollWidth - scroller.clientWidth;
				next.disabled = scroller.scrollLeft >= (maxScroll-5);
				if(progress){
					const ratio = maxScroll>0 ? scroller.scrollLeft / maxScroll : 0;
					progress.style.width = (ratio*100)+"%";
				}
			}
			scroller.addEventListener('scroll',update,{passive:true});
		let isDown=false,startX,startScroll,dragDist=0;
		scroller.addEventListener('pointerdown',e=>{isDown=true;dragDist=0;track.classList.add('dragging');startX=e.clientX;startScroll=scroller.scrollLeft;});
		scroller.addEventListener('pointermove',e=>{if(!isDown)return; const dx=e.clientX-startX; dragDist=Math.max(dragDist, Math.abs(dx)); scroller.scrollLeft=startScroll-dx;});
		scroller.addEventListener('pointerup',()=>{isDown=false;track.classList.remove('dragging');});
		scroller.addEventListener('pointerleave',()=>{isDown=false;track.classList.remove('dragging');});
		scroller.addEventListener('wheel',e=>{ if(Math.abs(e.deltaY)>Math.abs(e.deltaX)){ scroller.scrollLeft+=e.deltaY; e.preventDefault(); } },{passive:false});
			update();
			window.addEventListener('resize',()=>setTimeout(()=>{update();applyFewClass();},150));

			// Click vs drag ayrımı: kart içindeki link tıklaması sürükleme yoksa çalışsın
			scroller.addEventListener('click',function(e){
				// Sürükleme olduysa tıklamayı iptal et; olmadıysa varsayılan anchor navigasyonu anında çalışsın
				if(dragDist>5){ e.preventDefault(); e.stopImmediatePropagation(); }
			});
			// Ok butonları tıklandığında olay yayılmasını engelle
			prev.addEventListener('click',e=>{ e.preventDefault(); e.stopPropagation(); scroller.scrollBy({left:-scrollAmount(),behavior:'smooth'}); });
			next.addEventListener('click',e=>{ e.preventDefault(); e.stopPropagation(); scroller.scrollBy({left:scrollAmount(),behavior:'smooth'}); });
	});
});

// Tag filtering (frontend only)
document.addEventListener('DOMContentLoaded',()=>{
	const bar=document.querySelector('[data-tag-bar]');
	if(!bar) return;
	const chips=[...bar.querySelectorAll('.tag-chip')];
	const allCards=[...document.querySelectorAll('.quiz-card')];
	function apply(tag){
		chips.forEach(c=>c.classList.toggle('active',c.dataset.tag===tag));
		if(tag==='*'){allCards.forEach(c=>c.style.display='');return;}
		allCards.forEach(card=>{
			const tags=(card.getAttribute('data-tags')||'').split(/[\s,]+/);
			card.style.display= tags.includes(tag) ? '' : 'none';
		});
	}
	chips.forEach(ch=> ch.addEventListener('click',()=> apply(ch.dataset.tag)) );
});

// Password show/hide toggles
document.addEventListener('click',e=>{
	const btn=e.target.closest('[data-toggle-pass]');
	if(!btn) return;
	const wrapper=btn.closest('.position-relative');
	const input=wrapper ? wrapper.querySelector('input[data-pass]') : null;
	if(!input) return;
	if(input.type==='password'){
		input.type='text';
		btn.textContent='🙈';
	}else{
		input.type='password';
		btn.textContent='👁';
	}
});
// Avatar reset
document.addEventListener('click',e=>{
	const r=e.target.closest('[data-avatar-reset]');
	if(!r) return;
	const form=r.closest('form');
	if(!form) return;
	const hidden=form.querySelector('input[name=RemoveAvatar]');
	if(hidden){hidden.value='true';}
	const preview=document.getElementById('avatarPreview');
	if(preview){const img=preview.querySelector('img'); if(img) img.src='/img/anon-avatar.svg';}
});
