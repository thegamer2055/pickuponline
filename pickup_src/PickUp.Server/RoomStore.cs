using System.Collections.Concurrent;
using PickUp.Core;

namespace PickUp.Server;

public sealed class RoomStore
{
    public ConcurrentDictionary<string, GameRoom> Rooms { get; } = new(StringComparer.OrdinalIgnoreCase);
}
