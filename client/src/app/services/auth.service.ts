import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // Placeholder — Story #29 will integrate angular-auth-oidc-client with Andy Auth
  private _isAuthenticated = true; // dev mode: always authenticated

  get isAuthenticated(): boolean {
    return this._isAuthenticated;
  }

  getToken(): string | null {
    return null; // dev mode: no token needed
  }

  login(): void {
    // Will redirect to Andy Auth OIDC login
    console.log('Login not yet implemented');
  }

  logout(): void {
    this._isAuthenticated = false;
  }

  getUserName(): string {
    return 'Developer';
  }

  getEmail(): string {
    return 'test@andy.local';
  }
}
