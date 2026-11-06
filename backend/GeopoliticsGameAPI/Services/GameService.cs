using GeopoliticsGameAPI.Data;
using GeopoliticsGameAPI.Models;
using MongoDB.Driver;


namespace GeopoliticsGameAPI.Services
{
    public class GameService
    {

        private readonly MongoDbContext _context;
        private readonly BattleCommentaryService _battleCommentaryService;

        public GameService(
            MongoDbContext context,
            BattleCommentaryService battleCommentaryService)
        {
            _context = context;
            _battleCommentaryService = battleCommentaryService;
        }

        public async Task<Game> CreateGameAsync(string playerCountryId, string ownerPlayerId)
        {
            //Find the country selected by the player
            var playerCountry = await _context.Countries
                .Find(country => country.Id == playerCountryId)
                .FirstOrDefaultAsync();

            if (playerCountry == null)
            {
                throw new ArgumentException("The selected country does not exist");
            }


            //Load the available country catalogue
            var allCountries = await _context.Countries
                .Find(_ => true)
                .ToListAsync();

            if (allCountries.Count < 10)
            {
                throw new InvalidOperationException("At least 10 countries are required to create a game");
            }


            //Randomly select nine countries, excluding the player's country
            var otherCountries = allCountries
                .Where(Country => Country.Id != playerCountryId)
                .OrderBy(_ => Random.Shared.Next())
                .Take(9)
                .ToList();

            //Put the player and nine NPC countries into the game
            var selectedCountries = new List<Country> { playerCountry };
            selectedCountries.AddRange(otherCountries);

            var game = new Game
            {
                PlayerCountryId = playerCountryId,
                OwnerPlayerId = ownerPlayerId,
                Round = 1,
                Status = "InProgress",
                Countries = selectedCountries.Select(country =>
                {
                    var startingSoldiers = GameRules.CalculateStartingSoldiers(country.PopulationMillions);

                    return new GameCountry
                    {
                        CountryId = country.Id!,
                        Name = country.Name,
                        Continent = country.Continent,
                        PopulationMillions = country.PopulationMillions,
                        StartingSoldiers = startingSoldiers,
                        Soldiers = startingSoldiers,
                        IsHumanPlayer = country.Id == playerCountryId,
                        ControllerCountryId = country.Id!,
                        IsEliminated = false
                    };
                }).ToList()
            };


            //Randomise the order of the ten original controllers
            game.TurnOrder = selectedCountries
                .Select(country => country.Id!)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            game.CurrentTurnIndex = 0;
            game.CurrentTurnControllerId = game.TurnOrder[0];
            game.HasAttackedThisTurn = false;

            // Save the new game first so MongoDB assigns its ID.
            await _context.Games.InsertOneAsync(game);

            // Find the country scheduled to take the first turn.
            var firstController = game.Countries.First(
                country => country.CountryId == game.CurrentTurnControllerId);

            // Record the creation of the game.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "GameCreated",
                Round = game.Round,
                ActorCountryId = firstController.CountryId,
                ActorCountryName = firstController.Name,
                Message = $"A new game has started. {firstController.Name} " +
                          "has the first turn.",
                CreatedAt = DateTime.UtcNow
            });

            // Save the updated game with its initial event.
            await _context.Games.ReplaceOneAsync(
                existingGame => existingGame.Id == game.Id,
                game);

            // If an NPC goes first, let it play its turn straight away.
            if (game.CurrentTurnControllerId != game.PlayerCountryId)
            {
                return await PlayNpcTurnAsync(game.Id!) ?? game;
            }

