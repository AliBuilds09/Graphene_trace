using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MyProject.Models
{
    public class User
    {
        // Primary key
        public Guid UserId { get; set; } = Guid.NewGuid();

        // Unique username
        public string Username { get; set; } = string.Empty;

        // Legacy plaintext password (kept for backward compatibility). Prefer PasswordHash.
        public string Password { get; set; } = string.Empty;

        // Secure password hash
        public string PasswordHash { get; set; } = string.Empty;

        // Allowed roles: Admin | Clinician | Patient
        public static readonly string[] AllowedRoles = new[] { "Admin", "Clinician", "Patient" };

        private string _role = "Patient";
        public string Role
        {
            get => _role;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Role cannot be empty.");
                var normalized = value.Trim();
                if (!AllowedRoles.Any(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Invalid role. Allowed: Admin, Clinician, Patient");
                // Store the canonical casing
                _role = AllowedRoles.First(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Timestamps and status
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public Guid? CreatedByAdminId { get; set; }
        public DateTime? LastPasswordReset { get; set; }
        public bool MustChangePassword { get; set; } = false;
        public bool ApprovedByAdmin { get; set; } = true;

        // Helper to set password hash from plaintext (SHA256)
        public void SetPassword(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var hash = sha.ComputeHash(bytes);
            PasswordHash = Convert.ToHexString(hash);
        }
    }
}