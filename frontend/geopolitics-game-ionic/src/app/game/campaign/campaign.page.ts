import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonContent, IonHeader, IonSpinner, ToastController } from '@ionic/angular';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { GameService } from '../../services/game';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-campaign',
  templateUrl: './campaign.page.html',
  styleUrls: ['./campaign.page.scss'],
  standalone: true,
  imports: [IonContent, IonHeader, RouterLink, IonSpinner, CommonModule]
})
export class CampaignPage implements OnInit {

  game = signal<any | null>(null);
  isLoading = signal(true);
  errorMessage = signal('');
  isAdvancingTurn = signal(false);
  isGameOwner = signal(false);

  reinforcementAllocations = signal<Record<string, number>>({});
  isAllocatingReinforcements = signal(false);

  // Attack selections
  selectedAttackerCountryId = signal('');
  selectedDefenderCountryId = signal('');

  // Battle result and status
  battleResult = signal<any>(null);
  isAttacking = signal(false);
  attackErrorMessage = signal('');

  // Conquest soldier transfer
  soldiersToTransfer = signal(1);
  isTransferringConquestSoldiers = signal(false);

  // Ordinary troop transfers
  selectedTransferSourceId = signal('');
  selectedTransferDestinationId = signal('');
  soldiersToMove = signal(1);
  isTransferringSoldiers = signal(false);
  transferErrorMessage = signal('');

  // Makes sure the win/lose toast only appears once per game ending
  private gameOverToastShown = false;

  // ---------- Derived state ----------

  countries = computed<any[]>(() => this.game()?.countries ?? []);

  isGameOver = computed(() => {
    const status = this.game()?.status;
    return !!status && status !== 'InProgress';
  });

  // Latest events first
  events = computed<any[]>(() => {
    const gameEvents = this.game()?.events ?? [];
    return [...gameEvents].reverse();
  });

  // Uses the game's playerCountryId, so it still works after NPCs
  // have conquered every country the player owned.
  playerCountryName = computed(() => {
    const game = this.game();
    const playerCountry = this.countries().find(
      (country: any) => country.countryId === game?.playerCountryId
    );
    return playerCountry?.name ?? 'Unknown';
  });

  playerControlledCountries = computed<any[]>(() => {
    const game = this.game();
    if (!game) {
      return [];
    }
    return this.countries().filter(
      (country: any) =>
        country.controllerCountryId === game.playerCountryId && !country.isEliminated
    );
  });

  enemyCountries = computed<any[]>(() => {
    const game = this.game();
    if (!game || !game.countries) {
      return [];
    }
    return game.countries.filter(
      (country: any) =>
        country.controllerCountryId !== game.playerCountryId && !country.isEliminated
    );
  });

  totalAllocatedReinforcements = computed(() =>
    Object.values(this.reinforcementAllocations()).reduce(
      (total, soldiers) => total + soldiers,
      0
    )
  );

  remainingReinforcements = computed(() => {
    const available = this.game()?.reinforcementsAvailable ?? 0;
    return available - this.totalAllocatedReinforcements();
  });

