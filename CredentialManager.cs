using System;
using System.Security.Cryptography;
using System.Text;

namespace EBSBrowser
{
    public static class CredentialManager
    {
        public static string EncryptPassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return "";
            try
            {
                byte[] raw = Encoding.UTF8.GetBytes(plainPassword);
                byte[] enc = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch
            {
                return "";
            }
        }

        public static string DecryptPassword(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return "";
            try
            {
                byte[] enc = Convert.FromBase64String(cipherBase64);
                byte[] dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch
            {
                return "";
            }
        }
    }
}
