
import { Component, computed, signal } from '@angular/core';
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
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.page.html',
  styleUrls: ['./reset-password.page.scss'],
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
    IonSpinner
  ]
})
export class ResetPasswordPage {

  email = '';
  token = '';

  newPassword = signal('');
  confirmPassword = signal('');

  isLoading = signal(false);
  isReset = signal(false);

  errorMessage = signal('');
  successMessage = signal('');

  // Password validation requirements
  passwordRequirements = computed(() => {
    const password = this.newPassword();

    return {
      minLength: password.length >= 8,
      uppercase: /[A-Z]/.test(password),
      lowercase: /[a-z]/.test(password),
      number: /[0-9]/.test(password),
      specialCharacter: /[^A-Za-z0-9\s]/.test(password)
    };
  });

  // Check whether all password requirements are satisfied
  isPasswordValid = computed(() => {
    const requirements = this.passwordRequirements();

    return (
      requirements.minLength &&
      requirements.uppercase &&
      requirements.lowercase &&
      requirements.number &&
      requirements.specialCharacter
    );
  });

  // Check whether both passwords match
  passwordsMatch = computed(() => {
    return (
      this.newPassword().length > 0 &&
      this.confirmPassword().length > 0 &&
      this.newPassword() === this.confirmPassword()
    );
  });

  // Check whether the email and token are valid
  isResetLinkValid = computed(() => {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    return (
      emailPattern.test(this.email) &&
      this.token.trim().length > 0
    );
  });

  // Check whether the complete form is valid
  isFormValid = computed(() => {
    return (
      this.isResetLinkValid() &&
      this.isPasswordValid() &&
      this.passwordsMatch()
    );
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) {
    this.email = (
      this.route.snapshot.queryParamMap.get('email') || ''
    ).trim();

    this.token =
      this.route.snapshot.queryParamMap.get('token') || '';

    if (!this.isResetLinkValid()) {
      this.errorMessage.set(
        'The password reset link is incomplete or invalid. Please request a new reset link.'
      );
    }
  }

  resetPassword(): void {

    this.errorMessage.set('');

    if (this.isLoading()) {
      return;
    }

    if (!this.isResetLinkValid()) {
      this.errorMessage.set(
        'Invalid password reset link. Please request a new one.'
      );
      return;
    }

    if (!this.newPassword() || !this.confirmPassword()) {
      this.errorMessage.set(
        'Please complete both password fields.'
      );
      return;
    }

    if (!this.isPasswordValid()) {
      this.errorMessage.set(
        'Your password does not meet all the security requirements.'
      );
      return;
    }

    if (!this.passwordsMatch()) {
      this.errorMessage.set(
        'Your passwords do not match.'
      );
      return;
    }

    this.isLoading.set(true);

    this.authService.resetPassword({
      email: this.email,
      token: this.token,
      newPassword: this.newPassword()
    }).subscribe({

      next: (response) => {
        this.isLoading.set(false);
        this.isReset.set(true);

        this.successMessage.set(
          response.message ||
          'Your password has been successfully changed.'
        );

        this.newPassword.set('');
        this.confirmPassword.set('');
      },

      error: (error) => {
        this.isLoading.set(false);

        this.errorMessage.set(
          error.error?.message ||
          'Password reset failed. Your link may have expired. Please request a new one.'
        );
      }

    });
  }

  goToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
