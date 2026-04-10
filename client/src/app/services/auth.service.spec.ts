import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should be authenticated by default (dev mode)', () => {
    expect(service.isAuthenticated).toBeTrue();
  });

  it('should return null token in dev mode', () => {
    expect(service.getToken()).toBeNull();
  });

  it('should return Developer as username', () => {
    expect(service.getUserName()).toBe('Developer');
  });

  it('should return test email', () => {
    expect(service.getEmail()).toBe('test@andy.local');
  });

  it('should set isAuthenticated to false on logout', () => {
    service.logout();
    expect(service.isAuthenticated).toBeFalse();
  });
});
