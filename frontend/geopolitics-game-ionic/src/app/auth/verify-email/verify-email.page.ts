
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  IonContent,
  IonSpinner,
  IonButton,
  IonIcon
} from '@ionic/angular';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-verify-email',
  templateUrl: './verify-email.page.html',
  styleUrls: ['./verify-email.page.scss'],
  standalone: true,
  imports: [
    CommonModule,
    IonContent,
    IonSpinner,
    IonButton,
    IonIcon
  ]
})
export class VerifyEmailPage implements OnInit {

  isLoading = signal(true);
  isVerified = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  private email = '';
  private token = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') || '';
    this.token = this.route.snapshot.queryParamMap.get('token') || '';

    if (!this.email || !this.token) {
      this.isLoading.set(false);
      this.errorMessage.set(
        'The verification link is incomplete.'
      );
      return;
    }

    this.verifyEmail();
  }

  verifyEmail(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.authService.verifyEmail({
      email: this.email,
      token: this.token
    }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.isVerified.set(true);
      },
      error: (error) => {
        this.isLoading.set(false);
        this.isVerified.set(false);

        this.errorMessage.set(
          error.error?.message ||
          'Verification failed. Your link may have expired or already been used.'
        );
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/auth/login']);
  }

  resendVerification(): void {
    if (!this.email || this.isLoading()) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.authService.resendVerification(this.email).subscribe({
      next: (response) => {
        this.isLoading.set(false);

        this.successMessage.set(
          response.message ||
          'If your account is still unverified, a new verification email has been sent.'
        );
      },
      error: (error) => {
        this.isLoading.set(false);

        this.errorMessage.set(
          error.error?.message ||
          'Unable to resend the verification email. Please try again.'
        );
      }
    });
  }
}
