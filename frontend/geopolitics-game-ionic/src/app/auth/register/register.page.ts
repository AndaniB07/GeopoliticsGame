
import { Component } from '@angular/core';
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
    IonInput,
    IonLabel
  ]
})
export class RegisterPage {
  fullName = '';
  username = '';
  email = '';
  password = '';
  confirmPassword = '';

  isLoading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  async register(): Promise<void> {
    if (
      !this.fullName.trim() ||
      !this.username.trim() ||
      !this.email.trim() ||
      !this.password ||
      !this.confirmPassword
    ) {
      await this.showToast('Please complete all fields.');
      return;
    }

    if (this.password.length < 8) {
      await this.showToast(
        'Your password must contain at least 8 characters.'
      );
      return;
    }

    if (this.password !== this.confirmPassword) {
      await this.showToast('Your passwords do not match.');
      return;
    }

    if (this.isLoading) {
      return;
    }

    this.isLoading = true;

    this.authService.register({
      fullName: this.fullName.trim(),
      username: this.username.trim(),
      email: this.email.trim(),
      password: this.password
    }).subscribe({
      next: async () => {
        this.isLoading = false;

        await this.showToast(
          'Account created successfully! You can now sign in.'
        );

        await this.router.navigate(['/auth/login']);
      },
      error: async (error) => {
        this.isLoading = false;

        const message =
          error.error?.message ??
          'Registration failed. Please try again.';

        await this.showToast(message);
      }
    });
  }

  private async showToast(message: string): Promise<void> {
    const toast = await this.toastController.create({
      message,
      duration: 3000,
      position: 'bottom'
    });

    await toast.present();
  }
}
