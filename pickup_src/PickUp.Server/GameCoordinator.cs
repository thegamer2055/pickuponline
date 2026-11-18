using System.Security.Cryptography;
using PickUp.Core;

namespace PickUp.Server;

public sealed class GameCoordinator(RoomStore store)
{
    public JoinInfo CreateRoom(string playerName, string connectionId)
    {
        var code = NewRoomCode();
        var token = NewToken();
        var room = new GameRoom { Code = code, OwnerToken = token, Mode = GameMode.Online };
        var player = NewPlayer(token, playerName, connectionId, false, 0);
        room.PlayersByToken.Add(token, player);
        room.Version = 1;
        if (!store.Rooms.TryAdd(code, room)) throw new InvalidOperationException("Raumcode-Kollision. Bitte erneut versuchen.");
        return new JoinInfo(code, token, player.Name, room.Mode);
    }

    public JoinInfo CreateSolo(string playerName, string connectionId, DateTimeOffset? now = null)
    {
        var instant = now ?? DateTimeOffset.UtcNow;
        var code = NewRoomCode();
        var token = NewToken();
        var room = new GameRoom { Code = code, OwnerToken = token, Mode = GameMode.Solo };
        room.PlayersByToken.Add(token, NewPlayer(token, playerName, connectionId, false, 0));
        var botToken = NewToken();
        room.PlayersByToken.Add(botToken, NewPlayer(botToken, "Computer", null, true, 1));
        StartRoom(room, instant);
        if (!store.Rooms.TryAdd(code, room)) throw new InvalidOperationException("Raumcode-Kollision. Bitte erneut versuchen.");
        return new JoinInfo(code, token, room.PlayersByToken[token].Name, room.Mode);
    }

    public JoinInfo JoinRoom(string code, string playerName, string connectionId)
    {
        var room = GetRoom(code);
        lock (room.Gate)
        {
            if (room.Started) throw new InvalidOperationException("Diese Partie läuft bereits.");
            if (room.PlayersByToken.Values.Any(player => player.ConnectionId == connectionId))
                throw new InvalidOperationException("Diese Verbindung ist bereits in diesem Raum.");
            if (room.PlayersByToken.Count >= 2) throw new InvalidOperationException("Der Raum ist voll.");
            var token = NewToken();
            var player = NewPlayer(token, playerName, connectionId, false, 1);
            room.PlayersByToken.Add(token, player);
            room.Version++;
            return new JoinInfo(room.Code, token, player.Name, room.Mode);
        }
    }

    public RoomState RejoinRoom(string code, string token, string connectionId, DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        lock (room.Gate)
        {
            if (!room.PlayersByToken.TryGetValue(token ?? "", out var player))
                throw new InvalidOperationException("Raumcode oder Wiederverbindungstoken ist ungültig.");
            player.ConnectionId = connectionId;
            player.IsConnected = true;
            room.Version++;
            return BuildState(room, player, now ?? DateTimeOffset.UtcNow);
        }
    }

    public void StartGame(string code, string token, string connectionId, DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        lock (room.Gate)
        {
            var owner = GetPlayer(room, token);
            EnsureConnection(owner, connectionId);
            if (!string.Equals(room.OwnerToken, token, StringComparison.Ordinal))
                throw new InvalidOperationException("Nur der Ersteller des Raums kann starten.");
            if (room.Started) throw new InvalidOperationException("Das Spiel läuft bereits.");
            if (room.PlayersByToken.Count != 2)
                throw new InvalidOperationException("Zum Start müssen beide Spieler im Raum sein.");
            if (room.PlayersByToken.Values.Any(player => !player.IsComputer && !player.IsConnected))
                throw new InvalidOperationException("Beide Spieler müssen verbunden sein, bevor das Spiel startet.");
            StartRoom(room, now ?? DateTimeOffset.UtcNow);
        }
    }


