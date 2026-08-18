using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Auth;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class AuthApplicationService(
    IInventoryDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    ILogger<AuthApplicationService> logger) : IAuthApplicationService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = Guard.Required(request.Username, "Username", 100);
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password is required.");
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);

        // Same message for unknown user and wrong password so the endpoint does not leak account existence.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Username}", username);
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("This account has been deactivated. Contact an administrator.");
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokenService.CreateToken(user);
        var response = new LoginResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAt.ToIso(),
            User = user.ToDto()
        };

        response.Permissions.AddRange(RolePermissions.For((UserRole)(int)user.Role));
        logger.LogInformation("User {Username} signed in", user.Username);
        return response;
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new NotFoundException("User", currentUser.UserId);

        return user.ToDto();
    }

    public async Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new NotFoundException("User", currentUser.UserId);

        if (!passwordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            throw new ValidationException("The current password is incorrect.");
        }

        ValidatePassword(request.NewPassword);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.Touch();
        await db.SaveChangesAsync(ct);

        return new OperationResult { Success = true, Message = "Password updated." };
    }

    public async Task<ListUsersResponse> ListUsersAsync(PageRequest request, CancellationToken ct)
    {
        var search = Paging.SearchTerm(request);
        var query = db.Users.AsNoTracking().AsQueryable();

        if (search.Length > 0)
        {
            query = query.Where(u =>
                EF.Functions.Like(u.Username, $"%{search}%") ||
                EF.Functions.Like(u.FullName, $"%{search}%") ||
                EF.Functions.Like(u.Email, $"%{search}%"));
        }

        query = Paging.ApplySort(query, request, new Dictionary<string, System.Linq.Expressions.Expression<Func<User, object>>>
        {
            ["username"] = u => u.Username,
            ["fullName"] = u => u.FullName,
            ["email"] = u => u.Email,
            ["role"] = u => u.Role,
            ["createdAtUtc"] = u => u.CreatedAtUtc
        }, "username");

        var (items, info) = await Paging.ToPageAsync(query, request, ct);

        var response = new ListUsersResponse { PageInfo = info };
        response.Items.AddRange(items.Select(u => u.ToDto()));
        return response;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        var username = Guard.Required(request.Username, "Username", 100);
        var email = Guard.Required(request.Email, "Email", 200);
        Guard.Email(email, "Email");
        var fullName = Guard.Required(request.FullName, "Full name", 200);
        ValidatePassword(request.Password);

        if (request.Role == UserRole.Unspecified)
        {
            throw new ValidationException("A role must be selected.");
        }

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
        {
            throw new ConflictException($"Username '{username}' is already taken.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = (DomainEnums.UserRole)(int)request.Role,
            IsActive = request.IsActive
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                   ?? throw new NotFoundException("User", request.Id);

        var email = Guard.Required(request.Email, "Email", 200);
        Guard.Email(email, "Email");

        if (request.Role == UserRole.Unspecified)
        {
            throw new ValidationException("A role must be selected.");
        }

        // Guard against locking everyone out of administration.
        var isDemotingLastAdmin =
            user.Role == DomainEnums.UserRole.Administrator &&
            (request.Role != UserRole.Administrator || !request.IsActive);

        if (isDemotingLastAdmin)
        {
            var otherAdmins = await db.Users.CountAsync(
                u => u.Id != user.Id && u.Role == DomainEnums.UserRole.Administrator && u.IsActive, ct);

            if (otherAdmins == 0)
            {
                throw new ConflictException("At least one active administrator must remain.");
            }
        }

        user.Email = email;
        user.FullName = Guard.Required(request.FullName, "Full name", 200);
        user.Role = (DomainEnums.UserRole)(int)request.Role;
        user.IsActive = request.IsActive;

        if (request.HasNewPassword && !string.IsNullOrWhiteSpace(request.NewPassword))
        {
            ValidatePassword(request.NewPassword);
            user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        }

        user.Touch();
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<OperationResult> DeleteUserAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User", id);

        if (user.Id == currentUser.UserId)
        {
            throw new ConflictException("You cannot delete your own account.");
        }

        if (user.Role == DomainEnums.UserRole.Administrator)
        {
            var otherAdmins = await db.Users.CountAsync(
                u => u.Id != user.Id && u.Role == DomainEnums.UserRole.Administrator && u.IsActive, ct);

            if (otherAdmins == 0)
            {
                throw new ConflictException("At least one active administrator must remain.");
            }
        }

        // Stock movements keep a denormalised user name, so history survives the delete.
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return new OperationResult { Success = true, Message = $"User '{user.Username}' deleted." };
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ValidationException("Password must be at least 6 characters long.");
        }

        if (password.Length > 128)
        {
            throw new ValidationException("Password cannot be longer than 128 characters.");
        }
    }
}
