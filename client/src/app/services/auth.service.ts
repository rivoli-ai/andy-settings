import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, map, of } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oidc = inject(OidcSecurityService, { optional: true });
  private readonly oidcEnabled = !!environment.authAuthority;
  private authenticated = !this.oidcEnabled;
  private token: string | null = null;
  private userName = this.oidcEnabled ? '' : 'Developer';
  private email = this.oidcEnabled ? '' : 'test@andy.local';

  init(): Observable<boolean> {
    if (!this.oidcEnabled || !this.oidc) return of(true);
    return this.oidc.checkAuth().pipe(
      map(result => {
        this.authenticated = result.isAuthenticated;
        this.token = result.accessToken || null;
        this.userName = result.userData?.name || result.userData?.preferred_username || '';
        this.email = result.userData?.email || '';
        return result.isAuthenticated;
      })
    );
  }

  get isAuthenticated(): boolean {
    return this.authenticated;
  }

  getToken(): string | null {
    return this.token;
  }

  login(): void {
    if (this.oidcEnabled) this.oidc?.authorize();
    else this.authenticated = true;
  }

  logout(): void {
    if (this.oidcEnabled) this.oidc?.logoffAndRevokeTokens().subscribe();
    else this.authenticated = false;
  }

  getUserName(): string {
    return this.userName;
  }

  getEmail(): string {
    return this.email;
  }
}
