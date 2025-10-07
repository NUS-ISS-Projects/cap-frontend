using DISTestKit.Model;

namespace DISTestKit.Services
{
    public static class UserSessionCache
    {
        private static UserProfile? _userProfile;
        private static UserSession? _userSession;

        public static void SetUserProfile(UserProfile profile)
        {
            _userProfile = profile;
        }

        public static UserProfile? GetUserProfile()
        {
            return _userProfile;
        }

        public static void SetUserSession(UserSession session)
        {
            _userSession = session;
        }

        public static UserSession? GetUserSession()
        {
            return _userSession;
        }

        public static void Clear()
        {
            _userProfile = null;
            _userSession = null;
        }

        public static bool HasData()
        {
            return _userProfile != null && _userSession != null;
        }
    }
}
