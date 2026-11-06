import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonContent,
  IonHeader,
  IonSpinner
} from '@ionic/angular';
import { RouterLink } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { Chart, registerables } from 'chart.js';

import { GameService, GameStatistics } from '../../services/game';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth';

Chart.register(...registerables);

@Component({
  selector: 'app-statistics',
  templateUrl: './statistics.page.html',
  styleUrls: ['./statistics.page.scss'],
  standalone: true,
  imports: [
    IonContent,
    IonHeader,
    IonSpinner,
    CommonModule,
    FormsModule,
    RouterLink,
    BaseChartDirective
  ]
})
export class StatisticsPage implements OnInit {

  statistics = signal<GameStatistics | null>(null);
  isLoading = signal(true);
  errorMessage = signal('');

  constructor(
    private gameService: GameService,
    private route: ActivatedRoute,
    private authService: AuthService
  ) { }

  ngOnInit() {
    const gameId = this.route.snapshot.paramMap.get('id');

    if (!gameId) {
      this.errorMessage.set('Game ID could not be found.');
      this.isLoading.set(false);
      return;
    }

    this.loadStatistics(gameId);
  }

  loadStatistics(gameId: string) {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.gameService.getGameStatistics(gameId).subscribe({
      next: (data) => {
        this.statistics.set(data);

        this.battleChartData = {
          ...this.battleChartData,
          datasets: [
            {
              ...this.battleChartData.datasets[0],
              data: [data.battlesWon, data.battlesLost]
            }
          ]
            
        };
        
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Error loading game statistics:', error);
        this.errorMessage.set('Unable to load campaign statistics.');
        this.isLoading.set(false);
      }
    });
  }


  getBattleWinRate(): number {
  const stats = this.statistics();

  if (!stats || stats.totalBattleRounds === 0) {
    return 0;
  }

  return Math.round(
    (stats.battlesWon / stats.totalBattleRounds) * 100
  );
}

public battleChartData = {
  labels: ['Won', 'Lost'],
  datasets: [
    {
      label: 'Battle Rounds',
      data: [0, 0],
      backgroundColor: ['#d6ad60', '#c75c5c'],
      borderRadius: 8
    }
  ]
};

public battleChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      labels: {
        color: '#f5f5f5'
      }
    }
  },
  scales: {
    x: {
      ticks: {
        color: '#aab4c8'
      },
      grid: {
        color: '#273349'
      }
    },
    y: {
      beginAtZero: true,
      ticks: {
        color: '#aab4c8',
        precision: 0
      },
      grid: {
        color: '#273349'
      }
    }
  }
};

logout() {
  this.authService.logout();
  window.location.href = '/login';
}

}