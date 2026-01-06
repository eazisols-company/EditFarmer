using System;
using System.Linq;

namespace CarrotDownload.Auth.Helpers
{
    public static class PasswordValidator
    {
        public static (bool IsValid, string Message) Validate(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "❌ Password cannot be empty");

            if (password.Length < 6)
                return (false, "❌ Password must be at least 6 characters");

            if (!password.Any(char.IsUpper))
                return (false, "❌ Password must contain at least 1 uppercase letter");

            if (!password.Any(char.IsLower))
                return (false, "❌ Password must contain at least 1 lowercase letter");

            if (!password.Any(char.IsDigit))
                return (false, "❌ Password must contain at least 1 number");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                return (false, "❌ Password must contain at least 1 special character");

            return (true, "✓ Password meets all requirements");
        }
    }
}
