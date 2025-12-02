using System;
using Xunit;
using MyProject.Data;
using MyProject.Models;

namespace MyProject.Tests;

public class UsersRepositoryTests
{
    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0,8)}";

    [Fact]
    public void SeedAdminIfNone_Creates_Admin_User()
    {
        UsersRepository.Initialize();
        UsersRepository.SeedAdminIfNone();
        Assert.True(UsersRepository.IsAdmin("admin"));
    }

    [Fact]
    public void SelfRegister_Patient_Is_Approved()
    {
        UsersRepository.Initialize();
        var uname = Unique("patient");
        UsersRepository.SelfRegister(uname, "Pass@123!", "Patient");
        var user = UsersRepository.GetUserByUsername(uname);
        Assert.NotNull(user);
        Assert.Equal("Patient", user!.Role);
        Assert.True(user.ApprovedByAdmin);
    }

    [Fact]
    public void SelfRegister_Clinician_Not_Approved_Until_Admin_Approves()
    {
        UsersRepository.Initialize();
        UsersRepository.SeedAdminIfNone();
        var uname = Unique("clinician");
        UsersRepository.SelfRegister(uname, "Strong@123!", "Clinician");

        var user = UsersRepository.GetUserByUsername(uname);
        Assert.NotNull(user);
        Assert.Equal("Clinician", user!.Role);
        Assert.False(user.ApprovedByAdmin);

        // Admin approves
        UsersRepository.ApproveRegistration("admin", user.UserId);
        var after = UsersRepository.GetUserByUsername(uname);
        Assert.NotNull(after);
        Assert.True(after!.ApprovedByAdmin);
    }

    [Fact]
    public void VerifyCredentials_Fails_For_Unapproved_Clinician()
    {
        UsersRepository.Initialize();
        var uname = Unique("clinician_login");
        UsersRepository.SelfRegister(uname, "Strong@123!", "Clinician");
        var ok = UsersRepository.VerifyCredentials(uname, "Clinician", "Strong@123!", out var user);
        Assert.False(ok);
        Assert.Null(user);
    }
}