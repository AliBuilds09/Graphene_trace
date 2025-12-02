using System.Windows;
using MyProject.Controllers;
using MyProject.Models;

namespace MyProject.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            BuildLoginForm();
        }

        private void BuildLoginForm()
        {
            Title = "Login";

            var lblUser = new System.Windows.Controls.Label { Content = "Username" };
            System.Windows.Controls.Canvas.SetLeft(lblUser, 30);
            System.Windows.Controls.Canvas.SetTop(lblUser, 50);

            var txtUser = new System.Windows.Controls.TextBox { Width = 200 };
            System.Windows.Controls.Canvas.SetLeft(txtUser, 120);
            System.Windows.Controls.Canvas.SetTop(txtUser, 50);

            var lblPass = new System.Windows.Controls.Label { Content = "Password" };
            System.Windows.Controls.Canvas.SetLeft(lblPass, 30);
            System.Windows.Controls.Canvas.SetTop(lblPass, 90);

            var pwdPass = new System.Windows.Controls.PasswordBox { Width = 200 };
            System.Windows.Controls.Canvas.SetLeft(pwdPass, 120);
            System.Windows.Controls.Canvas.SetTop(pwdPass, 90);

            var lblRole = new System.Windows.Controls.Label { Content = "Role" };
            System.Windows.Controls.Canvas.SetLeft(lblRole, 30);
            System.Windows.Controls.Canvas.SetTop(lblRole, 130);

            var cmbRole = new System.Windows.Controls.ComboBox { Width = 200 };
            cmbRole.Items.Add("Patient");
            cmbRole.Items.Add("Clinician");
            cmbRole.Items.Add("Admin");
            System.Windows.Controls.Canvas.SetLeft(cmbRole, 120);
            System.Windows.Controls.Canvas.SetTop(cmbRole, 130);

            var btnLogin = new System.Windows.Controls.Button { Content = "Login", Width = 80 };
            System.Windows.Controls.Canvas.SetLeft(btnLogin, 120);
            System.Windows.Controls.Canvas.SetTop(btnLogin, 170);

            var btnRegister = new System.Windows.Controls.Button { Content = "Register", Width = 80 };
            System.Windows.Controls.Canvas.SetLeft(btnRegister, 210);
            System.Windows.Controls.Canvas.SetTop(btnRegister, 170);

            // Pending approval banner
            var pendingBanner = new System.Windows.Controls.TextBlock
            {
                Text = "Pending Admin Approval. Please wait for an administrator to approve your account.",
                Visibility = System.Windows.Visibility.Collapsed,
                Background = System.Windows.Media.Brushes.LightGoldenrodYellow,
                Foreground = System.Windows.Media.Brushes.DarkGoldenrod,
                Padding = new System.Windows.Thickness(6),
                Width = 360,
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
            System.Windows.Controls.Canvas.SetLeft(pendingBanner, 30);
            System.Windows.Controls.Canvas.SetTop(pendingBanner, 210);

            RootCanvas.Children.Add(lblUser);
            RootCanvas.Children.Add(txtUser);
            RootCanvas.Children.Add(lblPass);
            RootCanvas.Children.Add(pwdPass);
            RootCanvas.Children.Add(lblRole);
            RootCanvas.Children.Add(cmbRole);
            RootCanvas.Children.Add(btnLogin);
            RootCanvas.Children.Add(btnRegister);
            RootCanvas.Children.Add(pendingBanner);

            btnLogin.Click += (s, e) =>
            {
                var selectedRole = (cmbRole.SelectedItem as string) ?? string.Empty;
                var username = txtUser.Text.Trim();
                var password = pwdPass.Password;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(selectedRole))
                {
                    MessageBox.Show("Please enter username, password, and select a role.");
                    return;
                }

                if (AuthController.Authenticate(username, password, selectedRole, out User? u))
                {
                    pendingBanner.Visibility = System.Windows.Visibility.Collapsed;
                    if (u!.MustChangePassword)
                    {
                        var change = new ChangePasswordWindow(u);
                        var result = change.ShowDialog();
                        if (result != true)
                        {
                            MessageBox.Show("Password change required before proceeding.");
                            return;
                        }
                        // Reload user to reflect flag cleared
                        AuthController.GetUserById(u.UserId, out var refreshed);
                        u = refreshed ?? u;
                    }
                    var dash = new DashboardWindow(u.Role, u.Username);
                    dash.Show();
                    this.Close();
                }
                else
                {
                    // Show pending approval banner if the account exists and is unapproved for elevated roles
                    if (AuthController.GetUserByUsername(username, out var existing)
                        && existing != null
                        && (string.Equals(existing.Role, "Clinician", System.StringComparison.OrdinalIgnoreCase)
                            || string.Equals(existing.Role, "Admin", System.StringComparison.OrdinalIgnoreCase))
                        && !existing.ApprovedByAdmin)
                    {
                        pendingBanner.Text = "Pending Admin Approval. Your account is awaiting approval by an administrator.";
                        pendingBanner.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        pendingBanner.Visibility = System.Windows.Visibility.Collapsed;
                        MessageBox.Show("Invalid credentials. Please try again.");
                    }
                }
            };

            btnRegister.Click += (s, e) =>
            {
                var reg = new RegisterWindow();
                reg.ShowDialog();
            };
        }
    }
}