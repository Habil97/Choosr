// Shared helpers for play modes
(function(global){
  function toYtId(url){
    if(!url) return null; try{ const u=new URL(url); if(u.hostname.includes('youtu.be')) return u.pathname.substring(1); const v=u.searchParams.get('v'); if(v) return v; const parts=u.pathname.split('/'); const i=parts.indexOf('embed'); if(i>=0 && parts[i+1]) return parts[i+1]; return null; }catch{ return null; }
  }
  function toThumb(item){
    if(!item) return '/img/sample1.jpg';
    if(item.image) return item.image;
    const id = toYtId(item.youtube);
    return id ? `https://img.youtube.com/vi/${id}/hqdefault.jpg` : '/img/sample1.jpg';
  }
  function toEmbed(item){ const id = toYtId(item?.youtube); if(!id) return null; const origin = encodeURIComponent(window.location.origin); return `https://www.youtube.com/embed/${id}?controls=1&rel=0&enablejsapi=1&origin=${origin}`; }
  global.PlayCommon = { toYtId, toThumb, toEmbed };
})(window);
