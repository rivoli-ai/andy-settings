import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  template: `
    <header class="topbar">
      <div class="topbar-spacer"></div>
      <div class="topbar-user">
        <span class="user-name">{{ auth.getUserName() }}</span>
        <span class="user-email">{{ auth.getEmail() }}</span>
        <button class="btn-logout" (click)="auth.logout()">Logout</button>
      </div>
    </header>
  `,
  styles: [`
    .topbar {
      height: 56px;
      background: #fff;
      border-bottom: 1px solid #e0e0e0;
      display: flex;
      align-items: center;
      padding: 0 20px;
    }
    .topbar-spacer { flex: 1; }
    .topbar-user {
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 13px;
    }
    .user-name { font-weight: 600; }
    .user-email { color: #888; }
    .btn-logout {
      padding: 6px 12px;
      border: 1px solid #ddd;
      background: #fff;
      border-radius: 4px;
      cursor: pointer;
      font-size: 13px;
    }
    .btn-logout:hover { background: #f5f5f5; }
  `]
})
export class TopbarComponent {
  constructor(public auth: AuthService) {}
}
