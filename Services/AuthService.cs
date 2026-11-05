using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AsiErp.Models;

namespace AsiErp.Services
{
    public class AuthService
    {
        private readonly string _sessionFilePath;
        private readonly string _usersFilePath;
        private readonly FirebaseService _firebase;
        private readonly SyncQueueService _syncQueue;

        public List<AppUser> Users { get; private set; } = new();
        public AppUser? CurrentUser { get; private set; }

        public bool IsLoggedIn => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.Role == "admin";
        public bool IsStoreKeeper => CurrentUser?.Role == "storekeeper" || IsAdmin;
        public bool IsFinance => CurrentUser?.Role == "finance" || IsAdmin;

        public event Action? AuthStateChanged;

        public AuthService(FirebaseService firebase, SyncQueueService syncQueue)
        {
            _firebase = firebase;
            _syncQueue = syncQueue;

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiErp");
            Directory.CreateDirectory(folder);
            _sessionFilePath = Path.Combine(folder, "session.json");
            _usersFilePath = Path.Combine(folder, "users.json");

            LoadUsers();
            EnsureDefaultAdmin();
        }

        public void EnsureDefaultAdmin()
        {
            if (!Users.Any(u => u.Role == "admin"))
            {
                var admin = new AppUser
                {
                    Id = "usr_admin_001",
                    Username = "admin",
                    Password = "admin", // Simple easy-to-use initial password
                    FullName = "System Administrator",
                    Role = "admin"
                };
                Users.Add(admin);
                SaveUsers();
                _syncQueue.EnqueueSet("users", admin.Id, admin);
            }

            // Also seed default Store Keeper and Finance logins if empty
            if (!Users.Any(u => u.Role == "storekeeper"))
            {
                var sk = new AppUser
                {
                    Id = "usr_sk_001",
                    Username = "store",
                    Password = "store",
                    FullName = "Store Incharge",
                    Role = "storekeeper"
                };
                Users.Add(sk);
                SaveUsers();
                _syncQueue.EnqueueSet("users", sk.Id, sk);
            }

            if (!Users.Any(u => u.Role == "finance"))
            {
                var fa = new AppUser
                {
                    Id = "usr_fa_001",
                    Username = "finance",
                    Password = "finance",
                    FullName = "Finance Manager",
                    Role = "finance"
                };
                Users.Add(fa);
                SaveUsers();
                _syncQueue.EnqueueSet("users", fa.Id, fa);
            }
        }

        public bool TryAutoLogin()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    string json = File.ReadAllText(_sessionFilePath);
                    var session = JsonSerializer.Deserialize<UserSession>(json);
                    if (session != null && session.RememberMe && !string.IsNullOrEmpty(session.Username))
                    {
                        var user = Users.FirstOrDefault(u =>
                            u.Username.Equals(session.Username, StringComparison.OrdinalIgnoreCase) && u.IsActive);

                        if (user != null)
                        {
                            CurrentUser = user;
                            AuthStateChanged?.Invoke();
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public (bool success, string error) Login(string username, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Please enter both username and password.");
            }

            var user = Users.FirstOrDefault(u =>
                u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase) &&
                u.Password == password.Trim());

            if (user == null)
            {
                return (false, "Invalid username or password. Please check your credentials.");
            }

            if (!user.IsActive)
            {
                return (false, "This user account has been deactivated. Please contact your Administrator.");
            }

            CurrentUser = user;

            if (rememberMe)
            {
                SaveSession(new UserSession
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role,
                    RememberMe = true,
                    LoggedInAt = DateTime.UtcNow
                });
            }
            else
            {
                ClearSession();
            }

            AuthStateChanged?.Invoke();
            return (true, string.Empty);
        }

        public void Logout()
        {
            CurrentUser = null;
            ClearSession();
            AuthStateChanged?.Invoke();
        }

        public (bool success, string error) CreateUser(string username, string password, string fullName, string role)
        {
            username = username.Trim();
            if (string.IsNullOrWhiteSpace(username)) return (false, "Username is required.");
            if (string.IsNullOrWhiteSpace(password)) return (false, "Password is required.");
            if (string.IsNullOrWhiteSpace(fullName)) fullName = username;

            if (Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Username '{username}' already exists. Choose a different username.");
            }

            var newUser = new AppUser
            {
                Id = "usr_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Username = username,
                Password = password.Trim(),
                FullName = fullName.Trim(),
                Role = role
            };

            Users.Insert(0, newUser);
            SaveUsers();

            _syncQueue.EnqueueSet("users", newUser.Id, newUser);
            return (true, string.Empty);
        }

        public (bool success, string error) UpdateUserPassword(string userId, string newPassword)
        {
            var user = Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return (false, "User not found.");

            user.Password = newPassword.Trim();
            SaveUsers();
            _syncQueue.EnqueueSet("users", user.Id, user);
            return (true, string.Empty);
        }

        public (bool success, string error) DeleteUser(string userId)
        {
            var user = Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return (false, "User not found.");

            if (user.Role == "admin" && Users.Count(u => u.Role == "admin") <= 1)
            {
                return (false, "Cannot delete the sole Administrator account.");
            }

            if (CurrentUser?.Id == userId)
            {
                return (false, "You cannot delete your own currently logged-in account.");
            }

            Users.RemoveAll(u => u.Id == userId);
            SaveUsers();
            _syncQueue.EnqueueDelete("users", userId);
            return (true, string.Empty);
        }

        public void MergeCloudUsers(List<AppUser> cloudUsers)
        {
            if (cloudUsers == null || cloudUsers.Count == 0) return;

            foreach (var cu in cloudUsers)
            {
                var local = Users.FirstOrDefault(u => u.Id == cu.Id || u.Username.Equals(cu.Username, StringComparison.OrdinalIgnoreCase));
                if (local != null)
                {
                    local.FullName = cu.FullName;
                    local.Role = cu.Role;
                    local.Password = cu.Password;
                    local.IsActive = cu.IsActive;
                }
                else
                {
                    Users.Add(cu);
                }
            }

            SaveUsers();
        }

        private void SaveSession(UserSession session)
        {
            try
            {
                string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_sessionFilePath, json);
            }
            catch { }
        }

        private void ClearSession()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                }
            }
            catch { }
        }

        public void SaveUsers()
        {
            try
            {
                string json = JsonSerializer.Serialize(Users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_usersFilePath, json);
            }
            catch { }
        }

        private void LoadUsers()
        {
            try
            {
                if (File.Exists(_usersFilePath))
                {
                    string json = File.ReadAllText(_usersFilePath);
                    var list = JsonSerializer.Deserialize<List<AppUser>>(json);
                    if (list != null && list.Count > 0)
                    {
                        Users = list;
                    }
                }
            }
            catch { }
        }
    }
}
