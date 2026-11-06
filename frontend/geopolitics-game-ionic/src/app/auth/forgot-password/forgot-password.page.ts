
import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonContent,
  IonButton,
  IonInput,
  IonItem,
  IonLabel,
  IonIcon,
  IonSpinner
} from '@ionic/angular';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.page.html',
  styleUrls: ['./forgot-password.page.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IonContent,
    IonButton,
    IonInput,
    IonItem,
    IonLabel,
    IonIcon,
    IonSpinner,
    RouterModule
  ]
})
export class ForgotPasswordPage {

  email = '';

  isLoading = signal(false);
  submitted = signal(false);
  message = signal('');
  errorMessage = signal('');

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  submitRequest(): void {
    if (!this.email.trim()) {
      this.errorMessage.set('Please enter your email address.');
      return;
    }

    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');
    this.message.set('');

    this.authService.forgotPassword({
      email: this.email.trim()
    }).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        this.submitted.set(true);

        this.message.set(
          response.message ||
          'If your account exists and is verified, a password reset link will be sent to your email.'
        );
      },
      error: () => {
        this.isLoading.set(false);

        this.errorMessage.set(
          'Something went wrong. Please try again.'
        );
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
