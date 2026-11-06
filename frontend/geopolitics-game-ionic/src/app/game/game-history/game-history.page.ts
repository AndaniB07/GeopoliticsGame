
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, RouterLink } from '@angular/router';

import {
  IonHeader,
  IonContent,
  IonSpinner,
  IonButton,
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardContent,
  IonBadge
} from '@ionic/angular';

import { GameService } from '../../services/game';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-game-history',
  templateUrl: './game-history.page.html',
  styleUrls: ['./game-history.page.scss'],
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    IonHeader,
    IonContent,
    RouterLink,
    IonSpinner,
    IonButton,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardContent,
    IonBadge
  ]
})
export class GameHistoryPage implements OnInit {



  games = signal<any[]>([]);
  isLoading = signal(true);
  errorMessage = signal('');

  
  selectedFilter = signal('All');

filteredGames = computed(() => {
  const games = this.games();
  const filter = this.selectedFilter();

  if (filter === 'All') {
    return games;
  }

  return games.filter(game =>
    this.getGameStatus(game) === filter
  );
});

  constructor(
    private gameService: GameService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadGames();
  }

  openGame(gameId: string): void {
    this.router.navigate(['/game/campaign', gameId]);
  }

  loadGames(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.gameService.getMyGames().subscribe({
      next: (games) => {
        this.games.set(games);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Error loading game history:', error);

        this.errorMessage.set(
          'Unable to load your game history.'
        );

        this.isLoading.set(false);
      }
    });
  }

  setFilter(filter: string): void {
  this.selectedFilter.set(filter);
}

  getGameStatus(game: any): string {
    if (game.status === 'Completed') {
      if (game.outcome === 'Victory') {
        return 'Won';
      }

      if (game.outcome === 'Defeat') {
        return 'Lost';
      }
    }

    return 'In Progress';
  }

  getPlayerCountryName(game: any): string {
    const playerCountry = game.countries?.find(
      (country: any) =>
        country.countryId === game.playerCountryId
    );

    return playerCountry?.name ?? 'Unknown Country';
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/home']);
  }


}
