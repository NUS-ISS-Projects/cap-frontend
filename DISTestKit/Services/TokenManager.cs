using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DISTestKit.Services
{
    public class TokenManager
    {
        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DISTestKit",
            "auth_token.dat"
        );

        private static string? _currentToken;

        public static void SetToken(string token)
        {
            _currentToken = token;
            SaveTokenToFile(token);
        }

        public static string? GetToken()
        {
            if (_currentToken != null)
                return _currentToken;

            return LoadTokenFromFile();
        }

        public static void ClearToken()
        {
            _currentToken = null;
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }

        public static bool HasValidToken()
        {
            var token = GetToken();
            return !string.IsNullOrEmpty(token);
        }

        public static string GetAuthorizationHeader()
        {
            var token = GetToken();
            return string.IsNullOrEmpty(token) ? string.Empty : $"Bearer {token}";
        }

        private static void SaveTokenToFile(string token)
        {
            try
            {
                var directory = Path.GetDirectoryName(TokenFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var encryptedToken = ProtectData(token);
                File.WriteAllBytes(TokenFilePath, encryptedToken);
            }
            catch (Exception)
            {
                // Silently fail to prevent breaking the app
            }
        }

        private static string? LoadTokenFromFile()
        {
            try
            {
                if (!File.Exists(TokenFilePath))
                    return null;

                var encryptedToken = File.ReadAllBytes(TokenFilePath);
                return UnprotectData(encryptedToken);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] ProtectData(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        }

        private static string UnprotectData(byte[] data)
        {
            var bytes = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
