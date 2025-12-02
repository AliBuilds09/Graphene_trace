using MyProject.Models;
using MyProject.Data;

namespace MyProject.Controllers
{
    public static class AuthController
    {
        static AuthController()
        {
            // Initialize storage and seed admin if none exists
            UsersRepository.Initialize();
            UsersRepository.SeedAdminIfNone();
        }

        public static bool Authenticate(string username, string password, string role, out User? user)
        {
            var ok = UsersRepository.VerifyCredentials(username, role, password, out user);
            if (!ok || user == null) return false;
            if (!user.IsActive) return false;
            Session.CurrentUser = user;
            return true;
        }

        public static bool GetUserById(System.Guid id, out User? user)
        {
            user = UsersRepository.GetUserById(id);
            return user != null;
        }

        public static bool GetUserByUsername(string username, out User? user)
        {
            user = UsersRepository.GetUserByUsername(username);
            return user != null;
        }

        public static bool Register(string username, string password, string desiredRole, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.SelfRegister(username, password, desiredRole);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminCreateUser(string adminUsername, string username, string tempPassword, string role, out string? error)
        {
            error = null;
            try
            {
                if (!UsersRepository.IsAdmin(adminUsername))
                {
                    error = "Forbidden: Only Admin can create users.";
                    return false;
                }
                UsersRepository.CreateUser(adminUsername, username, tempPassword, role);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // AdminEditUserRole already defined above; do not redeclare

        public static bool AdminDeactivateUser(string adminUsername, System.Guid targetUserId, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.DeactivateUser(adminUsername, targetUserId);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminResetPassword(string adminUsername, System.Guid targetUserId, string tempPassword, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.ResetPassword(adminUsername, targetUserId, tempPassword);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminAssignPatientToClinician(string adminUsername, System.Guid patientId, System.Guid clinicianId, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.AssignPatientToClinician(adminUsername, patientId, clinicianId);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Admin approval workflow wrappers
        public static bool AdminGetPendingRegistrations(out System.Collections.Generic.List<User> users, out string? error)
        {
            error = null;
            users = new System.Collections.Generic.List<User>();
            try
            {
                users = UsersRepository.GetPendingRegistrations();
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminApproveRegistration(string adminUsername, System.Guid targetUserId, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.ApproveRegistration(adminUsername, targetUserId);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminRejectRegistration(string adminUsername, System.Guid targetUserId, bool delete, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.RejectRegistration(adminUsername, targetUserId, delete);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AdminEditUserRole(string adminUsername, System.Guid targetUserId, string newRole, out string? error)
        {
            error = null;
            try
            {
                UsersRepository.EditUserRole(adminUsername, targetUserId, newRole);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}