  constructor(
    private route: ActivatedRoute,
    private gameService: GameService,
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  ngOnInit(): void {
    this.loadGame();
  }

  // ---------- Central place where the game state is updated ----------

  private updateGame(game: any): void {
    const previousStatus = this.game()?.status;
    this.game.set(game);

    // Show the toast only when the game ends during this session
    // (not when someone opens an already-finished game).
    const justFinished =
      previousStatus === 'InProgress' && game?.status && game.status !== 'InProgress';

    if (justFinished && this.isGameOwner() && !this.gameOverToastShown) {
      this.gameOverToastShown = true;
      this.showGameOverToast(game.outcome);
    }
  }

  private async showGameOverToast(outcome: string): Promise<void> {
    const won = outcome === 'Victory';

    const toast = await this.toastController.create({
      message: won
        ? 'Victory! You have conquered the world.'
        : 'Defeat! You have lost all of your countries.',
      duration: 6000,
      position: 'top',
      color: won ? 'success' : 'danger',
      buttons: [{ text: 'OK', role: 'cancel' }]
    });

    await toast.present();
  }

  // ---------- Loading ----------

  loadGame(showSpinner: boolean = true): void {
    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.errorMessage.set('No campaign ID was provided');
      this.isLoading.set(false);
      return;
    }

    if (showSpinner) {
      this.isLoading.set(true);
    }
    this.errorMessage.set('');

    this.gameService.getGame(gameId).subscribe({
      next: (game) => {
        const currentPlayer = this.authService.getCurrentPlayer();
        this.isGameOwner.set(
          !!currentPlayer && currentPlayer.playerId === game.ownerPlayerId
        );

        this.updateGame(game);
        this.isLoading.set(false);
      },
      error: async (error) => {
        console.error('Failed to load campaign:', error);

        const message =
          error?.error?.message ??
          error?.message ??
          'Could not load the campaign. Please try again.';

        this.errorMessage.set(message);
        this.isLoading.set(false);

        const toast = await this.toastController.create({
          message,
          duration: 3500,
          position: 'bottom'
        });
        await toast.present();
      }
    });
  }

  // ---------- Event titles ----------

  getEventTitle(eventType: string): string {
    switch (eventType) {
      case 'GameCreated': return 'Game Created';
      case 'TurnStarted': return 'Turn Started';
      case 'NpcTurnStarted': return 'NPC Turn Started';
      case 'NpcAttackDecision': return 'NPC Attack Decision';
      case 'NpcAttackSkipped': return 'NPC Attack Skipped';
      case 'NpcAttackStopped': return 'NPC Attack Stopped';
      case 'NpcReinforcementsAllocated': return 'NPC Reinforcements Allocated';
      case 'NpcSoldierTransfer': return 'NPC Soldier Transfer';
      case 'NpcTransferSkipped': return 'NPC Transfer Skipped';
      case 'NpcBattleRound': return 'NPC Battle Round';
      case 'NpcConquest': return 'NPC Conquest';
      case 'PlayerBattleRound': return 'Your Battle Round';
      case 'PlayerConquest': return 'Your Conquest';
      case 'PlayerAttackStopped': return 'You Stopped Attacking';
      case 'ReinforcementsAllocated': return 'Your Reinforcements';
      case 'SoldiersTransferred': return 'Soldiers Transferred';
      case 'CountryEliminated': return 'Country Eliminated';
      case 'GameCompleted': return 'Game Completed';
      default: return eventType;
    }
  }

  // ---------- Reinforcements ----------

  updateReinforcementAllocation(countryId: string, soldiers: number): void {
    if (soldiers < 0) {
      return;
    }

    const allocations = this.reinforcementAllocations();
    const currentAllocation = allocations[countryId] ?? 0;
    const totalWithoutCurrentCountry =
      this.totalAllocatedReinforcements() - currentAllocation;
    const available = this.game()?.reinforcementsAvailable ?? 0;

    if (totalWithoutCurrentCountry + soldiers > available) {
      return;
    }

    this.reinforcementAllocations.set({ ...allocations, [countryId]: soldiers });
  }

  allocateReinforcements(): void {
    if (this.isAllocatingReinforcements()) {
      return;
    }

    const game = this.game();

    if (!game) {
      this.attackErrorMessage.set('Campaign data is unavailable');
      return;
    }

    if (game.currentTurnControllerId !== game.playerCountryId) {
      this.attackErrorMessage.set('It is not your turn.');
      return;
    }

    const allocations = Object.entries(this.reinforcementAllocations())
      .filter(([, soldiers]) => soldiers > 0)
      .map(([countryId, soldiers]) => ({ countryId, soldiers }));

    if (this.totalAllocatedReinforcements() !== game.reinforcementsAvailable) {
      this.attackErrorMessage.set(
        `Allocate exactly ${game.reinforcementsAvailable} soldiers before submitting.`
      );
      return;
    }

    if (allocations.length === 0) {
      this.attackErrorMessage.set('Assign soldiers to at least one country.');
      return;
    }

    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.attackErrorMessage.set('Campaign ID is missing.');
      return;
    }

