using Choosr.Web.ViewModels;
using Choosr.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Choosr.Web.Services;

public interface IDraftService
{
    DraftViewModel Upsert(string userName, DraftViewModel draft);
    DraftViewModel? Get(string userName, Guid id);
    IEnumerable<DraftViewModel> List(string userName);
    bool Delete(string userName, Guid id);
    IEnumerable<(Guid id, DateTime createdAt, string title)> ListRevisions(string userName, Guid draftId, int take = 50);
    Choosr.Domain.Entities.DraftRevision? GetRevision(string userName, Guid draftId, Guid revisionId);
}

public class InMemoryDraftService : IDraftService
{
    private readonly Dictionary<string, Dictionary<Guid, DraftViewModel>> _store = new(StringComparer.OrdinalIgnoreCase);

    public DraftViewModel Upsert(string userName, DraftViewModel draft)
    {
        if(!_store.TryGetValue(userName, out var bucket))
        {
            bucket = new Dictionary<Guid, DraftViewModel>();
            _store[userName] = bucket;
        }
        draft.UpdatedAt = DateTime.UtcNow;
        bucket[draft.Id] = draft;
        return draft;
    }

    public DraftViewModel? Get(string userName, Guid id)
    {
        return _store.TryGetValue(userName, out var bucket) && bucket.TryGetValue(id, out var d) ? d : null;
    }

    public IEnumerable<DraftViewModel> List(string userName)
    {
        return _store.TryGetValue(userName, out var bucket) ? bucket.Values.OrderByDescending(x=>x.UpdatedAt).ToList() : Enumerable.Empty<DraftViewModel>();
    }

    public bool Delete(string userName, Guid id)
    {
        return _store.TryGetValue(userName, out var bucket) && bucket.Remove(id);
    }

    public IEnumerable<(Guid id, DateTime createdAt, string title)> ListRevisions(string userName, Guid draftId, int take = 50)
    {
        if(_store.TryGetValue(userName, out var bucket) && bucket.TryGetValue(draftId, out var d))
            return new [] { (Guid.NewGuid(), d.UpdatedAt, d.Title ?? string.Empty) };
        return Enumerable.Empty<(Guid, DateTime, string)>();
    }

    public Choosr.Domain.Entities.DraftRevision? GetRevision(string userName, Guid draftId, Guid revisionId) => null;
}

// EF-backed persistent drafts
public class EfDraftService : IDraftService
{
    private readonly AppDbContext _db;
    public EfDraftService(AppDbContext db){ _db = db; }
    public DraftViewModel Upsert(string userName, DraftViewModel draft)
    {
        var id = draft.Id == Guid.Empty ? Guid.NewGuid() : draft.Id;
        var entity = _db.Drafts.Include(d=>d.Choices).FirstOrDefault(d=>d.Id==id && d.UserName==userName);
        if(entity==null)
        {
            entity = new Choosr.Domain.Entities.Draft{ Id = id, UserName = userName };
            _db.Drafts.Add(entity);
        }
        entity.Title = draft.Title ?? string.Empty;
        entity.Description = draft.Description ?? string.Empty;
        entity.Category = string.IsNullOrWhiteSpace(draft.Category)? "Genel" : draft.Category!;
        entity.Visibility = string.IsNullOrWhiteSpace(draft.Visibility)? "public" : draft.Visibility!;
        entity.IsAnonymous = draft.IsAnonymous;
        entity.Tags = draft.Tags ?? Array.Empty<string>();
        entity.CoverImageUrl = draft.CoverImageUrl;
        entity.CoverImageWidth = draft.CoverImageWidth;
        entity.CoverImageHeight = draft.CoverImageHeight;
        entity.UpdatedAt = DateTime.UtcNow;
        // Replace choices only if provided; avoid wiping existing choices with an accidental empty autosave
        if(draft.Choices != null)
        {
            // Robust replace to avoid concurrency exceptions when overlapping autosaves occur
            try
            {
                // Prefer server-side bulk delete (EF Core 7+)
                _db.DraftChoices.Where(x => x.DraftId == id).ExecuteDelete();
            }
            catch
            {
                // Fallback: tracked removal from a fresh query
                var olds = _db.DraftChoices.Where(x => x.DraftId == id).ToList();
                if(olds.Count > 0) _db.DraftChoices.RemoveRange(olds);
            }

            var newChoices = draft.Choices
                .Select((c,i)=> new Choosr.Domain.Entities.DraftChoice{
                    Id = c.Id==Guid.Empty? Guid.NewGuid() : c.Id,
                    DraftId = id,
                    ImageUrl = c.ImageUrl,
                    ImageWidth = c.ImageWidth,
                    ImageHeight = c.ImageHeight,
                    YoutubeUrl = c.YoutubeUrl,
                    Caption = c.Caption,
                    Order = c.Order>0? c.Order : i+1
                }).ToList();
            if(newChoices.Count > 0) _db.DraftChoices.AddRange(newChoices);
        }
        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            // One-time retry: re-apply choices with fresh state
            _db.ChangeTracker.Clear();
            var entity2 = _db.Drafts.Include(d=>d.Choices).FirstOrDefault(d=>d.Id==id && d.UserName==userName) ?? new Choosr.Domain.Entities.Draft{ Id=id, UserName=userName };
            if(entity2.Id == id && entity2.UserName==userName && _db.Entry(entity2).State==EntityState.Detached){ _db.Drafts.Attach(entity2); }
            if(draft.Choices != null)
            {
                try{ _db.DraftChoices.Where(x=>x.DraftId==id).ExecuteDelete(); } catch { var olds2=_db.DraftChoices.Where(x=>x.DraftId==id).ToList(); if(olds2.Count>0)_db.DraftChoices.RemoveRange(olds2);}            
                var newChoices2 = draft.Choices.Select((c,i)=> new Choosr.Domain.Entities.DraftChoice{
                    Id = c.Id==Guid.Empty? Guid.NewGuid() : c.Id,
                    DraftId = id,
                    ImageUrl = c.ImageUrl,
                    ImageWidth = c.ImageWidth,
                    ImageHeight = c.ImageHeight,
                    YoutubeUrl = c.YoutubeUrl,
                    Caption = c.Caption,
                    Order = c.Order>0? c.Order : i+1
                }).ToList();
                if(newChoices2.Count>0) _db.DraftChoices.AddRange(newChoices2);
            }
            _db.SaveChanges();
        }

