
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import {
  IonContent,
  IonHeader,
  IonButton,
  IonCard,
  IonCardContent,
  IonSpinner,
  IonSearchbar,
  IonSelect,
  IonSelectOption,
  ToastController
} from '@ionic/angular';
import { Router, RouterLink } from '@angular/router';
import { GameService, Country } from '../../services/game';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-country-selection',
  templateUrl: './country-selection.page.html',
  styleUrls: ['./country-selection.page.scss'],
  standalone: true,
  imports: [
    IonHeader,
    IonContent,
    IonButton,
    IonCard,
    IonCardContent,
    IonSpinner,
    IonSearchbar,
    IonSelect,
    IonSelectOption,
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

  // Search and filter values
  searchTerm = signal('');
  selectedContinent = signal('');
  selectedPopulationRange = signal('');
  sortBy = signal('nameAsc');

  // Get the available continents
  continents = computed(() => {

    return [
      ...new Set(
        this.countries()
          .map(country => country.continent)
          .filter(continent => !!continent)
      )
    ].sort();

  });

  // Search, filter and sort countries
  filteredCountries = computed(() => {

    const search = this.searchTerm().trim().toLowerCase();
    const continent = this.selectedContinent();
    const populationRange = this.selectedPopulationRange();
    const sorting = this.sortBy();

    let results = this.countries().filter(country => {

      const matchesSearch = country.name
        .toLowerCase()
        .includes(search);

      const matchesContinent =
        !continent || country.continent === continent;

      const population = Number(country.populationMillions);

      let matchesPopulation = true;

      if (populationRange === 'small') {
        matchesPopulation = population < 20;
      }

      else if (populationRange === 'medium') {
        matchesPopulation = population >= 20 && population <= 100;
      }

      else if (populationRange === 'large') {
        matchesPopulation = population > 100;
      }

      return matchesSearch && matchesContinent && matchesPopulation;

    });

    // Sort the matching countries
    results = [...results].sort((a, b) => {

      switch (sorting) {

        case 'nameAsc':
          return a.name.localeCompare(b.name);

        case 'nameDesc':
          return b.name.localeCompare(a.name);

        case 'populationDesc':
          return Number(b.populationMillions) -
                 Number(a.populationMillions);

        case 'populationAsc':
          return Number(a.populationMillions) -
                 Number(b.populationMillions);

        default:
          return 0;

      }

    });

    return results;

  });

  constructor(
    private gameService: GameService,
    private toastController: ToastController,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadCountries();
  }

  // Load countries from the backend
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

  // Search countries
  onSearchChange(event: CustomEvent): void {

    this.searchTerm.set(event.detail?.value ?? '');

  }

  // Filter by continent
  onContinentChange(event: CustomEvent): void {

    this.selectedContinent.set(event.detail?.value ?? '');

  }

  // Filter by population
  onPopulationChange(event: CustomEvent): void {

    this.selectedPopulationRange.set(event.detail?.value ?? '');

  }

  // Sort countries
  onSortChange(event: CustomEvent): void {

    this.sortBy.set(event.detail?.value ?? 'nameAsc');

  }

  // Clear all filters and restore default sorting
  clearFilters(): void {

    this.searchTerm.set('');
    this.selectedContinent.set('');
    this.selectedPopulationRange.set('');
    this.sortBy.set('nameAsc');

  }

  // Create a campaign using the selected country
  selectCountry(country: Country): void {

    if (this.isCreatingGame()) {
      return;
    }

    this.isCreatingGame.set(true);

    this.gameService.createGame(country.id).subscribe({

      next: async (game) => {

        this.isCreatingGame.set(false);

        await this.showToast(
          `Your campaign as ${country.name} has begun!`,
          2500
        );

        this.router.navigate(['/game/campaign', game.id]);

      },

      error: async (error) => {

        console.error('Failed to create campaign:', error);

        this.isCreatingGame.set(false);

        const message =
          error.error?.message ??
          'Unable to create your campaign. Please try again.';

        await this.showToast(message, 3500);

      }

    });

  }

  // Display notifications
  private async showToast(
    message: string,
    duration: number
  ): Promise<void> {

    const toast = await this.toastController.create({

      message,
      duration,
      position: 'bottom'

    });

    await toast.present();

  }

  logout(): void {
  this.authService.logout();
  this.router.navigate(['/home']);
}

}
