using System;
using MyProject.Models;

namespace MyProject.Models
{
    public static class Session
    {
        public static User? CurrentUser { get; set; }

        public static bool IsAuthenticated => CurrentUser != null && CurrentUser.IsActive;

        public static bool IsAdmin => IsAuthenticated && string.Equals(CurrentUser!.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        public static bool IsClinician => IsAuthenticated && string.Equals(CurrentUser!.Role, "Clinician", StringComparison.OrdinalIgnoreCase);
        public static bool IsPatient => IsAuthenticated && string.Equals(CurrentUser!.Role, "Patient", StringComparison.OrdinalIgnoreCase);
    }
}