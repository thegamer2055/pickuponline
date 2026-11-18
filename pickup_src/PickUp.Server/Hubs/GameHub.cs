using Microsoft.AspNetCore.SignalR;
using PickUp.Server;
using Microsoft.AspNetCore.Mvc;

namespace PickUp.Server.Hubs;

public sealed class GameHub(GameCoordinator coordinator, RoomNotifier notifier) : Hub
{
    public async Task<JoinInfo> CreateRoom(string playerName)
    {
        var info = Execute(() => coordinator.CreateRoom(playerName, Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, info.Code);
        await notifier.PublishAsync(info.Code);
        return info;
    }

    public async Task<JoinInfo> CreateSolo(string playerName)
    {
        var info = Execute(() => coordinator.CreateSolo(playerName, Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, info.Code);
        await notifier.PublishAsync(info.Code);
        return info;
    }

    public async Task<JoinInfo> JoinRoom(string code, string playerName)
    {
        var info = Execute(() => coordinator.JoinRoom(code, playerName, Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, info.Code);
        await notifier.PublishAsync(info.Code);
        return info;
    }

    public async Task<RoomState> RejoinRoom(string code, string token)
    {
        var state = Execute(() => coordinator.RejoinRoom(code, token, Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, state.Public.Code);
        await notifier.PublishAsync(state.Public.Code);
        return state;
    }

    public async Task LeaveRoom(string code, string token)
    {
        var result = Execute(() => coordinator.LeaveRoom(code, token, Context.ConnectionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, result.Code);
        if (!result.RoomRemoved) await notifier.PublishAsync(result.Code);
    }

    public async Task StartGame(string code, string token)
    {
        Execute(() => { coordinator.StartGame(code, token, Context.ConnectionId); return true; });
        await notifier.PublishAsync(code);
    }
    public async Task RestartGame(string code, string token)
    {
        Execute(() =>
        {
            coordinator.RestartGame(
                code,
                token,
                Context.ConnectionId);

            return true;
        });

        await notifier.PublishAsync(code);
    }

    public async Task<bool> ConfirmNextRound(string code, string token)
    {
        var started = Execute(() =>
            coordinator.ConfirmNextRound(
                code,
                token,
                Context.ConnectionId));

        await notifier.PublishAsync(code);

        return started;
    }

    public async Task<PickUp.Core.ActionResult> Reroll(
    string code,
    string token,
    int expectedTurnId,
    int expectedRollNumber,
    int rerollButton,
    bool[] held,
    string actionId)
    {
        var id = ParseActionId(actionId);

        var result = Execute(() => coordinator.Reroll(
            code,
            token,
            Context.ConnectionId,
            expectedTurnId,
            expectedRollNumber,
            rerollButton,
            held,
            id));

        await notifier.PublishAsync(code);
        return result;
    }

    public async Task<PickUp.Core.ActionResult> Bank(string code, string token, int expectedTurnId, int expectedRollNumber, string scoreField, string actionId)
    {
        var id = ParseActionId(actionId);
        var result = Execute(() => coordinator.Bank(code, token, Context.ConnectionId, expectedTurnId, expectedRollNumber, scoreField, id));
        await notifier.PublishAsync(code);
        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var code in coordinator.Disconnect(Context.ConnectionId))
            await notifier.PublishAsync(code);
        await base.OnDisconnectedAsync(exception);
    }

    private static T Execute<T>(Func<T> action)
    {
        try { return action(); }
        catch (InvalidOperationException ex) { throw new HubException(ex.Message); }
        catch (ArgumentException ex) { throw new HubException(ex.Message); }
    }

    private static Guid ParseActionId(string value)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            throw new HubException("Ungültige Aktions-ID.");
        return id;
    }
}
