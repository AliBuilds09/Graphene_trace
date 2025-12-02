using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using MyProject.Models;

namespace MyProject.Data
{
    public static class UsersRepository
    {
        private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyProject");
        private static readonly string DbPath = Path.Combine(AppDataDir, "users.db");
        private static readonly string ConnectionString = $"Data Source={Path.GetFullPath(DbPath)}";

        private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS Users (
    user_id              TEXT PRIMARY KEY,
    username             TEXT NOT NULL UNIQUE,
    password_hash        TEXT NOT NULL,
    role                 TEXT NOT NULL CHECK (role IN ('Admin','Clinician','Patient')),
    created_at           TEXT NOT NULL,
    is_active            INTEGER NOT NULL DEFAULT 1,
    created_by_admin_id  TEXT NULL,
    last_password_reset  TEXT NULL,
    must_change_password INTEGER NOT NULL DEFAULT 0,
    approved_by_admin    INTEGER NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users(username);

CREATE TABLE IF NOT EXISTS AdminActions (
    action_id        TEXT PRIMARY KEY,
    admin_id         TEXT NOT NULL,
    target_user_id   TEXT NULL,
    action_type      TEXT NOT NULL,
    details          TEXT NULL,
    created_at       TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ClinicianPatientMap (
    patient_id            TEXT NOT NULL,
    clinician_id          TEXT NOT NULL,
    created_at            TEXT NOT NULL,
    created_by_admin_id   TEXT NOT NULL,
    UNIQUE(patient_id, clinician_id)
);
";

        public static void Initialize()
        {
            Directory.CreateDirectory(AppDataDir);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = SchemaSql;
            cmd.ExecuteNonQuery();

            // Ensure new columns exist when upgrading existing DBs
            using var colCheck = conn.CreateCommand();
            colCheck.CommandText = "PRAGMA table_info(Users)";
            using var reader = colCheck.ExecuteReader();
            bool hasMustChange = false;
            bool hasApproved = false;
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "must_change_password", StringComparison.OrdinalIgnoreCase))
                {
                    hasMustChange = true; break;
                }
                if (string.Equals(name, "approved_by_admin", StringComparison.OrdinalIgnoreCase))
                {
                    hasApproved = true; break;
                }
            }
            reader.Close();
            if (!hasMustChange)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Users ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0";
                try { alter.ExecuteNonQuery(); } catch { /* ignore if not supported */ }
            }
            if (!hasApproved)
            {
                using var alter2 = conn.CreateCommand();
                alter2.CommandText = "ALTER TABLE Users ADD COLUMN approved_by_admin INTEGER NOT NULL DEFAULT 1";
                try { alter2.ExecuteNonQuery(); } catch { /* ignore if not supported */ }
            }

