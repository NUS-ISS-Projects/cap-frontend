namespace DISTestKit.Model
{
    public record UserProfile(string UserId, string Username, string Email);

    public record LastSession(string Date, string View);

    public record UserSession(
        string UserId,
        string UserName,
        string Name,
        LastSession LastSession
    );

    public record SaveUserSessionRequest(
        string UserId,
        string UserName,
        string Name,
        LastSession LastSession
    );

    public record SaveUserSessionResponse(
        string UserId,
        string UserName,
        string Name,
        LastSession LastSession
    );
}
