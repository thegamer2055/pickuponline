using PickUp.Core;
using PickUp.Server;
using Xunit;

namespace PickUp.Tests;

public sealed class MultiplayerCoordinatorTests
{
    [Fact]
    public void TwoPlayersHaveIndependentPrivateBoardsButShareRoomVersion()
    {
        var coordinator = new GameCoordinator(new RoomStore());
        var start = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var host = coordinator.CreateRoom("Österreich", "connection-a");
        var guest = coordinator.JoinRoom(host.Code, "Deutschland", "connection-b");
        coordinator.StartGame(host.Code, host.Token, "connection-a", start);

        var hostState = coordinator.GetState(host.Code, host.Token, start);
        var guestState = coordinator.GetState(host.Code, guest.Token, start);
        Assert.Equal(hostState.Public.Version, guestState.Public.Version);
        Assert.Equal(start, hostState.Public.RoundStartedAtUtc);
        Assert.Equal(hostState.Public.RoundStartedAtUtc, guestState.Public.RoundStartedAtUtc);
        Assert.NotSame(hostState.Me.Dice, guestState.Me.Dice);

        var guestDiceBefore = guestState.Me.Dice.ToArray();
        coordinator.Reroll(host.Code, host.Token, "connection-a", hostState.Me.TurnId, hostState.Me.RollNumber, [false,false,false,false,false], Guid.NewGuid(), start.AddSeconds(1));
        var guestAfterHostAction = coordinator.GetState(host.Code, guest.Token, start.AddSeconds(1));
        Assert.Equal(guestDiceBefore, guestAfterHostAction.Me.Dice);
    }

    [Fact]
    public void RepeatedActionIdDoesNotConsumeAnotherReroll()
    {
        var coordinator = new GameCoordinator(new RoomStore());
        var start = DateTimeOffset.UtcNow;
        var host = coordinator.CreateRoom("A", "a");
        var guest = coordinator.JoinRoom(host.Code, "B", "b");
        coordinator.StartGame(host.Code, host.Token, "a", start);
        var id = Guid.NewGuid();

        var before = coordinator.GetState(host.Code, host.Token, start).Me;
        coordinator.Reroll(host.Code, host.Token, "a", before.TurnId, before.RollNumber, [true,false,false,false,false], id, start.AddSeconds(1));
        var afterFirst = coordinator.GetState(host.Code, host.Token, start.AddSeconds(1)).Me.RerollsLeft;
        coordinator.Reroll(host.Code, host.Token, "a", before.TurnId, before.RollNumber, [false,false,false,false,false], id, start.AddSeconds(2));
        var afterDuplicate = coordinator.GetState(host.Code, host.Token, start.AddSeconds(2)).Me.RerollsLeft;

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, afterDuplicate);
    }

    [Fact]
    public void BothExpiredPlayersAdvanceTogetherToNextRound()
    {
        var coordinator = new GameCoordinator(new RoomStore());
        var start = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var host = coordinator.CreateRoom("A", "a");
        var guest = coordinator.JoinRoom(host.Code, "B", "b");
        coordinator.StartGame(host.Code, host.Token, "a", start);

        var changed = coordinator.Tick(start.AddSeconds(211));
        var hostState = coordinator.GetState(host.Code, host.Token, start.AddSeconds(211));
        var guestState = coordinator.GetState(host.Code, guest.Token, start.AddSeconds(211));

        Assert.Contains(host.Code, changed);
        Assert.Equal(2, hostState.Public.Round);
        Assert.Equal(hostState.Public.Round, guestState.Public.Round);
        Assert.All(hostState.Public.Players, p => Assert.False(p.Finished));
        Assert.Equal(hostState.Public.Version, guestState.Public.Version);
    }
    [Fact]
    public void StaleRollNumberAndOldConnectionAreRejected()
    {
        var store = new RoomStore();
        var coordinator = new GameCoordinator(store);
        var start = DateTimeOffset.UtcNow;
        var host = coordinator.CreateRoom("A", "connection-a");
        coordinator.JoinRoom(host.Code, "B", "connection-b");
        coordinator.StartGame(host.Code, host.Token, "connection-a", start);
        var before = coordinator.GetState(host.Code, host.Token, start).Me;

        coordinator.Reroll(host.Code, host.Token, "connection-a", before.TurnId, before.RollNumber,
            [true,false,false,false,false], Guid.NewGuid(), start.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => coordinator.Reroll(host.Code, host.Token, "connection-a",
            before.TurnId, before.RollNumber, [false,false,false,false,false], Guid.NewGuid(), start.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => coordinator.Reroll(host.Code, host.Token, "stale-connection",
            before.TurnId, 2, [false,false,false,false,false], Guid.NewGuid(), start.AddSeconds(2)));
    }

    [Fact]
    public void NoValidCombinationAfterThirdRollFinishesAndFreezesPlayerTimer()
    {
        var store = new RoomStore();
        var coordinator = new GameCoordinator(store);
        var start = DateTimeOffset.UtcNow;
        var host = coordinator.CreateRoom("A", "connection-a");
        coordinator.JoinRoom(host.Code, "B", "connection-b");
        coordinator.StartGame(host.Code, host.Token, "connection-a", start);
        var room = store.Rooms[host.Code];
        int turnId;
        lock (room.Gate)
        {
            var player = room.PlayersByToken[host.Token];
            player.Dice = [0,1,2,3,6]; // Paar-Joker ohne natürliches Paar: keine Wertung
            player.RerollsLeft = 1;
            player.RollNumber = 2;
            turnId = player.TurnId;
        }

        coordinator.Reroll(host.Code, host.Token, "connection-a", turnId, 2,
            [true,true,true,true,true], Guid.NewGuid(), start.AddSeconds(1));
        var finished = coordinator.GetState(host.Code, host.Token, start.AddSeconds(30)).Me;
        Assert.True(finished.Finished);
        Assert.Equal(FinishReason.NoValidMoves, finished.FinishReason);
        Assert.Equal(209, finished.RemainingSeconds); // Timer bleibt beim Beenden stehen.
    }

}