            // Bootstrap: ensure at least one Admin exists
            using var adminCountCmd = conn.CreateCommand();
            adminCountCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE role='Admin'";
            var adminCountObj = adminCountCmd.ExecuteScalar();
            int adminCount = 0;
            if (adminCountObj is long l) adminCount = (int)l;
            else if (adminCountObj is int i) adminCount = i;
            else if (adminCountObj is string s && int.TryParse(s, out var parsed)) adminCount = parsed;
            if (adminCount == 0)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin)
                                         VALUES ($id, $username, $hash, 'Admin', $created, 1, NULL, $resetTs, 1, 1)";
                var now = DateTime.UtcNow.ToString("o");
                insertCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                insertCmd.Parameters.AddWithValue("$username", "admin");
                insertCmd.Parameters.AddWithValue("$hash", HashPassword("Admin@123!"));
                insertCmd.Parameters.AddWithValue("$created", now);
                insertCmd.Parameters.AddWithValue("$resetTs", now);
                try { insertCmd.ExecuteNonQuery(); } catch { /* ignore if insertion fails */ }
            }
        }

        public static void SeedAdminIfNone()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Users WHERE role='Admin'";
                var count = Convert.ToInt32(check.ExecuteScalar());
                if (count > 0) return;
            }

            var adminId = Guid.NewGuid();
            var username = "admin";
            var tempPassword = "Admin@123!"; // default admin password; must be changed after first login
            var hash = HashPassword(tempPassword);
            using var insert = conn.CreateCommand();
            insert.CommandText = @"INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active, must_change_password, approved_by_admin)
                                   VALUES ($id, $username, $hash, 'Admin', $created, 1, 1, 1)";
            insert.Parameters.AddWithValue("$id", adminId.ToString());
            insert.Parameters.AddWithValue("$username", username);
            insert.Parameters.AddWithValue("$hash", hash);
            insert.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
            insert.ExecuteNonQuery();
        }

        public static bool IsAdmin(string username)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT role, is_active FROM Users WHERE username=$username";
            cmd.Parameters.AddWithValue("$username", username);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            var role = reader.GetString(0);
            var active = reader.GetInt32(1) == 1;
            return active && role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public static User? GetUserById(Guid userId)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin FROM Users WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new User
            {
                UserId = Guid.Parse(reader.GetString(0)),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                IsActive = reader.GetInt32(5) == 1,
                CreatedByAdminId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                LastPasswordReset = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                MustChangePassword = reader.IsDBNull(8) ? false : reader.GetInt32(8) == 1,
                ApprovedByAdmin = reader.IsDBNull(9) ? true : reader.GetInt32(9) == 1
            };
        }

        public static User? GetUserByUsername(string username)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin FROM Users WHERE username=$username";
            cmd.Parameters.AddWithValue("$username", username);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new User
            {
                UserId = Guid.Parse(reader.GetString(0)),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                IsActive = reader.GetInt32(5) == 1,
                CreatedByAdminId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                LastPasswordReset = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                MustChangePassword = reader.IsDBNull(8) ? false : reader.GetInt32(8) == 1,
                ApprovedByAdmin = reader.IsDBNull(9) ? true : reader.GetInt32(9) == 1
            };
        }

        public static bool VerifyCredentials(string username, string role, string password, out User? user)
        {
            user = null;
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin FROM Users WHERE username=$username";
            cmd.Parameters.AddWithValue("$username", username);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;

            var dbRole = reader.GetString(3);
            if (!dbRole.Equals(role, StringComparison.OrdinalIgnoreCase)) return false;

            var dbHash = reader.GetString(2);
            var inputHash = HashPassword(password);
            if (!string.Equals(dbHash, inputHash, StringComparison.Ordinal)) return false;

            user = new User
            {
                UserId = Guid.Parse(reader.GetString(0)),
                Username = reader.GetString(1),
                PasswordHash = dbHash,
                Role = dbRole,
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                IsActive = reader.GetInt32(5) == 1,
                CreatedByAdminId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                LastPasswordReset = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                MustChangePassword = reader.GetInt32(8) == 1,
                ApprovedByAdmin = reader.GetInt32(9) == 1
            };
            // Enforce approval for elevated roles at login
            if ((dbRole.Equals("Clinician", StringComparison.OrdinalIgnoreCase) || dbRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                && !user.ApprovedByAdmin)
            {
                user = null; // fail auth
                return false;
            }
            return true;
        }

        public static bool UsernameExists(string username)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM Users WHERE username=$username LIMIT 1";
            cmd.Parameters.AddWithValue("$username", username);
            using var reader = cmd.ExecuteReader();
            return reader.Read();
        }

        public static Guid? GetUserIdByUsername(string username)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id FROM Users WHERE username=$username";
            cmd.Parameters.AddWithValue("$username", username);
            var result = cmd.ExecuteScalar();
            if (result is string s && Guid.TryParse(s, out var g)) return g;
            return null;
        }

        public static void CreateUser(string adminUsername, string username, string tempPassword, string role, Guid? createdByAdminId = null)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can create users.");
            ValidateUsernameOrThrow(username);
            ValidatePasswordOrThrow(tempPassword);
            var normalizedRole = NormalizeRoleOrThrow(role);
            if (UsernameExists(username)) throw new InvalidOperationException("Username already exists");

            var adminId = createdByAdminId ?? GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, must_change_password, approved_by_admin)
                               VALUES ($id, $username, $hash, $role, $created, 1, $createdBy, 1, 1)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$hash", HashPassword(tempPassword));
            cmd.Parameters.AddWithValue("$role", normalizedRole);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$createdBy", adminId?.ToString());
            cmd.ExecuteNonQuery();

            LogAdminAction(adminId, null, "CreateUser", $"username={username}; role={normalizedRole}");
        }

        public static void SelfRegister(string username, string password, string desiredRole)
        {
            ValidateUsernameOrThrow(username);
            ValidatePasswordOrThrow(password);
            var normalizedRole = NormalizeRoleOrThrow(desiredRole);
            if (UsernameExists(username)) throw new InvalidOperationException("Username already exists");

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, must_change_password, approved_by_admin)
                               VALUES ($id, $username, $hash, $role, $created, 1, NULL, 0, $approved)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$hash", HashPassword(password));
            cmd.Parameters.AddWithValue("$role", normalizedRole);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
            var approved = (normalizedRole.Equals("Patient", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            cmd.Parameters.AddWithValue("$approved", approved);
            cmd.ExecuteNonQuery();
        }

        public static void EditUserRole(string adminUsername, Guid targetUserId, string newRole)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can edit users.");
            var normalized = NormalizeRoleOrThrow(newRole);
            var adminId = GetUserIdByUsername(adminUsername);
            if (adminId.HasValue && adminId.Value == targetUserId && !string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("You cannot remove your own Admin role.");
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET role=$role WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$role", normalized);
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, "EditRole", $"newRole={normalized}");
        }

        public static void DeactivateUser(string adminUsername, Guid targetUserId)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can deactivate users.");
            var adminId = GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET is_active=0 WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, "DeactivateUser", null);
        }

        public static void ActivateUser(string adminUsername, Guid targetUserId)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can activate users.");
            var adminId = GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET is_active=1 WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, "ActivateUser", null);
        }

        public static void ResetPassword(string adminUsername, Guid targetUserId, string tempPassword)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can reset passwords.");
            ValidatePasswordOrThrow(tempPassword);
            var adminId = GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET password_hash=$hash, last_password_reset=$ts, must_change_password=1 WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$hash", HashPassword(tempPassword));
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, "ResetPassword", null);
        }

        public static void ForceChangePassword(Guid userId, string newPassword)
        {
            ValidatePasswordOrThrow(newPassword);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET password_hash=$hash, last_password_reset=$ts, must_change_password=0 WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$hash", HashPassword(newPassword));
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
        }

        private static void LogAdminAction(Guid? adminId, Guid? targetUserId, string actionType, string? details)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO AdminActions (action_id, admin_id, target_user_id, action_type, details, created_at)
                                VALUES ($actionId, $adminId, $targetId, $type, $details, $ts)";
            cmd.Parameters.AddWithValue("$actionId", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$adminId", adminId?.ToString() ?? string.Empty);
            cmd.Parameters.AddWithValue("$targetId", targetUserId?.ToString());
            cmd.Parameters.AddWithValue("$type", actionType);
            cmd.Parameters.AddWithValue("$details", details ?? string.Empty);
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void AssignPatientToClinician(string adminUsername, Guid patientId, Guid clinicianId)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can assign relationships.");
            // Validate roles of target users
            var patient = GetUserById(patientId) ?? throw new InvalidOperationException("Patient not found");
            var clinician = GetUserById(clinicianId) ?? throw new InvalidOperationException("Clinician not found");
            if (!string.Equals(patient.Role, "Patient", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Target patientId is not a Patient");
            if (!string.Equals(clinician.Role, "Clinician", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Target clinicianId is not a Clinician");
            var adminId = GetUserIdByUsername(adminUsername) ?? throw new InvalidOperationException("Admin not found");

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO ClinicianPatientMap (patient_id, clinician_id, created_at, created_by_admin_id)
                                VALUES ($p, $c, $ts, $admin)";
            cmd.Parameters.AddWithValue("$p", patientId.ToString());
            cmd.Parameters.AddWithValue("$c", clinicianId.ToString());
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$admin", adminId.ToString());
            cmd.ExecuteNonQuery();

            LogAdminAction(adminId, patientId, "AssignPatient", $"clinicianId={clinicianId}");
        }

        // All users (for Admin User Management)
        public static List<User> GetAllUsers()
        {
            var results = new List<User>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin FROM Users ORDER BY datetime(created_at) DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new User
                {
                    UserId = Guid.Parse(reader.GetString(0)),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Role = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    IsActive = reader.GetInt32(5) == 1,
                    CreatedByAdminId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                    LastPasswordReset = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                    MustChangePassword = reader.GetInt32(8) == 1,
                    ApprovedByAdmin = reader.GetInt32(9) == 1
                });
            }
            return results;
        }

        // Audit logs (AdminActions)
        public static List<MyProject.Models.AdminAction> GetAdminActions(string? adminUsername = null, Guid? targetUserId = null, string? actionType = null, DateTime? start = null, DateTime? end = null)
        {
            var results = new List<MyProject.Models.AdminAction>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(adminUsername))
            {
                var adminId = GetUserIdByUsername(adminUsername);
                if (adminId.HasValue) { where.Add("admin_id=$adminId"); cmd.Parameters.AddWithValue("$adminId", adminId.Value.ToString()); }
            }
            if (targetUserId.HasValue)
            {
                where.Add("target_user_id=$targetId");
                cmd.Parameters.AddWithValue("$targetId", targetUserId.Value.ToString());
            }
            if (!string.IsNullOrWhiteSpace(actionType))
            {
                where.Add("action_type=$type");
                cmd.Parameters.AddWithValue("$type", actionType);
            }
            if (start.HasValue)
            {
                where.Add("datetime(created_at) >= datetime($start)");
                cmd.Parameters.AddWithValue("$start", start.Value.ToString("o"));
            }
            if (end.HasValue)
            {
                where.Add("datetime(created_at) <= datetime($end)");
                cmd.Parameters.AddWithValue("$end", end.Value.ToString("o"));
            }
            var whereClause = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            cmd.CommandText = "SELECT action_id, admin_id, target_user_id, action_type, details, created_at FROM AdminActions" + whereClause + " ORDER BY datetime(created_at) DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new MyProject.Models.AdminAction
                {
                    ActionId = Guid.Parse(reader.GetString(0)),
                    AdminId = string.IsNullOrWhiteSpace(reader.GetString(1)) ? (Guid?)null : Guid.Parse(reader.GetString(1)),
                    TargetUserId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                    ActionType = reader.GetString(3),
                    Details = reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5))
                });
            }
            return results;
        }

        // Pending registrations (Admin approval workflow)
        public static List<User> GetPendingRegistrations()
        {
            var results = new List<User>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, username, password_hash, role, created_at, is_active, created_by_admin_id, last_password_reset, must_change_password, approved_by_admin FROM Users WHERE approved_by_admin=0 AND role IN ('Clinician','Admin')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new User
                {
                    UserId = Guid.Parse(reader.GetString(0)),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Role = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    IsActive = reader.GetInt32(5) == 1,
                    CreatedByAdminId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                    LastPasswordReset = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                    MustChangePassword = reader.GetInt32(8) == 1,
                    ApprovedByAdmin = reader.GetInt32(9) == 1
                });
            }
            return results;
        }

        public static void ApproveRegistration(string adminUsername, Guid targetUserId)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can approve users.");
            var adminId = GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET approved_by_admin=1, is_active=1 WHERE user_id=$id";
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, "ApproveRegistration", null);
        }

        public static void RejectRegistration(string adminUsername, Guid targetUserId, bool delete = false)
        {
            if (!IsAdmin(adminUsername)) throw new UnauthorizedAccessException("Only Admin can reject users.");
            var adminId = GetUserIdByUsername(adminUsername);
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            if (delete)
            {
                cmd.CommandText = "DELETE FROM Users WHERE user_id=$id";
            }
            else
            {
                cmd.CommandText = "UPDATE Users SET is_active=0, approved_by_admin=0 WHERE user_id=$id";
            }
            cmd.Parameters.AddWithValue("$id", targetUserId.ToString());
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("User not found");
            LogAdminAction(adminId, targetUserId, delete ? "DeleteUser" : "RejectRegistration", null);
        }

        public static Guid[] GetAssignedPatientsForClinician(Guid clinicianId)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT patient_id FROM ClinicianPatientMap WHERE clinician_id=$c";
            cmd.Parameters.AddWithValue("$c", clinicianId.ToString());
            using var reader = cmd.ExecuteReader();
            var list = new System.Collections.Generic.List<Guid>();
            while (reader.Read())
            {
                var pid = reader.GetString(0);
                if (Guid.TryParse(pid, out var g)) list.Add(g);
            }
            return list.ToArray();
        }

        private static string HashPassword(string plainText)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private static void ValidateUsernameOrThrow(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username required");
            var trimmed = username.Trim();
            if (trimmed.Length < 3 || trimmed.Length > 64) throw new ArgumentException("Username must be 3-64 characters");
            if (!Regex.IsMatch(trimmed, "^[A-Za-z0-9_.-]+$")) throw new ArgumentException("Username can only contain letters, digits, '.', '_' or '-' ");
        }

        private static void ValidatePasswordOrThrow(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8) throw new ArgumentException("Password must be at least 8 characters");
            bool hasSpecial = false;
            foreach (var ch in password)
            {
                if (!char.IsLetterOrDigit(ch)) { hasSpecial = true; break; }
            }
            if (!hasSpecial) throw new ArgumentException("Password must include at least one special character");
        }

        private static string NormalizeRoleOrThrow(string role)
        {
            var allowed = User.AllowedRoles;
            foreach (var r in allowed)
            {
                if (r.Equals(role, StringComparison.OrdinalIgnoreCase)) return r;
            }
            throw new ArgumentException("Invalid role. Allowed: Admin, Clinician, Patient");
        }
    }
}