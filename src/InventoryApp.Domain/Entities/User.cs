using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

public class User : AuditableEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
}
