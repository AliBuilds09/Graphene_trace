using System;
using System.IO;
using Xunit;
using Microsoft.Data.Sqlite;
using MyProject.Data;
using MyProject.Models;

namespace MyProject.Tests;

public class DatabaseTests
{
    private static string DbFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "MyProject");
        return Path.Combine(dir, "users.db");
    }

    [Fact]
    public void DB_D01_RoleConstraint_Rejects_Invalid_Role()
    {
        // Ensure schema exists
        UsersRepository.Initialize();

        // Try inserting an invalid role (not Admin|Clinician|Patient)
        var connString = $"Data Source={Path.GetFullPath(DbFilePath())}";
        using var conn = new SqliteConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active)
                            VALUES ($id, $username, $hash, 'Guest', $created, 1)";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$username", $"guest_{Guid.NewGuid():N}");
        cmd.Parameters.AddWithValue("$hash", Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes("Pass@123!"))));
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));

        // Expect CHECK constraint failure
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public void DB_D02_AdminApproval_Toggles_ApprovedByAdmin_And_Logs()
    {
        UsersRepository.Initialize();
        UsersRepository.SeedAdminIfNone();

        // Self-register a clinician (not approved by default)
        var uname = $"clin_approve_{Guid.NewGuid():N}";
        UsersRepository.SelfRegister(uname, "Strong@123!", "Clinician");
        var before = UsersRepository.GetUserByUsername(uname);
        Assert.NotNull(before);
        Assert.Equal("Clinician", before!.Role);
        Assert.False(before.ApprovedByAdmin);

        // Approve as admin
        UsersRepository.ApproveRegistration("admin", before.UserId);
        var after = UsersRepository.GetUserByUsername(uname);
        Assert.NotNull(after);
        Assert.True(after!.ApprovedByAdmin);

        // Optional: verify an admin action was logged
        var connString = $"Data Source={Path.GetFullPath(DbFilePath())}";
        using var conn = new SqliteConnection(connString);
        conn.Open();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM AdminActions WHERE action_type='ApproveRegistration' AND target_user_id=$id";
        check.Parameters.AddWithValue("$id", before.UserId.ToString());
        var countObj = check.ExecuteScalar();
        var count = (countObj is long l) ? (int)l : (countObj is int i ? i : 0);
        Assert.True(count >= 1);
    }
}