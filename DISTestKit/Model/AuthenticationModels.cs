namespace DISTestKit.Model
{
    public record LoginRequest(string Email, string Password);

    public record RegisterRequest(string Email, string Password);

    public record LoginResponse(string Token);

    public record RegisterResponse(string Message);

    public record AuthenticationResult(
        bool IsSuccess,
        string? Token = null,
        string? ErrorMessage = null
    );
}
