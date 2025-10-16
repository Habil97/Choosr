using System.Text.Json;
using Choosr.Domain.Models;

namespace Choosr.Infrastructure.Services;

public class FileUserProfileService : IUserProfileService
{
    private readonly string _filePath;
    private readonly object _sync = new();
    private readonly Dictionary<string, HashSet<Guid>> _playedByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<Guid>> _reactedByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UserProfile> _profiles = new();

    private class PersistState
    {
        public List<UserProfile> Profiles { get; set; } = new();
        public Dictionary<string, List<Guid>> PlayedByUser { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<Guid>> ReactedByUser { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public FileUserProfileService(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<PersistState>(json);
            if (state == null) return;

            _profiles.Clear();
            _profiles.AddRange(state.Profiles ?? []);

            _playedByUser.Clear();
            if (state.PlayedByUser != null)
            {
                foreach (var kvp in state.PlayedByUser)
                {
                    _playedByUser[kvp.Key] = new HashSet<Guid>(kvp.Value ?? []);
                }
            }

            _reactedByUser.Clear();
            if (state.ReactedByUser != null)
            {
                foreach (var kvp in state.ReactedByUser)
                {
                    _reactedByUser[kvp.Key] = new HashSet<Guid>(kvp.Value ?? []);
                }
            }
        }
        catch
        {
            // ignore load failures; start fresh
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var state = new PersistState
            {
                Profiles = _profiles.ToList(),
                PlayedByUser = _playedByUser.ToDictionary(k => k.Key, v => v.Value.ToList(), StringComparer.OrdinalIgnoreCase),
                ReactedByUser = _reactedByUser.ToDictionary(k => k.Key, v => v.Value.ToList(), StringComparer.OrdinalIgnoreCase)
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_filePath)) File.Delete(_filePath);
            File.Move(tmp, _filePath);
        }
        catch
        {
            // swallow persistence errors to avoid taking down the app
        }
    }

    public IEnumerable<UserProfile> All()
    {
        lock (_sync) { return _profiles.ToList(); }
    }

    public UserProfile? Get(Guid id)
    {
        lock (_sync) { return _profiles.FirstOrDefault(p => p.Id == id); }
    }

    public UserProfile? GetByUserName(string userName)
    {
        lock (_sync) { return _profiles.FirstOrDefault(p => p.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)); }
    }

    public UserProfile Create(UserProfile profile)
    {
        lock (_sync)
        {
            if (_profiles.Any(p => p.UserName.Equals(profile.UserName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Username exists");
            _profiles.Add(profile);
            Save();
            return profile;
        }
    }

    public void Update(UserProfile profile)
    {
        lock (_sync)
        {
            var existing = _profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existing == null) return;
            existing.DisplayName = profile.DisplayName;
            existing.Bio = profile.Bio;
            existing.Twitter = profile.Twitter;
            existing.Instagram = profile.Instagram;
            existing.Youtube = profile.Youtube;
            existing.Twitch = profile.Twitch;
            existing.Kick = profile.Kick;
            existing.AvatarUrl = profile.AvatarUrl;
            Save();
        }
    }

    public void AddPlayed(string userName, Guid quizId)
    {
        lock (_sync)
        {
            if (!_playedByUser.TryGetValue(userName, out var set))
            {
                set = new HashSet<Guid>();
                _playedByUser[userName] = set;
            }
            if (set.Add(quizId))
            {
                var p = _profiles.FirstOrDefault(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                if (p == null)
                {
                    p = new UserProfile { UserName = userName, DisplayName = userName, AvatarUrl = "/img/demo-avatar.png" };
                    _profiles.Add(p);
                }
                if (p != null) p.PlayedCount = set.Count;
                Save();
            }
        }
    }

    public void AddReaction(string userName, Guid quizId)
    {
        lock (_sync)
        {
            if (!_reactedByUser.TryGetValue(userName, out var set))
            {
                set = new HashSet<Guid>();
                _reactedByUser[userName] = set;
            }
            if (set.Add(quizId))
            {
                var p = _profiles.FirstOrDefault(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                if (p == null)
                {
                    p = new UserProfile { UserName = userName, DisplayName = userName, AvatarUrl = "/img/demo-avatar.png" };
                    _profiles.Add(p);
                }
                if (p != null) p.ReactionCount = set.Count;
                Save();
            }
        }
    }

    public void RemoveReaction(string userName, Guid quizId)
    {
        lock (_sync)
        {
            if (_reactedByUser.TryGetValue(userName, out var set) && set.Remove(quizId))
            {
                var p = _profiles.FirstOrDefault(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                if (p != null) p.ReactionCount = set.Count;
                Save();
            }
        }
    }

    public IEnumerable<Guid> GetPlayed(string userName)
    {
        lock (_sync)
        {
            return _playedByUser.TryGetValue(userName, out var set) ? set.ToArray() : Array.Empty<Guid>();
        }
    }

    public IEnumerable<Guid> GetReactions(string userName)
    {
        lock (_sync)
        {
            return _reactedByUser.TryGetValue(userName, out var set) ? set.ToArray() : Array.Empty<Guid>();
        }
    }
}
