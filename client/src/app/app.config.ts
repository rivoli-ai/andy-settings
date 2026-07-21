import { APP_INITIALIZER, ApplicationConfig, EnvironmentProviders, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './interceptors/auth.interceptor';
import { provideAuth } from 'angular-auth-oidc-client';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environments/environment';
import { AuthService } from './services/auth.service';

function oidcProviders(): EnvironmentProviders[] {
  return environment.authAuthority ? [provideAuth({
    config: {
      authority: environment.authAuthority,
      redirectUrl: window.location.origin + '/callback',
      postLogoutRedirectUri: window.location.origin,
      clientId: environment.authClientId,
      scope: 'openid profile email',
      responseType: 'code',
      silentRenew: true,
      useRefreshToken: true,
      autoUserInfo: true,
    }
  })] : [];
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    ...oidcProviders(),
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [AuthService],
      useFactory: (auth: AuthService) => () => firstValueFrom(auth.init()),
    },
  ]
};
