namespace PickUp.Core;

public enum GameMode { Online, Solo }
public enum FinishReason { None, TargetReached, NoValidMoves, TimeExpired }

public enum ScoreField
{
    Clover,
    Moon,
    Heart,
    Star,
    Horseshoe,
    Crown,
    FourOfKind,
    FiveOfKind,
    FullHouse,
    FiveDifferent,
    PairJoker,
    WildJoker
}

public sealed class PlayerGameState
{
    public required string Token { get; init; }
    public required string Name { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsConnected { get; set; }
    public bool IsComputer { get; init; }
    public int SeatIndex { get; set; }
    public int[] Dice { get; set; } = new int[5];
    public bool[] Held { get; set; } = new bool[5];
    public int RerollsLeft { get; set; } = 2;
    public bool Reroll1Used { get; set; }
    public bool Reroll2Used { get; set; }
    public int RollNumber { get; set; } = 1;
    public int TurnId { get; set; }
    public int RoundScore { get; set; }
    public int TotalScore { get; set; }
    public int RoundBonus { get; set; }
    public int LastAward { get; set; }
    public HashSet<ScoreField> UsedScoreFields { get; } = new();
    public Dictionary<ScoreField, int> ScoreFieldPoints { get; } = new();
    public string LastCombination { get; set; } = "";
    public bool Finished { get; set; }
    public bool ReadyForNextRound { get; set; }
    public FinishReason FinishReason { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public DateTimeOffset RoundStartedAtUtc { get; set; }
    public DateTimeOffset NextBotActionAtUtc { get; set; }
    public Dictionary<Guid, ActionResult> RecentActions { get; } = new();
    public Queue<Guid> RecentActionOrder { get; } = new();
}

public sealed class GameRoom
{
    public object Gate { get; } = new();
    public required string Code { get; init; }
    public required string OwnerToken { get; set; }
    public GameMode Mode { get; init; }
    public Dictionary<string, PlayerGameState> PlayersByToken { get; } = new(StringComparer.Ordinal);
    public bool Started { get; set; }
    public int Round { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RoundStartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long Version { get; set; }
}

public sealed record ActionResult(bool Accepted, string Message, int AwardedPoints = 0, int TimeBonus = 0);
