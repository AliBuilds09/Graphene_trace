using System;
using System.Windows;
using MyProject.Data;
using MyProject.Models;

namespace MyProject.Views
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly User _user;

        public ChangePasswordWindow(User user)
        {
            _user = user;
            InitializeComponent();

            SaveButton.Click += OnSaveClicked;
            CancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            var newPw = NewPasswordInput.Password;
            var confirm = ConfirmPasswordInput.Password;

            if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 8)
            {
                ValidationText.Text = "Password must be at least 8 characters.";
                return;
            }
            bool hasSpecial = false;
            foreach (var ch in newPw)
            {
                if (!char.IsLetterOrDigit(ch)) { hasSpecial = true; break; }
            }
            if (!hasSpecial)
            {
                ValidationText.Text = "Password must include at least one special character.";
                return;
            }
            if (!string.Equals(newPw, confirm, StringComparison.Ordinal))
            {
                ValidationText.Text = "Passwords do not match.";
                return;
            }

            try
            {
                UsersRepository.ForceChangePassword(_user.UserId, newPw);
                MessageBox.Show("Password updated. Please continue.");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ValidationText.Text = ex.Message;
            }
        }
    }
}