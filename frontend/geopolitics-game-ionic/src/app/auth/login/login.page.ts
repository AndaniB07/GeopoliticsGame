
import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonContent,
  IonButton,
  IonIcon,
  IonInput,
  IonLabel,
  ToastController
} from '@ionic/angular';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    IonContent,
    IonButton,
    IonIcon,
    IonInput,
    IonLabel
  ]
})
export class LoginPage {

  // Form values
  usernameOrEmail = '';
  password = '';

  // Reactive state using Angular Signals
  isLoading = signal(false);
  isResending = signal(false);

  errorMessage = signal('');
  requiresVerification = signal(false);

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  async login(): Promise<void> {

    this.clearErrors();

    const loginValue = this.usernameOrEmail.trim();

    if (!loginValue || !this.password.trim()) {

      this.errorMessage.set(
        'Please enter both your username or email and your password.'
      );

      return;
    }

    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);

    this.authService.login({
      usernameOrEmail: loginValue,
      password: this.password
    }).subscribe({

      next: async () => {

        this.isLoading.set(false);

        await this.showToast(
          'Login successful. Welcome back!',
          'success'
        );

        await this.router.navigate(['/dashboard']);

      },

      error: async (error) => {

        this.isLoading.set(false);

        const message =
          error.error?.message ||
          error.message ||
          'Login failed. Please try again.';

        const lowerMessage = message.toLowerCase();

        // Email verification required
        if (
          error.status === 403 ||
          lowerMessage.includes('verify your email') ||
          lowerMessage.includes('email address before logging in')
        ) {

          this.requiresVerification.set(true);

          this.errorMessage.set(
            'Please verify your email address before logging in.'
          );

          console.log(
            'VERIFICATION REQUIRED:',
            this.requiresVerification()
          );

          await this.showToast(
            this.errorMessage(),
            'warning'
          );

          return;
        }

        // Incorrect login details
        if (error.status === 401) {

          this.errorMessage.set(
            'Incorrect username/email or password. Please try again.'
          );

          await this.showToast(
            this.errorMessage(),
            'danger'
          );

          return;
        }

        this.errorMessage.set(message);

        await this.showToast(
          message,
          'danger'
        );

      }

    });

  }

  isEmailAddress(): boolean {

    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(
      this.usernameOrEmail.trim()
    );

  }

  resendVerification(): void {

    if (!this.isEmailAddress() || this.isResending()) {
      return;
    }

    this.isResending.set(true);

    this.authService.resendVerification(
      this.usernameOrEmail.trim()
    ).subscribe({

      next: async () => {

        this.isResending.set(false);

        await this.showToast(
          'If your account is unverified, a new verification email has been sent.',
          'success'
        );

      },

      error: async () => {

        this.isResending.set(false);

        await this.showToast(
          'Unable to resend the verification email. Please try again.',
          'danger'
        );

      }

    });

  }

  clearErrors(): void {

    this.errorMessage.set('');
    this.requiresVerification.set(false);

  }

  private async showToast(
    message: string,
    color: string
  ): Promise<void> {

    const toast = await this.toastController.create({

      message,
      duration: 4000,
      position: 'bottom',
      color

    });

    await toast.present();

  }

}