        // After successful save, create a revision snapshot (best-effort)
        try
        {
            var snapshotChoices = _db.DraftChoices.AsNoTracking().Where(x=>x.DraftId==id).OrderBy(x=>x.Order).Select(x=> new {
                x.ImageUrl, x.ImageWidth, x.ImageHeight, x.YoutubeUrl, x.Caption, x.Order
            }).ToList();
            var choicesJson = JsonSerializer.Serialize(snapshotChoices);
            var rev = new Choosr.Domain.Entities.DraftRevision{
                Id = Guid.NewGuid(),
                DraftId = id,
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Title = entity.Title,
                Description = entity.Description,
                Category = entity.Category,
                Visibility = entity.Visibility,
                IsAnonymous = entity.IsAnonymous,
                Tags = entity.Tags ?? Array.Empty<string>(),
                CoverImageUrl = entity.CoverImageUrl,
                CoverImageWidth = entity.CoverImageWidth,
                CoverImageHeight = entity.CoverImageHeight,
                ChoicesJson = choicesJson
            };
            _db.DraftRevisions.Add(rev);
            _db.SaveChanges();
        }
        catch { }
        draft.Id = id; draft.UpdatedAt = entity.UpdatedAt;
        return draft;
    }
    public DraftViewModel? Get(string userName, Guid id)
    {
        var d = _db.Drafts.AsNoTracking().Include(x=>x.Choices).FirstOrDefault(x=>x.Id==id && x.UserName==userName);
        if(d==null) return null;
        return new DraftViewModel{
            Id = d.Id,
            Title = d.Title,
            Description = d.Description,
            Category = d.Category,
            Visibility = d.Visibility,
            IsAnonymous = d.IsAnonymous,
            Tags = d.Tags ?? Array.Empty<string>(),
            CoverImageUrl = d.CoverImageUrl,
            CoverImageWidth = d.CoverImageWidth,
            CoverImageHeight = d.CoverImageHeight,
            UpdatedAt = d.UpdatedAt,
            Choices = d.Choices.OrderBy(c=>c.Order).Select(c=> new DraftChoiceViewModel{
                Id = c.Id,
                ImageUrl = c.ImageUrl,
                ImageWidth = c.ImageWidth,
                ImageHeight = c.ImageHeight,
                YoutubeUrl = c.YoutubeUrl,
                Caption = c.Caption,
                Order = c.Order
            }).ToList()
        };
    }
    public IEnumerable<DraftViewModel> List(string userName)
    {
        var items = _db.Drafts.AsNoTracking().Include(d=>d.Choices).Where(d=>d.UserName==userName).OrderByDescending(d=>d.UpdatedAt).ToList();
        return items.Select(d=> new DraftViewModel{
            Id = d.Id,
            Title = d.Title,
            Description = d.Description,
            Category = d.Category,
            Visibility = d.Visibility,
            IsAnonymous = d.IsAnonymous,
            Tags = d.Tags ?? Array.Empty<string>(),
            CoverImageUrl = d.CoverImageUrl,
            CoverImageWidth = d.CoverImageWidth,
            CoverImageHeight = d.CoverImageHeight,
            UpdatedAt = d.UpdatedAt,
            Choices = d.Choices.OrderBy(c=>c.Order).Select(c=> new DraftChoiceViewModel{ Id=c.Id, ImageUrl=c.ImageUrl, ImageWidth=c.ImageWidth, ImageHeight=c.ImageHeight, YoutubeUrl=c.YoutubeUrl, Caption=c.Caption, Order=c.Order }).ToList()
        }).ToList();
    }
    public bool Delete(string userName, Guid id)
    {
        var d = _db.Drafts.Include(x=>x.Choices).FirstOrDefault(x=>x.Id==id && x.UserName==userName);
        if(d==null) return false;
        _db.Drafts.Remove(d);
        _db.SaveChanges();
        return true;
    }

    public IEnumerable<(Guid id, DateTime createdAt, string title)> ListRevisions(string userName, Guid draftId, int take = 50)
    {
        return _db.DraftRevisions.AsNoTracking()
            .Where(r=>r.DraftId==draftId && r.UserName==userName)
            .OrderByDescending(r=>r.CreatedAt)
            .Take(take)
            .Select(r=> new ValueTuple<Guid, DateTime, string>(r.Id, r.CreatedAt, r.Title))
            .ToList();
    }

    public Choosr.Domain.Entities.DraftRevision? GetRevision(string userName, Guid draftId, Guid revisionId)
    {
        return _db.DraftRevisions.AsNoTracking().FirstOrDefault(r=>r.Id==revisionId && r.DraftId==draftId && r.UserName==userName);
    }
}

