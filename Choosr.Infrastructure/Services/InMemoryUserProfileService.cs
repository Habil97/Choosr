using Choosr.Domain.Models;

namespace Choosr.Infrastructure.Services;

public class InMemoryUserProfileService : IUserProfileService
{
    private readonly List<UserProfile> _profiles = new();
    private readonly Dictionary<string, HashSet<Guid>> _playedByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<Guid>> _reactedByUser = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryUserProfileService()
    {
        // Seed a demo user for quick login
        _profiles.Add(new UserProfile{
            UserName = "habbil_cakir",
            DisplayName = "Habil Çakır",
            AvatarUrl = "/img/demo-avatar.png",
            Bio = "Kendiniz hakkında bir şeyler yazın",
            CreatedCount = 0,
            PlayedCount = 0,
            CommentCount = 0,
            ReactionCount = 0
        });
    }

    public IEnumerable<UserProfile> All() => _profiles;
    public UserProfile? Get(Guid id) => _profiles.FirstOrDefault(p=>p.Id==id);
    public UserProfile? GetByUserName(string userName) => _profiles.FirstOrDefault(p=>p.UserName.Equals(userName,StringComparison.OrdinalIgnoreCase));

    public UserProfile Create(UserProfile profile)
    {
        if(GetByUserName(profile.UserName)!=null) throw new InvalidOperationException("Username exists");
        _profiles.Add(profile);return profile;
    }
    public void Update(UserProfile profile)
    {
        var existing = Get(profile.Id);
        if(existing==null) return;
        existing.DisplayName = profile.DisplayName;
        existing.Bio = profile.Bio;
        existing.Twitter = profile.Twitter;
        existing.Instagram = profile.Instagram;
        existing.Youtube = profile.Youtube;
        existing.Twitch = profile.Twitch;
        existing.Kick = profile.Kick;
        existing.AvatarUrl = profile.AvatarUrl;
    }

    public void AddPlayed(string userName, Guid quizId)
    {
        if(!_playedByUser.TryGetValue(userName, out var set)) { set = new HashSet<Guid>(); _playedByUser[userName]=set; }
        if(set.Add(quizId))
        {
            var p = GetByUserName(userName); if(p!=null){ p.PlayedCount = set.Count; }
        }
    }
    public void AddReaction(string userName, Guid quizId)
    {
        if(!_reactedByUser.TryGetValue(userName, out var set)) { set = new HashSet<Guid>(); _reactedByUser[userName]=set; }
        if(set.Add(quizId))
        {
            var p = GetByUserName(userName); if(p!=null){ p.ReactionCount = set.Count; }
        }
    }
    public void RemoveReaction(string userName, Guid quizId)
    {
        if(_reactedByUser.TryGetValue(userName, out var set) && set.Remove(quizId))
        {
            var p = GetByUserName(userName); if(p!=null){ p.ReactionCount = set.Count; }
        }
    }
    public IEnumerable<Guid> GetPlayed(string userName)
    {
        return _playedByUser.TryGetValue(userName, out var set) ? set.ToArray() : Array.Empty<Guid>();
    }
    public IEnumerable<Guid> GetReactions(string userName)
    {
        return _reactedByUser.TryGetValue(userName, out var set) ? set.ToArray() : Array.Empty<Guid>();
    }
}