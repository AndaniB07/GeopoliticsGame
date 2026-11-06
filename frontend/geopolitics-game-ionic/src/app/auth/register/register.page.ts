
import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonContent,
  IonButton,
  IonIcon,
  IonInput,
  ToastController
} from '@ionic/angular';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-register',
  templateUrl: './register.page.html',
  styleUrls: ['./register.page.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    IonContent,
    IonButton,
    IonIcon,
    IonInput
  ]
})
export class RegisterPage {

  // Form values
  fullName = '';
  username = '';
  email = '';

  // Password values are Signals
  password = signal('');
  confirmPassword = signal('');

  // Loading and validation states
  isLoading = signal(false);
  emailError = signal('');
  confirmPasswordError = signal('');

  // Password requirements update as the user types
  passwordRequirements = computed(() => {
    const value = this.password();

    return {
      length: value.length >= 8,
      uppercase: /[A-Z]/.test(value),
      lowercase: /[a-z]/.test(value),
      number: /[0-9]/.test(value),
      symbol: /[^A-Za-z0-9]/.test(value)
    };
  });

  // Checks whether every password requirement is satisfied
  isPasswordValid = computed(() => {
    const requirements = this.passwordRequirements();

    return (
      requirements.length &&
      requirements.uppercase &&
      requirements.lowercase &&
      requirements.number &&
      requirements.symbol
    );
  });

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  // Email validation
  validateEmail(): void {
    const value = this.email.trim();

    if (!value) {
      this.emailError.set('Email address is required.');
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!emailPattern.test(value)) {
      this.emailError.set('Please enter a valid email address.');
      return;
    }

    this.emailError.set('');
  }

  // Password validation
  validatePassword(): void {
    this.validateConfirmPassword();
  }

  // Confirm password validation
  validateConfirmPassword(): void {
    const passwordValue = this.password();
    const confirmValue = this.confirmPassword();

    if (!confirmValue) {
      this.confirmPasswordError.set('');
      return;
    }

    if (passwordValue !== confirmValue) {
      this.confirmPasswordError.set('Passwords do not match.');
      return;
    }

    this.confirmPasswordError.set('');
  }

  // Register account
  async register(): Promise<void> {

    this.validateEmail();
    this.validateConfirmPassword();

    // Check required fields
    if (
      !this.fullName.trim() ||
      !this.username.trim() ||
      !this.email.trim() ||
      !this.password() ||
      !this.confirmPassword()
    ) {
      if (!this.email.trim()) {
        this.emailError.set('Email address is required.');
      }

      await this.showToast('Please complete all fields.');
      return;
    }

    // Check email
    if (this.emailError()) {
      await this.showToast('Please enter a valid email address.');
      return;
    }

    // Check password requirements
    if (!this.isPasswordValid()) {
      await this.showToast(
        'Please make sure your password meets all the requirements.'
      );
      return;
    }

    // Check matching passwords
    if (this.password() !== this.confirmPassword()) {
      this.confirmPasswordError.set('Passwords do not match.');
      await this.showToast('Your passwords do not match.');
      return;
    }

    // Prevent multiple submissions
    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);

    this.authService.register({
      fullName: this.fullName.trim(),
      username: this.username.trim(),
      email: this.email.trim(),
      password: this.password()
    }).subscribe({

      next: async () => {
        this.isLoading.set(false);

        await this.showToast(
          'Account created successfully! Please check your email to verify your account.'
        );

        await this.router.navigate(['/auth/login']);
      },

      error: async (error) => {
        this.isLoading.set(false);

        const message =
          error.error?.message ||
          'Registration failed. Please try again.';

        await this.showToast(message);
      }

    });
  }

  // Display toast messages
  private async showToast(message: string): Promise<void> {
    const toast = await this.toastController.create({
      message: message,
      duration: 4000,
      position: 'bottom'
    });

    await toast.present();
  }
}
