
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RegisterRequest {
  fullName: string;
  username: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface PlayerResponse {
  id: string;
  username: string;
  fullName: string;
  email: string;
  createdAt: string;
  isEmailVerified: boolean;
  message: string;
}

export interface AuthResponse {
  token: string;
  playerId: string;
  username: string;
  fullName: string;
  email: string;
}

export interface VerifyEmailRequest {
  email: string;
  token: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private readonly apiUrl = `${environment.apiUrl}/Player`;

  constructor(private http: HttpClient) {}

  // Register
  register(request: RegisterRequest): Observable<PlayerResponse> {
    return this.http.post<PlayerResponse>(
      `${this.apiUrl}/register`,
      request
    );
  }

  // Login
  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.apiUrl}/login`,
      request
    ).pipe(
      tap((response) => {
        localStorage.setItem('authToken', response.token);

        localStorage.setItem(
          'currentPlayer',
          JSON.stringify({
            playerId: response.playerId,
            username: response.username,
            fullName: response.fullName,
            email: response.email
          })
        );
      })
    );
  }

  // Verify email
  verifyEmail(request: VerifyEmailRequest): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/verify-email`,
      request
    );
  }

  // Resend verification email
  resendVerification(email: string): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/resend-verification`,
      { email }
    );
  }

  // Forgot password
  forgotPassword(request: ForgotPasswordRequest): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/forgot-password`,
      request
    );
  }

  // Reset password
  resetPassword(request: ResetPasswordRequest): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/reset-password`,
      request
    );
  }

  // Logout
  logout(): void {
    localStorage.removeItem('authToken');
    localStorage.removeItem('currentPlayer');
  }

  // Get authentication token
  getToken(): string | null {
    return localStorage.getItem('authToken');
  }

  // Check whether user is logged in
  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  // Get current player
  getCurrentPlayer(): {
    playerId: string;
    username: string;
    fullName: string;
    email: string;
  } | null {

    const player = localStorage.getItem('currentPlayer');

    if (!player) {
      return null;
    }

    try {
      return JSON.parse(player);
    } catch {
      return null;
    }
  }
}
