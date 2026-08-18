using InventoryApp.Domain.Entities;

namespace InventoryApp.Application.Abstractions;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}