            return game;
        }


        public async Task<Game?> GetGameAsync(string id)
        {
            return await _context.Games
                .Find(game => game.Id == id)
                .FirstOrDefaultAsync();
        }


        public async Task<GameStatistics?> GetGameStatisticsAsync(string id)
        {
            var game = await GetGameAsync(id);

            if (game == null)
            {
                return null;
            }

            var playerCountry = game.Countries
                .FirstOrDefault(c => c.CountryId == game.PlayerCountryId);

            var controlledCountries = game.Countries
                .Where(c =>
                    c.ControllerCountryId == game.PlayerCountryId &&
                    !c.IsEliminated)
                .ToList();

            var playerBattleRounds = game.Events
                .Where(e => e.EventType == "PlayerBattleRound")
                .ToList();

            int battlesWon = 0;
            int battlesLost = 0;

            foreach (var battle in playerBattleRounds)
            {
                int wins = 0;
                int losses = 0;

                int comparisons = Math.Min(
                    battle.AttackerDice.Count,
                    battle.DefenderDice.Count
                );

                for (int i = 0; i < comparisons; i++)
                {
                    if (battle.AttackerDice[i] > battle.DefenderDice[i])
                    {
                        wins++;
                    }
                    else if (battle.DefenderDice[i] > battle.AttackerDice[i])
                    {
                        losses++;
                    }
                }

                if (wins > losses)
                {
                    battlesWon++;
                }
                else if (losses > wins)
                {
                    battlesLost++;
                }
            }

            // Reconstruct country ownership using completed conquests.
            var ownership = game.Countries.ToDictionary(
                c => c.CountryId,
                c => c.CountryId
            );

            var conqueredCountryIds = new HashSet<string>();
            var lostCountryIds = new HashSet<string>();

            // Use completed conquest events rather than battle rounds.
            // This prevents the same conquest from being counted twice.
            var conquestEvents = game.Events
                .Where(e =>
                    e.EventType == "PlayerConquest" ||
                    e.EventType == "NpcConquest")
                .OrderBy(e => e.CreatedAt)
                .ToList();

            foreach (var conquest in conquestEvents)
            {
                if (!ownership.ContainsKey(conquest.ActorCountryId) ||
                    !ownership.ContainsKey(conquest.TargetCountryId))
                {
                    continue;
                }

                // Identify the controller of the attacking territory.
                string attackingController = ownership[conquest.ActorCountryId];

                // Identify who controlled the target before the conquest.
                string previousController = ownership[conquest.TargetCountryId];

                // Count territories conquered by the human player.
                if (attackingController == game.PlayerCountryId)
                {
                    conqueredCountryIds.Add(conquest.TargetCountryId);
                }

                // Count territories lost by the human player.
                if (previousController == game.PlayerCountryId &&
                    attackingController != game.PlayerCountryId)
                {
                    lostCountryIds.Add(conquest.TargetCountryId);
                }

                // Transfer ownership of this territory only.
                ownership[conquest.TargetCountryId] = attackingController;
            }

            return new GameStatistics
            {
                GameId = game.Id ?? string.Empty,
                PlayerCountryName = playerCountry?.Name ?? "Unknown",
                Round = game.Round,
                Status = game.Status,
                Outcome = game.Outcome,
                CountriesControlled = controlledCountries.Count,
                TotalSoldiers = controlledCountries.Sum(c => c.Soldiers),
                CountriesConquered = conqueredCountryIds.Count,
                CountriesLost = lostCountryIds.Count,
                BattlesWon = battlesWon,
                BattlesLost = battlesLost,
                TotalBattleRounds = playerBattleRounds.Count
            };
        }


        public async Task<List<Game>> GetPlayerGamesAsync(string ownerPlayerId)
        {
            return await _context.Games
                .Find(game => game.OwnerPlayerId == ownerPlayerId)
                .SortByDescending(game => game.CreatedAt)
                .ToListAsync();
        }


        public async Task<Game?> AdvanceTurnAsync(string gameId)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                return game;
            }

            // Do not advance while a battle is still in progress.
            if (game.BattleInProgress)
            {
                throw new InvalidOperationException(
                    "Finish or stop the current battle before advancing your turn.");
            }

            // The conquest transfer must be completed first.
            if (game.ConquestTransferPending)
            {
                throw new InvalidOperationException(
                    "Complete the conquest soldier transfer before advancing your turn.");
            }

            // The human player must allocate all available reinforcements.
            if (game.CurrentTurnControllerId == game.PlayerCountryId &&
                game.ReinforcementsAvailable > 0)
            {
                throw new InvalidOperationException(
                    "Allocate your reinforcement soldiers before advancing your turn.");
            }

            if (game.TurnOrder == null || game.TurnOrder.Count == 0)
            {
                throw new InvalidOperationException(
                    "This game has no turn order. Create a new game.");
            }

            // Check whether the game has ended before advancing the turn.
            FinishGameIfOver(game);

            if (game.Status != "InProgress")
            {
                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                return game;
            }


            int numberOfControllers = game.TurnOrder.Count;

            // Search for the next controller that still owns a country.
            for (int step = 1; step <= numberOfControllers; step++)
            {
                int rawIndex = game.CurrentTurnIndex + step;
                int nextIndex = rawIndex % numberOfControllers;

                // A new round begins when the turn order wraps around.
                bool roundIsAdvancing = rawIndex >= numberOfControllers;

                string nextControllerId = game.TurnOrder[nextIndex];

                bool controllerIsActive = game.Countries.Any(
                    country =>
                        country.ControllerCountryId == nextControllerId &&
                        !country.IsEliminated);

                // Skip controllers that have lost all their countries.
                if (!controllerIsActive)
                {
                    continue;
                }

                // Advance the round only when an active controller starts the new round.
                if (roundIsAdvancing)
                {
                    game.Round++;
                }

                // Set the next active controller.
                game.CurrentTurnIndex = nextIndex;
                game.CurrentTurnControllerId = nextControllerId;

                // Reset turn-specific state.
                game.HasAttackedThisTurn = false;
                game.ReinforcementsAvailable = 0;

                // Record the start of this controller's turn.
                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "TurnStarted",
                    Round = game.Round,
                    ActorCountryId = nextControllerId,
                    ActorCountryName = PlayerName(game, nextControllerId),
                    Message = $"{PlayerName(game, nextControllerId)}'s turn has started.",
                    CreatedAt = DateTime.UtcNow
                });


                // Reinforcements begin in Round 2.
                if (game.Round >= 2)
                {
                    var controlledCountries = game.Countries
                        .Where(country =>
                            country.ControllerCountryId == nextControllerId &&
                            !country.IsEliminated)
                        .ToList();

                    int reinforcementCount = controlledCountries.Count * 2;

                    if (controlledCountries.Count > 0)
                    {
                        // The human player allocates these manually;
                        // NPCs allocate them inside PlayNpcTurnAsync.
                        game.ReinforcementsAvailable = reinforcementCount;
                    }
                }


                // Save the updated turn and reinforcement state.
                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                // If the next controller is an NPC, let it play automatically.
                if (nextControllerId != game.PlayerCountryId)
                {
                    return await PlayNpcTurnAsync(gameId);
                }

                // If it is the human player's turn, return the game.
                return game;
            }

            // No active controller could be found in the turn order.
            game.Status = "Completed";

            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId,
                game);

            return game;
        }


        public async Task<Game?> PlayNpcTurnAsync(string gameId)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                return game;
            }

            if (game.CurrentTurnControllerId == game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "It is the human player's turn. NPCs cannot act now.");
            }

            if (game.BattleInProgress || game.ConquestTransferPending)
            {
                throw new InvalidOperationException(
                    "Resolve the current battle or conquest transfer first.");
            }

            string npcName = PlayerName(game, game.CurrentTurnControllerId);

            // Identify all countries controlled by the current NPC.
            var npcCountries = game.Countries
                .Where(country =>
                    country.ControllerCountryId == game.CurrentTurnControllerId &&
                    !country.IsEliminated)
                .ToList();

            // Record the beginning of the NPC's turn.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "NpcTurnStarted",
                Round = game.Round,
                ActorCountryId = game.CurrentTurnControllerId,
                ActorCountryName = npcName,
                Message = npcCountries.Count > 0
                    ? $"{npcName} begins its turn with " +
                      $"{npcCountries.Count} controlled countries."
                    : "An eliminated NPC has no countries left to control.",
                CreatedAt = DateTime.UtcNow
            });

            // An eliminated NPC cannot take any actions.
            if (npcCountries.Count == 0)
            {
                FinishGameIfOver(game);

                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                return game;
            }

            // Allocate available reinforcements.
            if (game.ReinforcementsAvailable > 0)
            {
                AllocateNpcReinforcements(game);
            }

            // Redistribute soldiers between NPC-controlled territories.
            TransferNpcSoldiers(game);

            // Refresh the NPC's territories after military adjustments.
            npcCountries = game.Countries
                .Where(country =>
                    country.ControllerCountryId == game.CurrentTurnControllerId &&
                    !country.IsEliminated)
                .ToList();

            // Identify all possible attacking countries.
            var possibleAttackers = npcCountries
                .Where(country => country.Soldiers >= 2)
                .ToList();

            // Identify all active enemy countries.
            var possibleDefenders = game.Countries
                .Where(country =>
                    country.ControllerCountryId != game.CurrentTurnControllerId &&
                    !country.IsEliminated &&
                    country.Soldiers >= 1)
                .ToList();

            // Check whether an attack is possible.
            if (possibleAttackers.Count == 0 || possibleDefenders.Count == 0)
            {
                string reason;

                if (possibleAttackers.Count == 0 &&
                    possibleDefenders.Count == 0)
                {
                    reason = "It has no country with enough soldiers to attack, " +
                             "and no enemy country is available.";
                }
                else if (possibleAttackers.Count == 0)
                {
                    reason = "It has no country with at least two soldiers to attack.";
                }
                else
                {
                    reason = "There are no enemy countries available to attack.";
                }

                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcAttackSkipped",
                    Round = game.Round,
                    ActorCountryId = game.CurrentTurnControllerId,
                    ActorCountryName = npcName,
                    Message = $"{npcName} decided not to attack. " + reason,
                    CreatedAt = DateTime.UtcNow
                });

                FinishGameIfOver(game);

                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                return game;
            }

            // Evaluate every possible attacking and defending combination.
            var attackOptions = possibleAttackers
                .SelectMany(attacker => possibleDefenders
                    .Where(defender => attacker.Soldiers > defender.Soldiers)
                    .Select(defender =>
                    {
                        // Count attacks against this territory during the current round.
                        int attacksThisRound = game.Events.Count(gameEvent =>
                            gameEvent.EventType == "NpcAttackDecision" &&
                            gameEvent.TargetCountryId == defender.CountryId &&
                            gameEvent.Round == game.Round);

                        // Also consider attacks during the previous round.
                        int attacksPreviousRound = game.Events.Count(gameEvent =>
                            gameEvent.EventType == "NpcAttackDecision" &&
                            gameEvent.TargetCountryId == defender.CountryId &&
                            gameEvent.Round == game.Round - 1);

                        // Calculate the military advantage.
                        double militaryScore =
                            (attacker.Soldiers - defender.Soldiers) * 2.0 +
                            ((double)attacker.Soldiers / defender.Soldiers) * 5.0;

                        // Penalise targets that other NPCs have recently attacked.
                        double attackPressurePenalty =
                            (attacksThisRound * 8.0) +
                            (attacksPreviousRound * 3.0);

                        return new
                        {
                            Attacker = attacker,
                            Defender = defender,
                            MilitaryScore = militaryScore,
                            AttacksThisRound = attacksThisRound,
                            AttacksPreviousRound = attacksPreviousRound,
                            Score = militaryScore - attackPressurePenalty
                        };
                    }))
                .OrderByDescending(option => option.Score)
                .ToList();

            // Do not attack if no favourable or strategically worthwhile target exists.
            if (attackOptions.Count == 0 || attackOptions.First().Score <= 0)
            {
                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcAttackSkipped",
                    Round = game.Round,
                    ActorCountryId = game.CurrentTurnControllerId,
                    ActorCountryName = npcName,
                    Message = attackOptions.Count == 0
                        ? $"{npcName} evaluated " +
                          $"{possibleAttackers.Count} possible attackers and " +
                          $"{possibleDefenders.Count} enemy territories but decided " +
                          "not to attack because none offered a numerical advantage."
                        : $"{npcName} decided not to attack. " +
                          "Although some targets offered a military advantage, their " +
                          "recent attack pressure made them less attractive strategic targets.",
                    CreatedAt = DateTime.UtcNow
                });

                FinishGameIfOver(game);

                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                return game;
            }

            // Select the attack with the highest strategic score.
            var selectedAttack = attackOptions.First();

            var attacker = selectedAttack.Attacker;
            var defender = selectedAttack.Defender;

            // Explain the NPC's target selection.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "NpcAttackDecision",
                Round = game.Round,
                ActorCountryId = attacker.CountryId,
                ActorCountryName = attacker.Name,
                TargetCountryId = defender.CountryId,
                TargetCountryName = defender.Name,
                Message =
                    $"{attacker.Name} decided to attack {defender.Name}. " +

                    $"Military assessment: {attacker.Name} has " +
                    $"{attacker.Soldiers} soldiers, while {defender.Name} " +
                    $"has {defender.Soldiers}. This gives the attacker " +
                    $"a numerical advantage of " +
                    $"{attacker.Soldiers - defender.Soldiers} soldiers " +
                    $"and a strength ratio of " +
                    $"{(double)attacker.Soldiers / defender.Soldiers:F2}:1. " +

                    $"The NPC evaluated {attackOptions.Count} possible " +
                    $"attack combinations and selected this target because " +
                    $"it achieved the highest strategic score. " +

                    $"Military advantage score: " +
                    $"{selectedAttack.MilitaryScore:F1}. " +

                    $"Recent attack pressure: " +
                    $"{selectedAttack.AttacksThisRound} attack(s) this round " +
                    $"and {selectedAttack.AttacksPreviousRound} " +
                    $"attack(s) in the previous round. " +

                    $"The resulting strategic score was " +
                    $"{selectedAttack.Score:F1}.",

                CreatedAt = DateTime.UtcNow
            });

            game.HasAttackedThisTurn = true;
            game.BattleInProgress = true;
            game.ActiveAttackerCountryId = attacker.CountryId;
            game.ActiveDefenderCountryId = defender.CountryId;

            // Limit NPC battles to ten rounds.
            const int maximumBattleRounds = 10;

            bool battleLimitReached = true;

            for (int round = 0; round < maximumBattleRounds; round++)
            {
                if (attacker.Soldiers <= 1 || defender.Soldiers <= 0)
                {
                    battleLimitReached = false;
                    break;
                }

                // Determine the number of dice for this battle round.
                int diceCount = Math.Min(
                    attacker.Soldiers - 1,
                    defender.Soldiers);

                if (diceCount <= 0)
                {
                    battleLimitReached = false;
                    break;
                }

                // Roll the attacker's dice (paired by position, not sorted).
                var attackerDice = Enumerable.Range(0, diceCount)
                    .Select(_ => Random.Shared.Next(1, 7))
                    .ToList();

                // Roll the defender's dice.
                var defenderDice = Enumerable.Range(0, diceCount)
                    .Select(_ => Random.Shared.Next(1, 7))
                    .ToList();

                int attackerLosses = 0;
                int defenderLosses = 0;

                // Compare matching dice. Ties cause no casualties.
                for (int i = 0; i < diceCount; i++)
                {
                    if (attackerDice[i] > defenderDice[i])
                    {
                        defenderLosses++;
                    }
                    else if (defenderDice[i] > attackerDice[i])
                    {
                        attackerLosses++;
                    }
                }

                // Apply casualties.
                attacker.Soldiers -= attackerLosses;
                defender.Soldiers -= defenderLosses;

                bool conquered = defender.Soldiers == 0;

                // Record the result of the NPC battle round.
                var npcBattleEvent = new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcBattleRound",
                    Round = game.Round,
                    ActorCountryId = attacker.CountryId,
                    ActorCountryName = attacker.Name,
                    TargetCountryId = defender.CountryId,
                    TargetCountryName = defender.Name,
                    AttackerDice = attackerDice,
                    DefenderDice = defenderDice,
                    AttackerLosses = attackerLosses,
                    DefenderLosses = defenderLosses,
                    WasConquered = conquered,
                    Message = conquered
                        ? $"{attacker.Name} rolled [{string.Join(", ", attackerDice)}] " +
                          $"against {defender.Name}'s " +
                          $"[{string.Join(", ", defenderDice)}]. " +
                          $"{attacker.Name} lost {attackerLosses} soldiers and " +
                          $"{defender.Name} lost {defenderLosses}. " +
                          $"{defender.Name} was conquered."
                        : $"{attacker.Name} rolled [{string.Join(", ", attackerDice)}] " +
                          $"against {defender.Name}'s " +
                          $"[{string.Join(", ", defenderDice)}]. " +
                          $"{attacker.Name} lost {attackerLosses} soldiers; " +
                          $"{defender.Name} lost {defenderLosses}.",
                    CreatedAt = DateTime.UtcNow
                };

                npcBattleEvent.Commentary =
                    await _battleCommentaryService.GenerateCommentaryAsync(
                        attacker.Name,
                        defender.Name,
                        attackerDice,
                        defenderDice,
                        attackerLosses,
                        defenderLosses,
                        conquered
                    );

                game.Events.Add(npcBattleEvent);

                // Handle a successful conquest.
                if (conquered)
                {
                    string previousControllerId = defender.ControllerCountryId;

                    // Transfer control to the attacking country's controller.
                    defender.ControllerCountryId = attacker.ControllerCountryId;

                    // Preserve whether the attacking territory belongs to the human player.
                    defender.IsHumanPlayer = attacker.IsHumanPlayer;

                    defender.IsEliminated = false;

                    // Check whether the previous controller has been eliminated.
                    bool previousControllerStillHasCountries = game.Countries.Any(
                        country =>
                            country.ControllerCountryId == previousControllerId &&
                            !country.IsEliminated);

                    if (!previousControllerStillHasCountries)
                    {
                        game.Events.Add(new GameEvent
                        {
                            GameId = game.Id!,
                            EventType = "CountryEliminated",
                            Round = game.Round,
                            ActorCountryId = attacker.CountryId,
                            ActorCountryName = attacker.Name,
                            TargetCountryId = previousControllerId,
                            TargetCountryName = PlayerName(game, previousControllerId),
                            Message =
    $"{PlayerName(game, previousControllerId)} has been eliminated. " +
    $"{attacker.Name} carried out the final conquest for " +
    $"{PlayerName(game, attacker.ControllerCountryId)}'s empire.",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Calculate the conquest bonus.
                    int conquestBonus = Math.Max(
                        2,
                        defender.StartingSoldiers / 2);

                    // Award the conquest bonus first, so a transfer is always possible.
                    attacker.Soldiers += conquestBonus;

                    // Move a real garrison into the conquered country,
                    // leaving at least one soldier behind.
                    int soldiersToTransfer = Math.Max(1,
                        Math.Min(attacker.Soldiers - 1, Math.Max(3, defender.StartingSoldiers)));

                    attacker.Soldiers -= soldiersToTransfer;
                    defender.Soldiers += soldiersToTransfer;

                    game.Events.Add(new GameEvent
                    {
                        GameId = game.Id!,
                        EventType = "NpcConquest",
                        Round = game.Round,
                        ActorCountryId = attacker.CountryId,
                        ActorCountryName = attacker.Name,
                        TargetCountryId = defender.CountryId,
                        TargetCountryName = defender.Name,
                        WasConquered = true,
                        Message =
    attacker.ControllerCountryId == attacker.CountryId
        ? $"{attacker.Name} conquered {defender.Name}, " +
          $"transferred {soldiersToTransfer} soldiers into it, " +
          $"and received {conquestBonus} bonus soldiers."
        : $"{attacker.Name}, operating under " +
          $"{PlayerName(game, attacker.ControllerCountryId)}'s control, " +
          $"conquered {defender.Name}. " +
          $"{defender.Name} is now part of " +
          $"{PlayerName(game, attacker.ControllerCountryId)}'s empire. " +
          $"{soldiersToTransfer} soldiers were transferred and " +
          $"{conquestBonus} bonus soldiers were awarded.",
                        CreatedAt = DateTime.UtcNow
                    });

                    // Resolve the NPC's conquest transfer automatically.
                    game.ConquestTransferPending = false;
                    game.PendingConquestCountryId = string.Empty;
                    game.PendingConquestAttackerCountryId = string.Empty;
                    game.ConquestBonusAvailable = 0;

                    battleLimitReached = false;
                    break;
                }

                // Reassess the battle after every round.
                // Stop if the NPC no longer has a numerical advantage.
                if (attacker.Soldiers <= defender.Soldiers)
                {
                    game.Events.Add(new GameEvent
                    {
                        GameId = game.Id!,
                        EventType = "NpcAttackStopped",
                        Round = game.Round,
                        ActorCountryId = attacker.CountryId,
                        ActorCountryName = attacker.Name,
                        TargetCountryId = defender.CountryId,
                        TargetCountryName = defender.Name,
                        Message =
                            $"{attacker.Name} stopped attacking {defender.Name} " +
                            $"because its remaining military strength " +
                            $"({attacker.Soldiers} soldiers) was no longer greater " +
                            $"than the defender's strength ({defender.Soldiers} " +
                            "soldiers). The NPC decided that continuing would " +
                            "expose its remaining forces to unnecessary losses.",
                        CreatedAt = DateTime.UtcNow
                    });

                    battleLimitReached = false;
                    break;
                }

                // Stop if the attacker has only one soldier remaining.
                if (attacker.Soldiers <= 1)
                {
                    game.Events.Add(new GameEvent
                    {
                        GameId = game.Id!,
                        EventType = "NpcAttackStopped",
                        Round = game.Round,
                        ActorCountryId = attacker.CountryId,
                        ActorCountryName = attacker.Name,
                        TargetCountryId = defender.CountryId,
                        TargetCountryName = defender.Name,
                        Message =
                            $"{attacker.Name} stopped attacking because it " +
                            "has only one soldier remaining.",
                        CreatedAt = DateTime.UtcNow
                    });

                    battleLimitReached = false;
                    break;
                }
            }

            // Explain why the NPC stopped if it reached the battle limit.
            if (battleLimitReached)
            {
                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcAttackStopped",
                    Round = game.Round,
                    ActorCountryId = attacker.CountryId,
                    ActorCountryName = attacker.Name,
                    TargetCountryId = defender.CountryId,
                    TargetCountryName = defender.Name,
                    Message =
                        $"{attacker.Name} stopped attacking {defender.Name} " +
                        $"after reaching the maximum of {maximumBattleRounds} " +
                        "battle rounds.",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Clear active battle information.
            game.BattleInProgress = false;
            game.ActiveAttackerCountryId = string.Empty;
            game.ActiveDefenderCountryId = string.Empty;

            // Check whether the game has ended (writes the GameCompleted event).
            FinishGameIfOver(game);

            // Save all NPC decisions, battles, conquests and outcomes.
            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId,
                game);

            return game;
        }


        public async Task<Game?> AllocateReinforcementsAsync(
            string gameId,
            List<ReinforcementAllocation> allocations)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                throw new InvalidOperationException(
                    "This game is not in progress.");
            }

            if (game.CurrentTurnControllerId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "It is not the human player's turn.");
            }

            if (game.ReinforcementsAvailable <= 0)
            {
                throw new InvalidOperationException(
                    "There are no reinforcement soldiers to allocate.");
            }

            if (allocations == null || allocations.Count == 0)
            {
                throw new ArgumentException(
                    "Provide at least one reinforcement allocation.");
            }

            if (allocations.Any(allocation =>
                allocation == null || allocation.Soldiers <= 0))
            {
                throw new ArgumentException(
                    "Each allocation must contain at least one soldier.");
            }

            if (allocations.Select(a => a.CountryId).Distinct().Count()
                != allocations.Count)
            {
                throw new ArgumentException(
                    "Each country can appear only once in the allocations.");
            }

            int totalSoldiers = allocations.Sum(a => a.Soldiers);

            if (totalSoldiers != game.ReinforcementsAvailable)
            {
                throw new ArgumentException(
                    $"You must allocate exactly {game.ReinforcementsAvailable} soldiers.");
            }

            var allocationSummary = new List<string>();

            foreach (var allocation in allocations)
            {
                var country = game.Countries.FirstOrDefault(country =>
                    country.CountryId == allocation.CountryId &&
                    country.ControllerCountryId == game.PlayerCountryId &&
                    !country.IsEliminated);

                if (country == null)
                {
                    throw new ArgumentException(
                        "You can only reinforce countries you control.");
                }

                country.Soldiers += allocation.Soldiers;
                allocationSummary.Add($"{allocation.Soldiers} to {country.Name}");
            }

            // Record the allocation in the game history.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "ReinforcementsAllocated",
                Round = game.Round,
                ActorCountryId = game.PlayerCountryId,
                ActorCountryName = PlayerName(game, game.PlayerCountryId),
                Message = $"{PlayerName(game, game.PlayerCountryId)} allocated " +
                          $"{totalSoldiers} reinforcement soldiers: " +
                          $"{string.Join(", ", allocationSummary)}.",
                CreatedAt = DateTime.UtcNow
            });

            game.ReinforcementsAvailable = 0;

            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId, game);

            return game;
        }



        private void AllocateNpcReinforcements(Game game)
        {
            string npcCountryId = game.CurrentTurnControllerId;

            // Find all territories controlled by this NPC.
            var npcCountries = game.Countries
                .Where(country =>
                    country.ControllerCountryId == npcCountryId &&
                    !country.IsEliminated)
                .ToList();

            // Check whether reinforcements can be allocated.
            if (npcCountries.Count == 0 ||
                game.ReinforcementsAvailable <= 0)
            {
                return;
            }

            int reinforcements = game.ReinforcementsAvailable;

            // Calculate the average military strength of the NPC's territories.
            double averageSoldiers = npcCountries.Average(
                country => country.Soldiers);

            // Evaluate each territory's need for reinforcements.
            var reinforcementOptions = npcCountries
                .Select(country => new
                {
                    Country = country,

                    // Territories below the NPC's average strength
                    // receive a higher priority.
                    Score = averageSoldiers - country.Soldiers
                })
                .OrderByDescending(option => option.Score)
                .ToList();

            // Select the territory that needs the most support.
            var targetCountry = reinforcementOptions.First().Country;

            // Allocate all available reinforcements.
            targetCountry.Soldiers += reinforcements;

            // Record the NPC's reinforcement decision.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "NpcReinforcementsAllocated",
                Round = game.Round,
                ActorCountryId = targetCountry.CountryId,
                ActorCountryName = targetCountry.Name,
                Message =
                    $"{targetCountry.Name} received {reinforcements} reinforcement " +
                    $"soldiers. The NPC selected this territory because it had " +
                    $"{targetCountry.Soldiers - reinforcements} soldiers before " +
                    $"reinforcement, making it one of the weakest territories " +
                    $"under its control. The NPC's average territorial strength " +
                    $"was {averageSoldiers:F1} soldiers. Strengthening this " +
                    "territory helps balance its overall military forces.",
                CreatedAt = DateTime.UtcNow
            });

            // All available reinforcements have been allocated.
            game.ReinforcementsAvailable = 0;
        }



        public async Task<Game?> TransferConquestSoldiersAsync(
            string gameId,
            int soldiersToTransfer)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                throw new InvalidOperationException(
                    "This game is not in progress");
            }

            if (game.CurrentTurnControllerId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "It is not the human player's turn");
            }

            if (!game.ConquestTransferPending)
            {
                throw new InvalidOperationException(
                    "There is no pending conquest transfer.");
            }

            var attacker = game.Countries.FirstOrDefault(country =>
                country.CountryId == game.PendingConquestAttackerCountryId);

            var conqueredCountry = game.Countries.FirstOrDefault(country =>
                country.CountryId == game.PendingConquestCountryId);

            if (attacker == null || conqueredCountry == null)
            {
                throw new InvalidOperationException(
                    "The attacking or conquered country could not be found");
            }

            if (attacker.ControllerCountryId != game.PlayerCountryId ||
                conqueredCountry.ControllerCountryId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "Both countries must be controlled by the human player");
            }

            if (soldiersToTransfer < 1)
            {
                throw new ArgumentException(
                    "You must transfer at least one soldier");
            }

            // The attacking country must retain at least one soldier.
            if (soldiersToTransfer > attacker.Soldiers - 1)
            {
                throw new ArgumentException(
                    "The attacking country must retain at least one soldier");
            }

            // Move soldiers into the conquered country.
            attacker.Soldiers -= soldiersToTransfer;
            conqueredCountry.Soldiers += soldiersToTransfer;

            // Record the completed conquest and soldier transfer.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "PlayerConquest",
                Round = game.Round,
                ActorCountryId = attacker.CountryId,
                ActorCountryName = attacker.Name,
                TargetCountryId = conqueredCountry.CountryId,
                TargetCountryName = conqueredCountry.Name,
                WasConquered = true,
                Message =
    attacker.ControllerCountryId == attacker.CountryId
        ? $"{attacker.Name} conquered {conqueredCountry.Name} and " +
          $"transferred {soldiersToTransfer} soldiers into it. " +
          $"{game.ConquestBonusAvailable} bonus soldiers were awarded " +
          $"to {attacker.Name}."
        : $"{attacker.Name}, operating under " +
          $"{PlayerName(game, attacker.ControllerCountryId)}'s control, " +
          $"conquered {conqueredCountry.Name}. " +
          $"{conqueredCountry.Name} is now part of " +
          $"{PlayerName(game, attacker.ControllerCountryId)}'s empire. " +
          $"{soldiersToTransfer} soldiers were transferred and " +
          $"{game.ConquestBonusAvailable} bonus soldiers were awarded.",
                CreatedAt = DateTime.UtcNow
            });


            // Clear the pending transfer information.
            game.ConquestTransferPending = false;
            game.PendingConquestCountryId = string.Empty;
            game.PendingConquestAttackerCountryId = string.Empty;
            game.ConquestBonusAvailable = 0;

            // Check whether the game has ended (writes the GameCompleted event).
            FinishGameIfOver(game);

            // Save the updated game.
            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId,
                game);

            return game;
        }


        public async Task<Game?> TransferSoldiersAsync(string gameId, TransferRequest request)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                throw new InvalidOperationException("This game is not in progress");
            }

            if (game.CurrentTurnControllerId != game.PlayerCountryId)
            {
                throw new InvalidOperationException("It is not the human player's turn");
            }

            if (game.BattleInProgress || game.ConquestTransferPending)
            {
                throw new InvalidOperationException("Finish the current battle or conquest transfer first");
            }

            if (request == null)
            {
                throw new ArgumentException("A transfer request is required");
            }

            if (request.SourceCountryId == request.DestinationCountryId)
            {
                throw new ArgumentException("The source and destination countries must be different");
            }

            if (request.soldiers < 1)
            {
                throw new ArgumentException("You must transfer at least one soldier");
            }

            var sourceCountry = game.Countries.FirstOrDefault(country =>
                country.CountryId == request.SourceCountryId);

            var destinationCountry = game.Countries.FirstOrDefault(country =>
                country.CountryId == request.DestinationCountryId);

            if (sourceCountry == null || destinationCountry == null)
            {
                throw new ArgumentException(
                    "You can only transfer soldiers between countries you control.");
            }

            if (sourceCountry.IsEliminated || destinationCountry.IsEliminated)
            {
                throw new ArgumentException(
                    "You cannot transfer soldiers to or from an eliminated country.");
            }

            if (sourceCountry.ControllerCountryId != game.PlayerCountryId ||
                destinationCountry.ControllerCountryId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "You can only transfer soldiers between countries you control.");
            }

            if (request.soldiers > sourceCountry.Soldiers - 1)
            {
                throw new ArgumentException("The source country must retain at least one soldier");
            }

            sourceCountry.Soldiers -= request.soldiers;
            destinationCountry.Soldiers += request.soldiers;

            // Record the transfer in the game history.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "SoldiersTransferred",
                Round = game.Round,
                ActorCountryId = sourceCountry.CountryId,
                ActorCountryName = sourceCountry.Name,
                TargetCountryId = destinationCountry.CountryId,
                TargetCountryName = destinationCountry.Name,
                Message = $"{PlayerName(game, game.PlayerCountryId)} moved " +
                          $"{request.soldiers} soldiers from {sourceCountry.Name} " +
                          $"to {destinationCountry.Name}.",
                CreatedAt = DateTime.UtcNow
            });

            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId, game);

            return game;
        }



        private void TransferNpcSoldiers(Game game)
        {
            string npcCountryId = game.CurrentTurnControllerId;
            string npcName = PlayerName(game, npcCountryId);

            // Find all territories controlled by this NPC.
            var npcCountries = game.Countries
                .Where(country =>
                    country.ControllerCountryId == npcCountryId &&
                    !country.IsEliminated)
                .ToList();

            // Transfers require at least two controlled territories.
            if (npcCountries.Count < 2)
            {
                if (npcCountries.Count == 0)
                {
                    return;
                }

                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcTransferSkipped",
                    Round = game.Round,
                    ActorCountryId = npcCountryId,
                    ActorCountryName = npcName,
                    Message =
                        $"{npcName} decided not to redistribute soldiers " +
                        "because it controls only one territory. " +
                        "There is no other controlled territory to receive soldiers.",
                    CreatedAt = DateTime.UtcNow
                });

                return;
            }

            // Calculate the NPC's average military strength.
            double averageSoldiers = npcCountries.Average(
                country => country.Soldiers);

            // Find territories with above-average military strength.
            // Keep at least two soldiers at the source territory.
            var sourceOptions = npcCountries
                .Where(country => country.Soldiers > averageSoldiers &&
                                  country.Soldiers > 2)
                .OrderByDescending(country => country.Soldiers - averageSoldiers)
                .ToList();

            // Find territories with below-average military strength.
            var destinationOptions = npcCountries
                .Where(country => country.Soldiers < averageSoldiers)
                .OrderBy(country => country.Soldiers)
                .ToList();

            // Check whether a useful transfer is possible.
            if (sourceOptions.Count == 0 ||
                destinationOptions.Count == 0)
            {
                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "NpcTransferSkipped",
                    Round = game.Round,
                    ActorCountryId = npcCountryId,
                    ActorCountryName = npcName,
                    Message =
                        $"{npcName} decided not to redistribute soldiers " +
                        "because its territories are already reasonably " +
                        "balanced or no safe transfer is possible.",
                    CreatedAt = DateTime.UtcNow
                });

                return;
            }

            // Select the strongest source and weakest destination.
            var sourceCountry = sourceOptions.First();
            var destinationCountry = destinationOptions.First();

            // Calculate how many soldiers the source can safely contribute.
            int availableSoldiers = sourceCountry.Soldiers - 2;

            // Calculate the destination's military shortfall.
            int militaryShortfall = (int)Math.Ceiling(
                averageSoldiers - destinationCountry.Soldiers);

            // Transfer only enough soldiers to improve the balance.
            int soldiersToTransfer = Math.Min(
                availableSoldiers,
                Math.Max(1, militaryShortfall));

            if (soldiersToTransfer <= 0)
            {
                return;
            }

            // Perform the transfer.
            sourceCountry.Soldiers -= soldiersToTransfer;
            destinationCountry.Soldiers += soldiersToTransfer;

            // Record the NPC's decision and justification.
            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "NpcSoldierTransfer",
                Round = game.Round,
                ActorCountryId = sourceCountry.CountryId,
                ActorCountryName = sourceCountry.Name,
                TargetCountryId = destinationCountry.CountryId,
                TargetCountryName = destinationCountry.Name,
                Message =
                    $"{sourceCountry.Name} transferred {soldiersToTransfer} " +
                    $"soldiers to {destinationCountry.Name}. The NPC selected " +
                    $"{sourceCountry.Name} because it had above-average military " +
                    $"strength ({sourceCountry.Soldiers + soldiersToTransfer} " +
                    $"soldiers before the transfer), while " +
                    $"{destinationCountry.Name} needed additional support. " +
                    "At least two soldiers were retained at the source territory.",
                CreatedAt = DateTime.UtcNow
            });
        }


        private bool UpdateGameOutcome(Game game)
        {
            // Check whether the human player still controls any active countries.
            bool humanPlayerHasCountries = game.Countries.Any(country =>
                country.ControllerCountryId == game.PlayerCountryId &&
                !country.IsEliminated);

            // The human player loses immediately when they control no active countries.
            if (!humanPlayerHasCountries)
            {
                game.Status = "Completed";
                game.Outcome = "Defeat";

                Console.WriteLine(
                    "GAME OVER: The human player has been eliminated.");

                return true;
            }

            // Find controllers that still own at least one active country.
            var activeControllers = game.Countries
                .Where(country => !country.IsEliminated)
                .Select(country => country.ControllerCountryId)
                .Distinct()
                .ToList();

            // Victory occurs when the human player controls every active country.
            if (activeControllers.Count == 1 &&
                activeControllers[0] == game.PlayerCountryId)
            {
                game.Status = "Completed";
                game.Outcome = "Victory";

                Console.WriteLine(
                    "VICTORY: The human player controls the world.");

                return true;
            }

            // The game can continue.
            return false;
        }


        // Checks for a winner or loser and, if the game has just ended,
        // records a single GameCompleted event. Safe to call repeatedly.
        private void FinishGameIfOver(Game game)
        {
            if (game.Status != "InProgress")
            {
                return;
            }

            if (!UpdateGameOutcome(game))
            {
                return;
            }

            game.Events.Add(new GameEvent
            {
                GameId = game.Id!,
                EventType = "GameCompleted",
                Round = game.Round,
                ActorCountryId = game.PlayerCountryId,
                ActorCountryName = PlayerName(game, game.PlayerCountryId),
                Message = game.Outcome == "Victory"
                    ? "The human player has conquered every country and won the game."
                    : "The human player has lost all controlled countries and been defeated.",
                CreatedAt = DateTime.UtcNow
            });
        }


        // Returns the name of a player (the original country of a controller).
        private static string PlayerName(Game game, string controllerId) =>
            game.Countries.FirstOrDefault(c => c.CountryId == controllerId)?.Name
            ?? controllerId;


        //Attack Method
        public async Task<BattleResult?> AttackAsync(
            string gameId,
            AttackRequest request)
        {
            var game = await GetGameAsync(gameId);

            if (game == null)
            {
                return null;
            }

            if (game.Status != "InProgress")
            {
                throw new InvalidOperationException(
                    "This game is not in progress.");
            }

            if (game.CurrentTurnControllerId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "It is not the human player's turn.");
            }

            if (request == null)
            {
                throw new ArgumentException(
                    "An attack request is required.");
            }

            if (game.ReinforcementsAvailable > 0)
            {
                throw new InvalidOperationException(
                    "Allocate your reinforcement soldiers before attacking.");
            }

            if (game.ConquestTransferPending)
            {
                throw new InvalidOperationException(
                    "Complete the conquest soldier transfer before starting another attack.");
            }

            GameCountry? attacker;
            GameCountry? defender;

            if (game.BattleInProgress)
            {
                // An ongoing battle must use the same attacker and defender.
                if (game.ActiveAttackerCountryId != request.AttackerCountryId ||
                    game.ActiveDefenderCountryId != request.DefenderCountryId)
                {
                    throw new InvalidOperationException(
                        "Finish or stop the current battle before starting another.");
                }

                attacker = game.Countries.FirstOrDefault(country =>
                    country.CountryId == game.ActiveAttackerCountryId);

                defender = game.Countries.FirstOrDefault(country =>
                    country.CountryId == game.ActiveDefenderCountryId);
            }
            else
            {
                // A new attack cannot start if the player has already attacked.
                if (game.HasAttackedThisTurn)
                {
                    throw new InvalidOperationException(
                        "You have already attacked this turn.");
                }

                // A stop request is only valid during an ongoing battle.
                if (!request.ContinueAttack)
                {
                    throw new InvalidOperationException(
                        "There is no ongoing battle to stop.");
                }

                attacker = game.Countries.FirstOrDefault(country =>
                    country.CountryId == request.AttackerCountryId);

                defender = game.Countries.FirstOrDefault(country =>
                    country.CountryId == request.DefenderCountryId);
            }

            if (attacker == null || defender == null)
            {
                throw new ArgumentException(
                    "Both countries must belong to this game.");
            }

            if (attacker.CountryId == defender.CountryId)
            {
                throw new ArgumentException(
                    "A country cannot attack itself.");
            }

            if (attacker.ControllerCountryId != game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "You can only attack using a country you control.");
            }

            if (defender.ControllerCountryId == game.PlayerCountryId)
            {
                throw new InvalidOperationException(
                    "You cannot attack a country you control.");
            }

            if (attacker.Soldiers < 2)
            {
                throw new InvalidOperationException(
                    "You need at least two soldiers to attack.");
            }

            if (defender.Soldiers < 1)
            {
                throw new InvalidOperationException(
                    "The defending country has no soldiers.");
            }

            // Stop the existing battle and end the player's turn.
            if (!request.ContinueAttack && game.BattleInProgress)
            {
                // Record that the player chose to stop the battle.
                game.Events.Add(new GameEvent
                {
                    GameId = game.Id!,
                    EventType = "PlayerAttackStopped",
                    Round = game.Round,
                    ActorCountryId = attacker.CountryId,
                    ActorCountryName = attacker.Name,
                    TargetCountryId = defender.CountryId,
                    TargetCountryName = defender.Name,
                    Message = $"{attacker.Name} stopped attacking {defender.Name}. " +
                              "The player's turn has ended.",
                    CreatedAt = DateTime.UtcNow
                });


                game.BattleInProgress = false;
                game.ActiveAttackerCountryId = string.Empty;
                game.ActiveDefenderCountryId = string.Empty;

                await _context.Games.ReplaceOneAsync(
                    savedGame => savedGame.Id == gameId,
                    game);

                return new BattleResult
                {
                    AttackerCountryId = attacker.CountryId,
                    DefenderCountryId = defender.CountryId,
                    AttackerCountryName = attacker.Name,
                    DefenderCountryName = defender.Name,
                    Message = $"{attacker.Name} stopped attacking. The turn has ended."
                };
            }

            // Start a new battle if one is not already in progress.
            if (!game.BattleInProgress)
            {
                game.BattleInProgress = true;
                game.ActiveAttackerCountryId = attacker.CountryId;
                game.ActiveDefenderCountryId = defender.CountryId;
                game.HasAttackedThisTurn = true;
            }

            // Both sides roll the same number of dice.
            // The attacker must leave at least one soldier behind.
            int diceCount = Math.Min(
                attacker.Soldiers - 1,
                defender.Soldiers);

            var attackerDice = Enumerable.Range(0, diceCount)
                .Select(_ => Random.Shared.Next(1, 7))
                .ToList();

            var defenderDice = Enumerable.Range(0, diceCount)
                .Select(_ => Random.Shared.Next(1, 7))
                .ToList();

            int attackerLosses = 0;
            int defenderLosses = 0;

            // Compare matching dice. Ties cause no casualties.
            for (int i = 0; i < diceCount; i++)
            {
                if (attackerDice[i] > defenderDice[i])
                {
                    defenderLosses++;
                }
                else if (defenderDice[i] > attackerDice[i])
                {
                    attackerLosses++;
                }
            }

            // Apply casualties.
            attacker.Soldiers -= attackerLosses;
            defender.Soldiers -= defenderLosses;


            // Record this battle round in the game's event history.
            bool conquered = defender.Soldiers == 0;

            var battleEvent = new GameEvent
            {
                GameId = game.Id!,
                EventType = "PlayerBattleRound",
                Round = game.Round,
                ActorCountryId = attacker.CountryId,
                ActorCountryName = attacker.Name,
                TargetCountryId = defender.CountryId,
                TargetCountryName = defender.Name,
                AttackerDice = attackerDice,
                DefenderDice = defenderDice,
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                WasConquered = conquered,
                Message = $"{attacker.Name} rolled [{string.Join(", ", attackerDice)}] " +
                          $"against {defender.Name}'s [{string.Join(", ", defenderDice)}]. " +
                          $"{attacker.Name} lost {attackerLosses} soldiers; " +
                          $"{defender.Name} lost {defenderLosses} soldiers." +
                          (conquered
                              ? $" {attacker.Name} conquered {defender.Name}!"
                              : ""),
                CreatedAt = DateTime.UtcNow
            };

            battleEvent.Commentary =
                await _battleCommentaryService.GenerateCommentaryAsync(
                    attacker.Name,
                    defender.Name,
                    attackerDice,
                    defenderDice,
                    attackerLosses,
                    defenderLosses,
                    conquered
                );

            game.Events.Add(battleEvent);

            bool attackerExhausted = attacker.Soldiers <= 1;

            if (conquered)
            {
                // Remember who controlled the country before the conquest.
                string previousControllerId = defender.ControllerCountryId;

                // Transfer control to the attacking country.
                defender.ControllerCountryId = attacker.ControllerCountryId;
                defender.IsHumanPlayer = attacker.IsHumanPlayer;
                defender.IsEliminated = false;

                // Check whether the previous controller still owns any active countries.
                bool previousControllerStillHasCountries = game.Countries.Any(
                    country =>
                        country.ControllerCountryId == previousControllerId &&
                        !country.IsEliminated);

                if (!previousControllerStillHasCountries)
                {
                    game.Events.Add(new GameEvent
                    {
                        GameId = game.Id!,
                        EventType = "CountryEliminated",
                        Round = game.Round,
                        ActorCountryId = attacker.CountryId,
                        ActorCountryName = attacker.Name,
                        TargetCountryId = previousControllerId,
                        TargetCountryName = PlayerName(game, previousControllerId),
                        Message =
                                $"{PlayerName(game, previousControllerId)} has been eliminated. " +
                                $"{attacker.Name} carried out the final conquest for " +
                                $"{PlayerName(game, attacker.ControllerCountryId)}'s empire.",
                        CreatedAt = DateTime.UtcNow
                    });
                }


                // Calculate the conquest bonus and pay it immediately,
                // so the attacker always has soldiers available to transfer.
                int conquestBonus = Math.Max(
                    2,
                    defender.StartingSoldiers / 2);

                attacker.Soldiers += conquestBonus;

                // Save the pending conquest transfer information.
                game.ConquestTransferPending = true;
                game.PendingConquestCountryId = defender.CountryId;
                game.PendingConquestAttackerCountryId = attacker.CountryId;
                game.ConquestBonusAvailable = conquestBonus;

                // End the battle. The player must transfer soldiers next.
                game.BattleInProgress = false;
                game.ActiveAttackerCountryId = string.Empty;
                game.ActiveDefenderCountryId = string.Empty;
            }
            else if (attackerExhausted)
            {
                // The attacker has one soldier left and cannot continue.
                game.BattleInProgress = false;
                game.ActiveAttackerCountryId = string.Empty;
                game.ActiveDefenderCountryId = string.Empty;
            }

            string message;

            if (conquered)
            {
                int conquestBonus = game.ConquestBonusAvailable;

                message =
                    $"{attacker.Name} conquered {defender.Name}! " +
                    $"{conquestBonus} bonus soldiers are available. " +
                    "Choose how many soldiers to transfer into the conquered country.";
            }
            else if (attackerExhausted)
            {
                message =
                    $"{attacker.Name} has one soldier remaining. " +
                    "The attack has ended.";
            }
            else
            {
                message =
                    $"{attacker.Name} lost {attackerLosses} soldiers; " +
                    $"{defender.Name} lost {defenderLosses}. " +
                    "Choose whether to continue attacking or stop.";
            }

            var result = new BattleResult
            {
                AttackerCountryId = attacker.CountryId,
                DefenderCountryId = defender.CountryId,
                AttackerCountryName = attacker.Name,
                DefenderCountryName = defender.Name,
                AttackerDice = attackerDice,
                DefenderDice = defenderDice,
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                DefenderConquered = conquered,
                Message = message
            };

            await _context.Games.ReplaceOneAsync(
                savedGame => savedGame.Id == gameId,
                game);

            return result;
        }

    }

}