    this.isAllocatingReinforcements.set(true);
    this.attackErrorMessage.set('');

    this.gameService.allocateReinforcements(gameId, allocations).subscribe({
      next: (updatedGame) => {
        this.updateGame(updatedGame);
        this.reinforcementAllocations.set({});
        this.isAllocatingReinforcements.set(false);
        this.attackErrorMessage.set('');
      },
      error: (error) => {
        console.error('Failed to allocate reinforcements:', error);

        const message =
          error?.error?.message ??
          (typeof error?.error === 'string' ? error.error : null) ??
          'Failed to allocate reinforcements.';

        this.attackErrorMessage.set(message);
        this.isAllocatingReinforcements.set(false);
      }
    });
  }

  // ---------- Attack ----------

  attackCountry(continueAttack: boolean = true): void {
    if (this.isAttacking()) {
      return;
    }

    const attackerCountryId = this.selectedAttackerCountryId();
    const defenderCountryId = this.selectedDefenderCountryId();

    if (!attackerCountryId || !defenderCountryId) {
      this.attackErrorMessage.set(
        'Select both an attacking country and an enemy country.'
      );
      return;
    }

    if (attackerCountryId === defenderCountryId) {
      this.attackErrorMessage.set(
        'Your attacking and defending countries must be different.'
      );
      return;
    }

    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.attackErrorMessage.set('Campaign ID is missing.');
      return;
    }

    this.isAttacking.set(true);
    this.attackErrorMessage.set('');
    this.battleResult.set(null);

    this.gameService.attack(
      gameId,
      attackerCountryId,
      defenderCountryId,
      continueAttack
    ).subscribe({
      next: (result) => {
        this.battleResult.set(result);
        this.isAttacking.set(false);

        // Refresh soldiers and ownership without showing the full-page spinner.
        this.loadGame(false);
      },
      error: (error) => {
        console.error('Attack failed:', error);

        const message =
          error?.error?.message ??
          (typeof error?.error === 'string' ? error.error : null) ??
          'The attack failed. Check the console for details.';

        this.attackErrorMessage.set(message);
        this.isAttacking.set(false);
      }
    });
  }

  // ---------- Advance turn (NPC turns) ----------

  advanceTurn(): void {
    if (this.isAdvancingTurn() || this.isAttacking()) {
      return;
    }

    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.attackErrorMessage.set('Campaign ID is missing.');
      return;
    }

    this.isAdvancingTurn.set(true);
    this.attackErrorMessage.set('');
    this.battleResult.set(null);

    this.processNextTurn(gameId);
  }

  private processNextTurn(gameId: string): void {
    this.gameService.advanceTurn(gameId).subscribe({
      next: (game) => {
        // updateGame also shows the defeat toast if the NPCs just won.
        this.updateGame(game);

        // Stop if the game has ended.
        if (game.status !== 'InProgress') {
          this.isAdvancingTurn.set(false);
          return;
        }

        // Stop when control returns to the human player.
        if (game.currentTurnControllerId === game.playerCountryId) {
          this.isAdvancingTurn.set(false);
          return;
        }

        // Continue with the next NPC after a short delay.
        setTimeout(() => this.processNextTurn(gameId), 300);
      },
      error: (error) => {
        console.error('Failed to advance turn:', error);

        const message =
          error?.error?.message ??
          (typeof error?.error === 'string' ? error.error : null) ??
          'Could not advance the turn. Please try again.';

        this.attackErrorMessage.set(message);
        this.isAdvancingTurn.set(false);
      }
    });
  }

  // ---------- Conquest transfer ----------

  transferConquestSoldiers(): void {
    if (this.isTransferringConquestSoldiers()) {
      return;
    }

    const game = this.game();

    if (!game || !game.conquestTransferPending) {
      this.attackErrorMessage.set('There is no pending conquest transfer.');
      return;
    }

    const soldiers = this.soldiersToTransfer();

    if (!Number.isInteger(soldiers) || soldiers < 1) {
      this.attackErrorMessage.set('Transfer at least one soldier.');
      return;
    }

    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.attackErrorMessage.set('Campaign ID is missing.');
      return;
    }

    this.isTransferringConquestSoldiers.set(true);
    this.attackErrorMessage.set('');

    this.gameService.transferConquestSoldiers(gameId, soldiers).subscribe({
      next: (updatedGame) => {
        // If this conquest was the last country, updateGame shows the victory toast.
        this.updateGame(updatedGame);
        this.soldiersToTransfer.set(1);
        this.isTransferringConquestSoldiers.set(false);
        this.battleResult.set(null);
        this.attackErrorMessage.set('');
      },
      error: (error) => {
        console.error('Conquest transfer failed:', error);

        const message =
          error?.error?.message ??
          (typeof error?.error === 'string' ? error.error : null) ??
          'Failed to transfer conquest soldiers.';

        this.attackErrorMessage.set(message);
        this.isTransferringConquestSoldiers.set(false);
      }
    });
  }

  // ---------- Ordinary troop transfer ----------

  transferSoldiers(): void {
    if (this.isTransferringSoldiers()) {
      return;
    }

    const game = this.game();

    if (!game) {
      this.transferErrorMessage.set('Campaign data is unavailable.');
      return;
    }

    if (game.status !== 'InProgress') {
      this.transferErrorMessage.set('This campaign is not in progress.');
      return;
    }

    if (game.currentTurnControllerId !== game.playerCountryId) {
      this.transferErrorMessage.set('It is not your turn.');
      return;
    }

    if (game.battleInProgress || game.conquestTransferPending) {
      this.transferErrorMessage.set(
        'Finish the current battle or conquest transfer first.'
      );
      return;
    }

    const sourceCountryId = this.selectedTransferSourceId();
    const destinationCountryId = this.selectedTransferDestinationId();
    const soldiers = this.soldiersToMove();

    if (!sourceCountryId || !destinationCountryId) {
      this.transferErrorMessage.set('Select both countries.');
      return;
    }

    if (sourceCountryId === destinationCountryId) {
      this.transferErrorMessage.set(
        'The source and destination countries must be different.'
      );
      return;
    }

    if (!Number.isInteger(soldiers) || soldiers < 1) {
      this.transferErrorMessage.set('Transfer at least one soldier.');
      return;
    }

    const sourceCountry = this.playerControlledCountries().find(
      (country: any) => country.countryId === sourceCountryId
    );

    if (!sourceCountry) {
      this.transferErrorMessage.set(
        'Select a country that you control as the source.'
      );
      return;
    }

    if (soldiers > sourceCountry.soldiers - 1) {
      this.transferErrorMessage.set(
        'The source country must retain at least one soldier.'
      );
      return;
    }

    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.transferErrorMessage.set('Campaign ID is missing.');
      return;
    }

    this.isTransferringSoldiers.set(true);
    this.transferErrorMessage.set('');

    this.gameService.transferSoldiers(
      gameId,
      sourceCountryId,
      destinationCountryId,
      soldiers
    ).subscribe({
      next: (updatedGame) => {
        this.updateGame(updatedGame);
        this.selectedTransferSourceId.set('');
        this.selectedTransferDestinationId.set('');
        this.soldiersToMove.set(1);
        this.isTransferringSoldiers.set(false);
        this.transferErrorMessage.set('');
      },
      error: (error) => {
        console.error('Troop transfer failed:', error);

        const message =
          error?.error?.message ??
          (typeof error?.error === 'string' ? error.error : null) ??
          'Failed to transfer soldiers.';

        this.transferErrorMessage.set(message);
        this.isTransferringSoldiers.set(false);
      }
    });
  }

  // ---------- Helpers ----------

  getControllerCountryName(controllerCountryId: string): string {
    const controller = this.countries().find(
      country => country.countryId === controllerCountryId
    );
    return controller?.name ?? 'Unknown';
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/home']);
  }
}