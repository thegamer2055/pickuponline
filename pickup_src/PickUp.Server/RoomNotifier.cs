using Microsoft.AspNetCore.SignalR;
using PickUp.Server.Hubs;

namespace PickUp.Server;

public sealed class RoomNotifier(IHubContext<GameHub> hub, GameCoordinator coordinator)
{
    public async Task PublishAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var publicState = coordinator.GetPublicState(code);
            await hub.Clients.Group(code).SendAsync("PublicState", publicState, cancellationToken);
            foreach (var recipient in coordinator.GetConnectedPlayers(code))
            {
                try
                {
                    var state = coordinator.GetStateForConnection(code, recipient.Token, recipient.ConnectionId);
                    await hub.Clients.Client(recipient.ConnectionId).SendAsync("RoomState", state, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Verbindung/Room kann sich zwischen Empfängerliste und Versand ändern.
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Raum wurde zwischen Tick und Publish bereits entfernt/ist nicht mehr vorhanden.
        }
    }
}
