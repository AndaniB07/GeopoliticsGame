
import { Routes } from '@angular/router';
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  {
    path: 'home',
    loadComponent: () =>
      import('./home/home.page').then((m) => m.HomePage)
  },
  {
    path: 'auth/register',
    loadComponent: () =>
      import('./auth/register/register.page').then(
        (m) => m.RegisterPage
      )
  },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./auth/login/login.page').then(
        (m) => m.LoginPage
      )
  },
  {
    path: 'auth/forgot-password',
    loadComponent: () =>
      import('./auth/forgot-password/forgot-password.page').then(
        (m) => m.ForgotPasswordPage
      )
  },
  {
    path: 'verify-email',
    loadComponent: () =>
      import('./auth/verify-email/verify-email.page').then(
        (m) => m.VerifyEmailPage
      )
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./auth/reset-password/reset-password.page').then(
        (m) => m.ResetPasswordPage
      )
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./dashboard/dashboard.page').then(
        (m) => m.DashboardPage
      )
  },
  {
    path: 'game/country-selection',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./game/country-selection/country-selection.page').then(
        (m) => m.CountrySelectionPage
      )
  },
  {
    path: 'game/campaign/:id',
    loadComponent: () =>
      import('./game/campaign/campaign.page').then(
        (m) => m.CampaignPage
      )
  },
  {
    path: 'game-history',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./game/game-history/game-history.page').then(
        (m) => m.GameHistoryPage
      )
  },
  {
    path: 'game/statistics/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./game/statistics/statistics.page').then(
        (m) => m.StatisticsPage
      )
  },
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full'
  }
];
