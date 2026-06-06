using System.Security.Cryptography;
using System.Text;

namespace App_thi_tin_hoc.Helpers
{
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return HashPassword(password).Equals(hashedPassword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
