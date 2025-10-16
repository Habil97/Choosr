using Choosr.Domain.Models;

namespace Choosr.Infrastructure.Services;

public interface IUserProfileService
{
    UserProfile? GetByUserName(string userName);
    UserProfile? Get(Guid id);
    IEnumerable<UserProfile> All();
    UserProfile Create(UserProfile profile);
    void Update(UserProfile profile);
    // New: activity tracking
    void AddPlayed(string userName, Guid quizId);
    void AddReaction(string userName, Guid quizId);
    void RemoveReaction(string userName, Guid quizId);
    IEnumerable<Guid> GetPlayed(string userName);
    IEnumerable<Guid> GetReactions(string userName);
}