public interface ITagSuggestService
{
    IEnumerable<string> Suggest(IEnumerable<string> corpus, string title, string? description, int take = 5);
}

public interface ITagSelectionService
{
    void Increment(IEnumerable<string> tags);
    IReadOnlyDictionary<string,int> GetAllCounts();
}

public class EfTagSelectionService : ITagSelectionService
{
    private readonly Choosr.Infrastructure.Data.AppDbContext _db;
    public EfTagSelectionService(Choosr.Infrastructure.Data.AppDbContext db){ _db=db; }
    public void Increment(IEnumerable<string> tags)
    {
        var list = tags.Where(t=>!string.IsNullOrWhiteSpace(t)).Select(t=>t.Trim().ToLowerInvariant()).Distinct().ToList();
        if(list.Count==0) return;
        var now = DateTime.UtcNow;
        foreach(var tag in list)
        {
            var row = _db.TagSelectionStats.FirstOrDefault(x=>x.Tag==tag);
            if(row==null){ row = new Choosr.Domain.Entities.TagSelectionStat{ Tag = tag, Count = 1, LastSelectedAt = now }; _db.TagSelectionStats.Add(row); }
            else { row.Count += 1; row.LastSelectedAt = now; }
        }
        _db.SaveChanges();
    }

    public IReadOnlyDictionary<string, int> GetAllCounts()
    {
        return _db.TagSelectionStats.AsNoTracking().ToDictionary(x=>x.Tag, x=>x.Count);
    }
}

public class SimpleTfIdfTagSuggestService : ITagSuggestService
{
    public IEnumerable<string> Suggest(IEnumerable<string> corpus, string title, string? description, int take = 5)
    {
        var text = (title + " " + (description ?? "")).ToLowerInvariant();
        var tokens = text.Split(new[]{' ', '\n', '\r', '\t', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '/', '\\', '-'}, StringSplitOptions.RemoveEmptyEntries);
        var tokenSet = new HashSet<string>(tokens);
        var scores = new List<(string tag, int score)>();
        foreach(var tag in corpus)
        {
            var t = tag.ToLowerInvariant();
            // simple similarity: exact occurrence + substring match
            int score = 0;
            if(tokenSet.Contains(t)) score += 3;
            if(text.Contains(t, StringComparison.OrdinalIgnoreCase)) score += 1;
            if(score>0) scores.Add((tag, score));
        }
        return scores
            .OrderByDescending(x=>x.score)
            .ThenBy(x=>x.tag)
            .Select(x=>x.tag)
            .Distinct()
            .Take(Math.Max(1, take))
            .ToList();
    }
}

public class BlendedTagSuggestService : ITagSuggestService
{
    private readonly ITagSelectionService _sel;
    private readonly SimpleTfIdfTagSuggestService _baseline = new();
    public BlendedTagSuggestService(ITagSelectionService sel){ _sel=sel; }
    public IEnumerable<string> Suggest(IEnumerable<string> corpus, string title, string? description, int take = 5)
    {
        var baseline = _baseline.Suggest(corpus, title, description, take: int.MaxValue).ToList();
        if(baseline.Count==0){ return Array.Empty<string>(); }
        var counts = _sel.GetAllCounts();
        double maxC = counts.Count>0? counts.Max(kv=> (double)kv.Value) : 0d;
        // Blend score = rank-based baseline + normalized frequency
        var rank = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<baseline.Count;i++){ rank[baseline[i]] = 1.0 / (i+1); }
        var blended = baseline
            .Select(tag=>{
                var key = tag.Trim().ToLowerInvariant();
                var r = rank[tag];
                var f = (maxC>0 && counts.TryGetValue(key, out var c)) ? (c / maxC) : 0d;
                var score = 0.7*r + 0.3*f;
                return (tag, score);
            })
            .OrderByDescending(x=>x.score)
            .ThenBy(x=>x.tag, StringComparer.OrdinalIgnoreCase)
            .Select(x=>x.tag)
            .Distinct()
            .Take(Math.Max(1, take))
            .ToList();
        return blended;
    }
}
