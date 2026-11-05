using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class UserManagementWindow : Window
    {
        private readonly AuthService _auth;

        public UserManagementWindow()
        {
            InitializeComponent();
            _auth = AppDataService.Instance.Auth;
            RefreshList();
        }

        private void RefreshList()
        {
            UsersList.ItemsSource = null;
            UsersList.ItemsSource = _auth.Users;
        }

        private string _selectedRole = "admin";

        private void RoleRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (CardRoleAdmin == null || CardRoleStore == null || CardRoleFinance == null) return;

            // Reset all borders
            var defaultBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
            var defaultBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
            CardRoleAdmin.Background = defaultBg;
            CardRoleAdmin.BorderBrush = defaultBorder;
            CardRoleAdmin.BorderThickness = new Thickness(1);
            CardRoleStore.Background = defaultBg;
            CardRoleStore.BorderBrush = defaultBorder;
            CardRoleStore.BorderThickness = new Thickness(1);
            CardRoleFinance.Background = defaultBg;
            CardRoleFinance.BorderBrush = defaultBorder;
            CardRoleFinance.BorderThickness = new Thickness(1);

            if (RbRoleStore?.IsChecked == true)
            {
                _selectedRole = "storekeeper";
                CardRoleStore.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 250));
                CardRoleStore.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 184, 166));
                CardRoleStore.BorderThickness = new Thickness(1.5);
            }
            else if (RbRoleFinance?.IsChecked == true)
            {
                _selectedRole = "finance";
                CardRoleFinance.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 252, 232));
                CardRoleFinance.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8));
                CardRoleFinance.BorderThickness = new Thickness(1.5);
            }
            else
            {
                _selectedRole = "admin";
                CardRoleAdmin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 246, 255));
                CardRoleAdmin.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
                CardRoleAdmin.BorderThickness = new Thickness(1.5);
            }
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            string fullName = TxtFullName.Text.Trim();
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();

            string role = _selectedRole;

            var (ok, error) = _auth.CreateUser(username, password, fullName, role);
            if (!ok)
            {
                MessageBox.Show(error, "Cannot Create User", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtFullName.Clear();
            TxtUsername.Clear();
            TxtPassword.Clear();
            if (RbRoleAdmin != null) RbRoleAdmin.IsChecked = true;
            _selectedRole = "admin";

            RefreshList();
            MessageBox.Show($"Login created successfully for '{username}' ({role}).", "User Created", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string userId)
            {
                var user = _auth.Users.FirstOrDefault(u => u.Id == userId);
                if (user == null) return;

                var prompt = new Window
                {
                    Title = "Reset Password",
                    Width = 420,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252))
                };

                var sp = new StackPanel { Margin = new Thickness(22) };
                sp.Children.Add(new TextBlock
                {
                    Text = $"New Password for @{user.Username} ({user.FullName}):",
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                    Margin = new Thickness(0, 0, 0, 10),
                    FontSize = 13
                });

                var txt = new TextBox
                {
                    Text = user.Password,
                    Height = 36,
                    Background = System.Windows.Media.Brushes.White,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 18),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI"),
                    FontSize = 13,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                sp.Children.Add(txt);

                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var btnCancel = new Button
                {
                    Content = "Cancel",
                    Padding = new Thickness(16, 7, 16, 7),
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                    BorderThickness = new Thickness(1),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand
                };
                btnCancel.Click += (_, _) => prompt.Close();

                var btnSave = new Button
                {
                    Content = "Save Password",
                    Padding = new Thickness(18, 7, 18, 7),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 70, 229)),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btnSave.Click += (_, _) =>
                {
                    string newPass = txt.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(newPass))
                    {
                        var (ok, error) = _auth.UpdateUserPassword(userId, newPass);
                        if (ok)
                        {
                            MessageBox.Show($"Password for '{user.Username}' updated successfully to: {newPass}", "Password Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                            RefreshList();
                            prompt.Close();
                        }
                        else
                        {
                            MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                };

                btnPanel.Children.Add(btnCancel);
                btnPanel.Children.Add(btnSave);
                sp.Children.Add(btnPanel);

                prompt.Content = sp;
                prompt.ShowDialog();
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string userId)
            {
                var res = MessageBox.Show("Are you sure you want to delete this staff user account?", "Delete Account", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    var (ok, error) = _auth.DeleteUser(userId);
                    if (!ok)
                    {
                        MessageBox.Show(error, "Cannot Delete User", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    RefreshList();
                }
            }
        }

        
        private void CardRoleAdmin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RbRoleAdmin != null) RbRoleAdmin.IsChecked = true;
        }

        private void CardRoleStore_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RbRoleStore != null) RbRoleStore.IsChecked = true;
        }

        private void CardRoleFinance_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RbRoleFinance != null) RbRoleFinance.IsChecked = true;
        }

private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
