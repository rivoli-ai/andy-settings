import { TestBed } from '@angular/core/testing';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of } from 'rxjs';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let oidc: jasmine.SpyObj<OidcSecurityService>;

  beforeEach(() => {
    oidc = jasmine.createSpyObj<OidcSecurityService>(
      'OidcSecurityService', ['checkAuth', 'authorize', 'logoffAndRevokeTokens']);
    oidc.checkAuth.and.returnValue(of({
      isAuthenticated: true,
      accessToken: 'access-token',
      userData: { name: 'Test User', email: 'test@example.com' }
    } as any));
    oidc.logoffAndRevokeTokens.and.returnValue(of(null));

    TestBed.configureTestingModule({
      providers: [AuthService, { provide: OidcSecurityService, useValue: oidc }]
    });
    service = TestBed.inject(AuthService);
  });

  it('initializes authentication, token, and user claims from OIDC', () => {
    service.init().subscribe(authenticated => expect(authenticated).toBeTrue());

    expect(service.isAuthenticated).toBeTrue();
    expect(service.getToken()).toBe('access-token');
    expect(service.getUserName()).toBe('Test User');
    expect(service.getEmail()).toBe('test@example.com');
  });

  it('delegates login and logout to the OIDC client', () => {
    service.login();
    service.logout();

    expect(oidc.authorize).toHaveBeenCalled();
    expect(oidc.logoffAndRevokeTokens).toHaveBeenCalled();
  });
});
