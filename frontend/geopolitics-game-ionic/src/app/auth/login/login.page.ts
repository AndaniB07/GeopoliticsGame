
import { Component } from '@angular/core';
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
  usernameOrEmail = '';
  password = '';
  isLoading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  async login(): Promise<void> {
    if (!this.usernameOrEmail.trim() || !this.password) {
      await this.showToast(
        'Please enter your username or email and password.'
      );
      return;
    }

    if (this.isLoading) {
      return;
    }

    this.isLoading = true;

    this.authService.login({
      usernameOrEmail: this.usernameOrEmail.trim(),
      password: this.password
    }).subscribe({
      next: async () => {
        this.isLoading = false;

        await this.showToast('Login successful. Welcome back!');

        await this.router.navigate(['/dashboard']);
      },
      error: async (error) => {
        this.isLoading = false;

        const message =
          error.error?.message ??
          'Login failed. Check your details and try again.';

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
