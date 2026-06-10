using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using Maichess.User.V1;

namespace MaichessAnticheatService.Rest;

// IDevGate over user-service Users.GetUser: dev status is the persisted
// dev_mode profile field (knowledge-base decision — dev access is a
// user-service fact, not a client toggle). Unknown users are not devs.
// Excluded from coverage: live gRPC client shell.
[ExcludeFromCodeCoverage]
internal sealed class UserServiceDevGate(Users.UsersClient client) : IDevGate
{
    public async Task<bool> IsDevAsync(string userId, CancellationToken ct)
    {
        try
        {
            GetUserResponse response = await client.GetUserAsync(
                new GetUserRequest { UserId = userId },
                cancellationToken: ct);
            return response.User.DevMode;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }
}
