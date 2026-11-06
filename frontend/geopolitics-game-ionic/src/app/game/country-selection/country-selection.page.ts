import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonContent,
  IonButton,
  IonCard,
  IonCardContent,
  IonSpinner,
  ToastController
} from '@ionic/angular';
import { Router, RouterLink } from '@angular/router';
import { GameService, Country } from '../../services/game';

@Component({
  selector: 'app-country-selection',
  templateUrl: './country-selection.page.html',
  styleUrls: ['./country-selection.page.scss'],
  standalone: true,
  imports: [
    IonContent,
    IonButton,
    IonCard,
    IonCardContent,
    IonSpinner,
    CommonModule,
    FormsModule,
    RouterLink
  ]
})
export class CountrySelectionPage implements OnInit {
  countries = signal<Country[]>([]);
  isLoading = signal(true);
  errorMessage = signal('');
  isCreatingGame = signal(false);

  constructor(
    private gameService: GameService,
    private toastController: ToastController,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadCountries();
  }

  loadCountries(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.gameService.getCountries().subscribe({
      next: (countries) => {
        this.countries.set(countries);
        this.isLoading.set(false);
      },
      error: async (error) => {
        console.error('Failed to load countries:', error);

        const message =
          'Unable to load countries. Please check that the game server is running.';
        this.errorMessage.set(message);
        this.isLoading.set(false);

        await this.showToast(message, 3000);
      }
    });
  }

  selectCountry(country: Country): void {
    if (this.isCreatingGame()) {
      return;
    }

    this.isCreatingGame.set(true);

    this.gameService.createGame(country.id).subscribe({
      next: async (game) => {
        this.isCreatingGame.set(false);

        await this.showToast(`Your campaign as ${country.name} has begun!`, 2500);
        this.router.navigate(['/game/campaign', game.id]);
      },
      error: async (error) => {
        console.error('Failed to create campaign:', error);
        this.isCreatingGame.set(false);

        const message =
          error.error?.message ?? 'Unable to create your campaign. Please try again.';

        await this.showToast(message, 3500);
      }
    });
  }

  private async showToast(message: string, duration: number): Promise<void> {
    const toast = await this.toastController.create({
      message,
      duration,
      position: 'bottom'
    });
    await toast.present();
  }
}