    public void RestartGame(
    string code,
    string token,
    string connectionId,
    DateTimeOffset? now = null)
    {
        var room = GetRoom(code);

        lock (room.Gate)
        {
            var owner = GetPlayer(room, token);
            EnsureConnection(owner, connectionId);

            if (!string.Equals(room.OwnerToken, token, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Nur der Ersteller des Raums kann eine neue Partie starten.");

            if (room.Started || room.Round < 5 || room.CompletedAtUtc is null)
                throw new InvalidOperationException(
                    "Die aktuelle Partie ist noch nicht beendet.");

            if (room.PlayersByToken.Values.Any(
                player => !player.IsComputer && !player.IsConnected))
            {
                throw new InvalidOperationException(
                    "Beide Spieler müssen verbunden sein.");
            }

            StartRoom(room, now ?? DateTimeOffset.UtcNow);
        }
    }

    public bool ConfirmNextRound(
    string code,
    string token,
    string connectionId,
    DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        var instant = now ?? DateTimeOffset.UtcNow;

        lock (room.Gate)
        {
            var player = GetPlayer(room, token);
            EnsureConnection(player, connectionId);

            if (!room.Started)
                throw new InvalidOperationException("Das Spiel läuft nicht.");

            if (!player.Finished)
                throw new InvalidOperationException(
                    "Du musst zuerst deine Runde beenden.");

            if (player.ReadyForNextRound)
                return false;

            player.ReadyForNextRound = true;
            room.Version++;

            return StartNextRoundIfEveryoneReady(room, instant);
        }
    }

    public ActionResult Reroll(
    string code,
    string token,
    string connectionId,
    int expectedTurnId,
    int expectedRollNumber,
    int rerollButton,
    bool[] held,
    Guid actionId,
    DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        var instant = now ?? DateTimeOffset.UtcNow;

        lock (room.Gate)
        {
            var player = GetPlayer(room, token);
            EnsureConnection(player, connectionId);

            if (TryGetDuplicate(player, actionId, out var duplicate))
                return duplicate;

            EnsureActive(room, player, instant);

            if (player.TurnId != expectedTurnId ||
                player.RollNumber != expectedRollNumber)
            {
                throw new InvalidOperationException(
                    "Der Wurf wurde bereits verändert. Bitte den aktuellen Spielstand verwenden.");
            }

            if (held is null || held.Length != 5)
                throw new InvalidOperationException(
                    "Die Halte-Auswahl muss genau fünf Positionen enthalten.");

            if (rerollButton == 1 && player.Reroll1Used)
                throw new InvalidOperationException("Die 1. Neudrehung wurde bereits verwendet.");

            if (rerollButton == 2 && player.Reroll2Used)
                throw new InvalidOperationException("Die 2. Neudrehung wurde bereits verwendet.");

            if (rerollButton != 1 && rerollButton != 2)
                throw new InvalidOperationException("Ungültiger Neudrehungs-Knopf.");

            if (player.RerollsLeft <= 0)
                throw new InvalidOperationException("Für diesen Zug sind keine weiteren Drehungen möglich.");

            if (rerollButton == 1)
                player.Reroll1Used = true;
            else
                player.Reroll2Used = true;

            player.Held = held.ToArray();

            for (var i = 0; i < 5; i++)
            {
                if (!player.Held[i])
                    player.Dice[i] = RandomNumberGenerator.GetInt32(0, 6);
            }

            player.RerollsLeft--;
            player.RollNumber++;

            var result = new ActionResult(true, "Neu gedreht.");

            if (player.RerollsLeft == 0 &&
                GameRules.AvailableFieldScores(
                    player.Dice,
                    player.UsedScoreFields).Count == 0)
            {
                FinishPlayer(player, FinishReason.NoValidMoves, instant);

                result = new ActionResult(
                    true,
                    "Keine freie gültige Wertung möglich – du wartest auf den anderen Spieler.");
            }

            Remember(player, actionId, result);
            room.Version++;

            StartNextRoundIfEveryoneReady(room, instant);

            return result;
        }
    }



    public ActionResult Bank(
        string code,
        string token,
        string connectionId,
        int expectedTurnId,
        int expectedRollNumber,
        string scoreField,
        Guid actionId,
        DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        var instant = now ?? DateTimeOffset.UtcNow;

        lock (room.Gate)
        {
            var player = GetPlayer(room, token);
            EnsureConnection(player, connectionId);

            if (TryGetDuplicate(player, actionId, out var duplicate))
                return duplicate;

            EnsureActive(room, player, instant);

            if (player.TurnId != expectedTurnId ||
                player.RollNumber != expectedRollNumber)
            {
                throw new InvalidOperationException(
                    "Dieser Wurf wurde bereits verändert oder gewertet. Bitte den aktuellen Spielstand verwenden.");
            }

            if (!Enum.TryParse<ScoreField>(
                    scoreField,
                    true,
                    out var field) ||
                !Enum.IsDefined(typeof(ScoreField), field))
            {
                throw new InvalidOperationException(
                    "Das ausgewählte Wertungsfeld ist ungültig.");
            }

            if (player.UsedScoreFields.Contains(field))
            {
                throw new InvalidOperationException(
                    "Dieses Wertungsfeld wurde bereits verwendet.");
            }

            var score = GameRules.ScoreForField(player.Dice, field);

            if (score.Points <= 0)
            {
                throw new InvalidOperationException(
                    "Diese Würfelkombination ist für das ausgewählte Wertungsfeld nicht gültig.");
            }

            player.UsedScoreFields.Add(field);

            player.ScoreFieldPoints[field] = score.Points;

            var remainingSeconds = GameRules.RemainingSeconds(
     player.RoundStartedAtUtc,
     instant);

            Console.WriteLine(
                $"ZEITBONUS-TEST: Spieler={player.Name}, " +
                $"Runde={room.Round}, " +
                $"Rundenstart={player.RoundStartedAtUtc:O}, " +
                $"Wertungszeit={instant:O}, " +
                $"Restsekunden={remainingSeconds}");

            var award = GameRules.ApplyRoundAward(
                player.RoundScore,
                score.Points,
                room.Round,
                remainingSeconds);

            player.RoundScore = award.NewRoundScore;
            player.TotalScore += score.Points + award.Bonus;
            player.LastAward = score.Points;

            player.LastCombination = string.IsNullOrWhiteSpace(score.JokerExplanation)
                ? score.Combination
                : $"{score.Combination} ({score.JokerExplanation})";

            var bonus = award.Bonus;

            if (award.TargetReached)
            {
                player.RoundBonus += bonus;
            }

            StartNextTurn(player, instant);

            var jokerText = string.IsNullOrWhiteSpace(score.JokerExplanation)
                ? ""
                : $" ({score.JokerExplanation})";

            var result = new ActionResult(
                true,
                $"{score.Combination}: {score.Points:N0} Punkte{jokerText}" +
                (bonus > 0 ? $" + {bonus:N0} Zeitbonus" : ""),
                score.Points,
                bonus);

            Remember(player, actionId, result);
            room.Version++;

            StartNextRoundIfEveryoneReady(room, instant);

            return result;
        }
    }


    public RoomState GetState(string code, string token, DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        lock (room.Gate) return BuildState(room, GetPlayer(room, token), now ?? DateTimeOffset.UtcNow);
    }

    public RoomState GetStateForConnection(string code, string token, string connectionId, DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        lock (room.Gate)
        {
            var player = GetPlayer(room, token);
            EnsureConnection(player, connectionId);
            return BuildState(room, player, now ?? DateTimeOffset.UtcNow);
        }
    }

    public PublicRoomState GetPublicState(string code, DateTimeOffset? now = null)
    {
        var room = GetRoom(code);
        lock (room.Gate) return BuildPublicState(room, now ?? DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<ConnectedPlayer> GetConnectedPlayers(string code)
    {
        var room = GetRoom(code);
        lock (room.Gate)
            return room.PlayersByToken.Values
                .Where(p => p.IsConnected && !string.IsNullOrWhiteSpace(p.ConnectionId))
                .Select(p => new ConnectedPlayer(p.ConnectionId!, p.Token)).ToArray();
    }

    public LeaveRoomResult LeaveRoom(string code, string token, string connectionId)
    {
        var room = GetRoom(code);
        lock (room.Gate)
        {
            var player = GetPlayer(room, token);
            EnsureConnection(player, connectionId);
            player.ConnectionId = null;
            player.IsConnected = false;

            if (!room.Started)
            {
                room.PlayersByToken.Remove(token);
                if (room.PlayersByToken.Count == 0)
                {
                    store.Rooms.TryRemove(room.Code, out _);
                    return new LeaveRoomResult(room.Code, true);
                }

                var remaining = room.PlayersByToken.Values.Single();
                remaining.SeatIndex = 0;
                if (room.OwnerToken == token) room.OwnerToken = remaining.Token;
            }

            room.Version++;
            return new LeaveRoomResult(room.Code, false);
        }
    }

    public IReadOnlyList<string> Disconnect(string connectionId)
    {
        var affectedRooms = new List<string>();
        foreach (var room in store.Rooms.Values)
        {
            lock (room.Gate)
            {
                var affected = room.PlayersByToken.Values.Where(p => p.ConnectionId == connectionId).ToArray();
                if (affected.Length == 0) continue;
                foreach (var player in affected)
                {
                    player.ConnectionId = null;
                    player.IsConnected = false;
                }
                room.Version++;
                affectedRooms.Add(room.Code);
            }
        }
        return affectedRooms;
    }

    // Vom Sekunden-Takt aufgerufen. Die Rückgabe enthält Räume, deren Zustand an Clients
    // publiziert werden soll (Countdown, Bot-Aktion, Zeitablauf oder Rundenwechsel).
    public IReadOnlyList<string> Tick(DateTimeOffset? now = null)
    {
        var instant = now ?? DateTimeOffset.UtcNow;
        var changed = new List<string>();
        foreach (var room in store.Rooms.Values)
        {
            lock (room.Gate)
            {
                if (!room.Started)
                {
                    var idleSince = room.CompletedAtUtc ?? room.CreatedAtUtc;
                    if (instant - idleSince > TimeSpan.FromHours(1)) store.Rooms.TryRemove(room.Code, out _);
                    continue;
                }
                var didChange = false;
                foreach (var player in room.PlayersByToken.Values)
                {
                    if (player.Finished) continue;
                    if (instant >= player.RoundStartedAtUtc.AddSeconds(GameRules.RoundDurationSeconds))
                    {
                        FinishPlayer(player, FinishReason.TimeExpired, instant);
                        didChange = true;
                        continue;
                    }

                    if (player.IsComputer && instant >= player.NextBotActionAtUtc)
                    {
                        RunBotStep(room, player, instant);
                        didChange = true;
                    }
                }

                if (StartNextRoundIfEveryoneReady(room, instant)) didChange = true;
                // Auch ohne Spielaktion wird jede Sekunde ein autoritativer Countdown publiziert.
                if (didChange || room.Started)
                {
                    room.Version++;
                    changed.Add(room.Code);
                }
            }
        }
        return changed;
    }


    private static void RunBotStep(
        GameRoom room,
        PlayerGameState bot,
        DateTimeOffset now)
    {
        if (bot.RerollsLeft > 0)
        {
            var counts = new int[6];

            foreach (var die in bot.Dice)
            {
                if (die < 6)
                    counts[die]++;
            }

            var bestSymbol = Array.IndexOf(counts, counts.Max());
            var keepPair = counts[bestSymbol] >= 2;
            var keep = new bool[5];

            for (var i = 0; i < 5; i++)
            {
                var die = bot.Dice[i];
                keep[i] = die == bestSymbol && keepPair;
            }

            bot.Held = keep;

            for (var i = 0; i < 5; i++)
            {
                if (!keep[i])
                    bot.Dice[i] = RandomNumberGenerator.GetInt32(0, 6);
            }

            bot.RerollsLeft--;
            bot.RollNumber++;

            if (bot.RerollsLeft == 0 &&
                GameRules.AvailableFieldScores(
                    bot.Dice,
                    bot.UsedScoreFields).Count == 0)
            {
                FinishPlayer(bot, FinishReason.NoValidMoves, now);
            }

            bot.NextBotActionAtUtc = now.AddSeconds(1);
            return;
        }

        var availableScores = GameRules.AvailableFieldScores(
            bot.Dice,
            bot.UsedScoreFields);

        var bestScore = availableScores
            .OrderByDescending(score => score.Points)
            .FirstOrDefault();

        if (bestScore is null)
        {
            FinishPlayer(bot, FinishReason.NoValidMoves, now);
            return;
        }

        BankInternal(room, bot, bestScore, now);
        bot.NextBotActionAtUtc = now.AddSeconds(1);
    }



    private static void BankInternal(
        GameRoom room,
        PlayerGameState player,
        FieldScore score,
        DateTimeOffset now)
    {
        if (player.UsedScoreFields.Contains(score.Field))
            throw new InvalidOperationException(
                "Dieses Wertungsfeld wurde bereits verwendet.");

        player.UsedScoreFields.Add(score.Field);

        player.ScoreFieldPoints[score.Field] = score.Points;

        var award = GameRules.ApplyRoundAward(
            player.RoundScore,
            score.Points,
            room.Round,
            GameRules.RemainingSeconds(player.RoundStartedAtUtc, now));

        player.RoundScore = award.NewRoundScore;
        player.TotalScore += score.Points + award.Bonus;
        player.LastAward = score.Points;

        player.LastCombination = string.IsNullOrWhiteSpace(score.JokerExplanation)
            ? score.Combination
            : $"{score.Combination} ({score.JokerExplanation})";

        if (award.TargetReached)
        {
            player.RoundBonus += award.Bonus;
        }

        StartNextTurn(player, now);
    }


    private static void StartRoom(GameRoom room, DateTimeOffset now)
    {
        room.Started = true;
        room.Round = 1;
        room.CompletedAtUtc = null;
        room.RoundStartedAtUtc = now;
        foreach (var player in room.PlayersByToken.Values)
        {
            player.TotalScore = 0;
            ResetForRound(player, now);
        }
        room.Version++;
    }

    private static void ResetForRound(PlayerGameState player, DateTimeOffset now)
    {
        player.RoundScore = 0;
        player.RoundBonus = 0;
        player.LastAward = 0;
        player.UsedScoreFields.Clear();
        player.ScoreFieldPoints.Clear();
        player.LastCombination = "";
        player.Finished = false;
        player.FinishReason = FinishReason.None;
        player.FinishedAtUtc = null;
        player.ReadyForNextRound = false;
        player.RoundStartedAtUtc = now;
        StartNextTurn(player, now);
    }
   

    private static void StartNextTurn(PlayerGameState player, DateTimeOffset now)
    {
        player.Dice = Enumerable.Range(0, 5)
            .Select(_ => RandomNumberGenerator.GetInt32(0, 6))
            .ToArray();
        player.Held = new bool[5];
        player.RerollsLeft = 2;
        player.Reroll1Used = false;
        player.Reroll2Used = false;
        player.RollNumber = 1;
        player.TurnId++;
        player.NextBotActionAtUtc = now.AddSeconds(1);
    }

    private static void FinishPlayer(
    PlayerGameState player,
    FinishReason reason,
    DateTimeOffset now)
    {
        player.Finished = true;
        player.FinishReason = reason;
        player.FinishedAtUtc = now;
        player.ReadyForNextRound = player.IsComputer;
    }

    private static bool StartNextRoundIfEveryoneReady(
    GameRoom room,
    DateTimeOffset now)
    {
        if (!room.Started || room.PlayersByToken.Count == 0)
            return false;

        // Alle Spieler müssen zuerst ihre Runde beendet haben.
        if (room.PlayersByToken.Values.Any(p => !p.Finished))
            return false;

        // Danach müssen alle Spieler auf "Weiter" gedrückt haben.
        if (room.PlayersByToken.Values.Any(p => !p.ReadyForNextRound))
            return false;

        // Nach Runde 5 ist das Spiel beendet.
        if (room.Round >= 5)
        {
            room.Started = false;
            room.CompletedAtUtc = now;
            room.Version++;
            return true;
        }

        // Nächste Runde gemeinsam starten.
        room.Round++;
        room.RoundStartedAtUtc = now;

        foreach (var player in room.PlayersByToken.Values)
        {
            ResetForRound(player, now);
        }

        room.Version++;
        return true;
    }

    private static void EnsureActive(GameRoom room, PlayerGameState player, DateTimeOffset now)
    {
        if (!room.Started) throw new InvalidOperationException("Das Spiel läuft derzeit nicht.");
        if (player.Finished) throw new InvalidOperationException("Deine Runde ist bereits beendet.");
        if (now >= player.RoundStartedAtUtc.AddSeconds(GameRules.RoundDurationSeconds))
        {
            FinishPlayer(player, FinishReason.TimeExpired, now);
            room.Version++;
            // Der Sekunden-Takt führt den gemeinsamen Rundenwechsel aus und publiziert ihn.
            throw new InvalidOperationException("Die Zeit für diese Runde ist abgelaufen.");
        }
    }

    private static bool TryGetDuplicate(PlayerGameState player, Guid actionId, out ActionResult result)
    {
        if (actionId == Guid.Empty) throw new InvalidOperationException("Eine gültige Aktions-ID ist erforderlich.");
        if (player.RecentActions.TryGetValue(actionId, out var previous))
        {
            result = previous;
            return true;
        }
        result = null!;
        return false;
    }

    private static void Remember(PlayerGameState player, Guid actionId, ActionResult result)
    {
        player.RecentActions[actionId] = result;
        player.RecentActionOrder.Enqueue(actionId);
        while (player.RecentActionOrder.Count > 100)
        {
            var oldest = player.RecentActionOrder.Dequeue();
            player.RecentActions.Remove(oldest);
        }
    }

    private static PlayerGameState NewPlayer(string token, string name, string? connectionId, bool computer, int seatIndex)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? (computer ? "Computer" : "Spieler") : name.Trim();
        if (cleanName.Length > 20) cleanName = cleanName[..20];
        return new PlayerGameState
        {
            Token = token,
            Name = cleanName,
            ConnectionId = connectionId,
            IsConnected = connectionId is not null,
            IsComputer = computer,
            SeatIndex = seatIndex,
            RoundStartedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static int PlayerRemaining(PlayerGameState player, DateTimeOffset now) =>
        GameRules.RemainingSeconds(player.RoundStartedAtUtc, player.FinishedAtUtc ?? now);

    private static void EnsureConnection(PlayerGameState player, string connectionId)
    {
        if (!player.IsConnected || !string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal))
            throw new InvalidOperationException("Diese Verbindung ist nicht mehr dem Spielerplatz zugeordnet. Bitte erneut verbinden.");
    }

    private static PlayerGameState GetPlayer(GameRoom room, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !room.PlayersByToken.TryGetValue(token, out var player))
            throw new InvalidOperationException("Du bist diesem Raum nicht zugeordnet.");
        return player;
    }

    private GameRoom GetRoom(string code)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        if (!store.Rooms.TryGetValue(normalized, out var room)) throw new InvalidOperationException("Raum nicht gefunden.");
        return room;
    }

    private static RoomState BuildState(GameRoom room, PlayerGameState player, DateTimeOffset now) =>
        new(BuildPublicState(room, now), BuildPrivateState(room, player, now), room.OwnerToken == player.Token,
            player.SeatIndex);

    private static PublicRoomState BuildPublicState(GameRoom room, DateTimeOffset now)
    {
        var players = room.PlayersByToken.Values.OrderBy(p => p.SeatIndex).Select(p => new PublicPlayerState(
            p.Name, p.TotalScore, p.RoundScore, p.Finished, p.FinishReason, p.IsComputer,
            p.IsConnected,
            room.RoundStartedAtUtc.HasValue
                ? PlayerRemaining(p, now)
                : GameRules.RoundDurationSeconds,
            p.LastAward,
            p.RoundBonus,
            p.ReadyForNextRound)).ToArray();
        return new PublicRoomState(room.Code, room.Mode.ToString(), room.Round,
            room.Started ? GameRules.TargetForRound(room.Round) : GameRules.TargetForRound(Math.Clamp(room.Round, 1, 5)),
            room.Started, room.Version, room.RoundStartedAtUtc, players);
    }


    private static PrivatePlayerState BuildPrivateState(
        GameRoom room,
        PlayerGameState player,
        DateTimeOffset now)
    {
        var availableScores = room.RoundStartedAtUtc.HasValue
            ? GameRules.AvailableFieldScores(
                player.Dice,
                player.UsedScoreFields)
            : Array.Empty<FieldScore>();

        var best = availableScores
            .OrderByDescending(score => score.Points)
            .FirstOrDefault();

        return new PrivatePlayerState(
            player.Name,
            player.Dice.ToArray(),
            player.Held.ToArray(),
            player.RerollsLeft,
            player.Reroll1Used,
            player.Reroll2Used,
            player.RollNumber,
            player.TurnId,
            player.RoundScore,
            player.TotalScore,
            player.RoundBonus,
            player.LastAward,
            player.LastCombination,
            best?.Points ?? 0,
            best?.Combination ?? "Keine freie gültige Wertung",
            best?.JokerExplanation ?? "",
            player.Finished,
            player.FinishReason,
            room.RoundStartedAtUtc.HasValue
                ? PlayerRemaining(player, now)
                : GameRules.RoundDurationSeconds,
            availableScores,
            player.UsedScoreFields.ToArray(),
            new Dictionary<ScoreField, int>(player.ScoreFieldPoints));
    }


    private static string NewRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Concat(Enumerable.Range(0, 6).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

public sealed record JoinInfo(string Code, string Token, string PlayerName, GameMode Mode);
public sealed record LeaveRoomResult(string Code, bool RoomRemoved);
public sealed record ConnectedPlayer(string ConnectionId, string Token);
public sealed record RoomState(PublicRoomState Public, PrivatePlayerState Me, bool IsOwner, int MyPlayerIndex);
public sealed record PublicRoomState(string Code, string Mode, int Round, int Target, bool Started,
    long Version, DateTimeOffset? RoundStartedAtUtc, IReadOnlyList<PublicPlayerState> Players);
public sealed record PublicPlayerState(
    string Name,
    int TotalScore,
    int RoundScore,
    bool Finished,
    FinishReason FinishReason,
    bool IsComputer,
    bool IsConnected,
    int RemainingSeconds,
    int LastAward,
    int RoundBonus,
    bool ReadyForNextRound);

public sealed record PrivatePlayerState(
    string Name,
    int[] Dice,
    bool[] Held,
    int RerollsLeft,
    bool Reroll1Used,
    bool Reroll2Used,
    int RollNumber,
    int TurnId,
    int RoundScore,
    int TotalScore,
    int RoundBonus,
    int LastAward,
    string LastCombination,
    int BestPossiblePoints,
    string BestPossibleCombination,
    string BestPossibleJokerUse,
    bool Finished,
    FinishReason FinishReason,
    int RemainingSeconds,
    IReadOnlyList<FieldScore> AvailableFieldScores,
    ScoreField[] UsedScoreFields,
    Dictionary<ScoreField, int> ScoreFieldPoints);

