using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MyProject.Controllers;

namespace MyProject.Views
{
    public partial class CreateUserWindow : Window
    {
        private readonly string _adminUsername;

        public CreateUserWindow(string adminUsername)
        {
            _adminUsername = adminUsername;
            InitializeComponent();

            GeneratePasswordButton.Click += (s, e) =>
            {
                PasswordInput.Password = GenerateTempPassword();
            };

            CancelButton.Click += (s, e) => Close();

            CreateButton.Click += OnCreateClicked;
        }

        private void OnCreateClicked(object? sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;

            var username = UsernameInput.Text.Trim();
            var password = PasswordInput.Password;
            var roleItem = RoleCombo.SelectedItem as ComboBoxItem;
            var role = roleItem?.Content as string ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                ValidationText.Text = "Username is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                ValidationText.Text = "Password must be at least 8 characters.";
                return;
            }
            bool hasSpecial = false;
            foreach (var ch in password)
            {
                if (!char.IsLetterOrDigit(ch)) { hasSpecial = true; break; }
            }
            if (!hasSpecial)
            {
                ValidationText.Text = "Password must include at least one special character.";
                return;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9_.-]+$"))
            {
                ValidationText.Text = "Username can only contain letters, digits, '.', '_' or '-' .";
                return;
            }
            if (string.IsNullOrWhiteSpace(role))
            {
                ValidationText.Text = "Select a role.";
                return;
            }

            if (AuthController.AdminCreateUser(_adminUsername, username, password, role, out var error))
            {
                MessageBox.Show($"User '{username}' created as {role}.");
                Close();
            }
            else
            {
                ValidationText.Text = error ?? "Unknown error.";
            }
        }

        private static string GenerateTempPassword()
        {
            // Simple generator: 12 chars mix
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@#$%";
            var rng = RandomNumberGenerator.Create();
            var sb = new StringBuilder();
            var buf = new byte[1];
            for (int i = 0; i < 12; i++)
            {
                rng.GetBytes(buf);
                var idx = buf[0] % chars.Length;
                sb.Append(chars[idx]);
            }
            return sb.ToString();
        }
    }
}