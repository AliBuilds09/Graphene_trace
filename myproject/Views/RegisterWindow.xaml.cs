using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyProject.Controllers;

namespace MyProject.Views
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();

            RoleCombo.SelectionChanged += (s, e) =>
            {
                var item = RoleCombo.SelectedItem as ComboBoxItem;
                var role = item?.Content as string ?? "Patient";
                RoleWarning.Visibility = (role == "Patient") ? Visibility.Collapsed : Visibility.Visible;
            };

            CancelButton.Click += (s, e) => Close();
            RegisterButton.Click += OnRegisterClicked;

            PasswordInput.PasswordChanged += (s, e) =>
            {
                UpdateStrengthMeter(PasswordInput.Password);
            };
        }

        private void OnRegisterClicked(object? sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            var username = UsernameInput.Text.Trim();
            var password = PasswordInput.Password;
            var confirm = ConfirmPasswordInput.Password;
            var roleItem = RoleCombo.SelectedItem as ComboBoxItem;
            var role = roleItem?.Content as string ?? "Patient";

            if (string.IsNullOrWhiteSpace(username))
            {
                ValidationText.Text = "Username is required.";
                return;
            }
            if (!Regex.IsMatch(username, "^[A-Za-z0-9_.-]+$"))
            {
                ValidationText.Text = "Username can only contain letters, digits, '.', '_' or '-' .";
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
            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                ValidationText.Text = "Passwords do not match.";
                return;
            }

            if (AuthController.Register(username, password, role, out var error))
            {
                var msg = (role == "Patient")
                    ? $"Registration successful. You can now login as {role}."
                    : $"Registration submitted as {role}. Admin approval required before you can login as {role}.";
                MessageBox.Show(msg);
                Close();
            }
            else
            {
                ValidationText.Text = error ?? "Unknown error.";
            }
        }

        private void UpdateStrengthMeter(string password)
        {
            int score = 0;
            string label = "Weak";

            if (string.IsNullOrEmpty(password))
            {
                StrengthBar.Value = 0;
                StrengthLabel.Text = "Password strength: ";
                StrengthLabel.Foreground = Brushes.Gray;
                return;
            }

            // Length
            if (password.Length >= 8) score += 30;
            if (password.Length >= 12) score += 10;

            bool hasLower = false, hasUpper = false, hasDigit = false, hasSpecial = false;
            foreach (var ch in password)
            {
                if (char.IsLower(ch)) hasLower = true;
                else if (char.IsUpper(ch)) hasUpper = true;
                else if (char.IsDigit(ch)) hasDigit = true;
                else hasSpecial = true;
            }

            int kinds = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);
            score += kinds * 15; // up to +60

            score = Math.Min(score, 100);

            if (score < 40)
            {
                label = "Weak";
                StrengthLabel.Foreground = Brushes.Firebrick;
            }
            else if (score < 70)
            {
                label = "Medium";
                StrengthLabel.Foreground = Brushes.DarkOrange;
            }
            else
            {
                label = "Strong";
                StrengthLabel.Foreground = Brushes.ForestGreen;
            }

            StrengthBar.Value = score;
            StrengthLabel.Text = $"Password strength: {label}";
        }
    }
}