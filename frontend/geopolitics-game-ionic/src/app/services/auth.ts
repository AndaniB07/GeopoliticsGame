import { Injectable } from'@angular/core';
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
}

export interface AuthResponse {
    token: string;
    playerId: string;
    username: string;
    fullName: string;
    email: string;
}

@Injectable({
    providedIn: 'root'
})

export class AuthService {
    private readonly apiUrl = `${environment.apiUrl}/Player`;

    constructor(private http: HttpClient) {}

    register(request: RegisterRequest): Observable<PlayerResponse> {
        return this.http.post<PlayerResponse>(
        `${this.apiUrl}/register`,
        request
        );
    }

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


  logout(): void {
    localStorage.removeItem('authToken');
    localStorage.removeItem('currentPlayer');
  }


  getToken(): string | null {
    return localStorage.getItem('authToken');
  }

  isLoggedIn(): boolean {
  return !!this.getToken();
}


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