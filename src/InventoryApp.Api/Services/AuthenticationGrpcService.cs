using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Auth;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class AuthenticationGrpcService(IAuthApplicationService service)
    : AuthenticationService.AuthenticationServiceBase
{
    [AllowAnonymous]
    public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) =>
        service.LoginAsync(request, context.CancellationToken);

    public override Task<UserDto> GetCurrentUser(Empty request, ServerCallContext context) =>
        service.GetCurrentUserAsync(context.CancellationToken);

    public override Task<OperationResult> ChangePassword(ChangePasswordRequest request, ServerCallContext context) =>
        service.ChangePasswordAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageUsers)]
    public override Task<ListUsersResponse> ListUsers(PageRequest request, ServerCallContext context) =>
        service.ListUsersAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageUsers)]
    public override Task<UserDto> CreateUser(CreateUserRequest request, ServerCallContext context) =>
        service.CreateUserAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageUsers)]
    public override Task<UserDto> UpdateUser(UpdateUserRequest request, ServerCallContext context) =>
        service.UpdateUserAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageUsers)]
    public override Task<OperationResult> DeleteUser(IdRequest request, ServerCallContext context) =>
        service.DeleteUserAsync(request.Id, context.CancellationToken);
}
