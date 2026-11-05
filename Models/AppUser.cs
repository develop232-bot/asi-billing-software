using System;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class AppUser
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [FirestoreProperty("username")]
        public string Username { get; set; } = string.Empty;

        [FirestoreProperty("password")]
        public string Password { get; set; } = string.Empty;

        [FirestoreProperty("fullName")]
        public string FullName { get; set; } = string.Empty;

        [FirestoreProperty("role")]
        public string Role { get; set; } = "storekeeper"; // admin, storekeeper, finance

        [FirestoreProperty("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

        [FirestoreProperty("isActive")]
        public bool IsActive { get; set; } = true;

        public string RoleTitle => Role switch
        {
            "admin" => "Administrator (Full Access)",
            "storekeeper" => "Store Keeper (Stock & Production)",
            "finance" => "Finance & Accounts (Billing & Accounts)",
            _ => Role
        };

        public string RoleBadgeColor => Role switch
        {
            "admin" => "#4338CA",
            "storekeeper" => "#0D9488",
            "finance" => "#B45309",
            _ => "#475569"
        };
    }

    public class UserSession
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime LoggedInAt { get; set; } = DateTime.UtcNow;
        public bool RememberMe { get; set; } = true;
    }
}
