import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  template: `
    <header class="h-16 bg-white border-b border-surface-200 flex items-center px-6">
      <div class="flex-1"></div>
      <div class="flex items-center gap-3 text-sm">
        <span class="font-semibold text-surface-900">{{ auth.getUserName() }}</span>
        <span class="text-surface-400">{{ auth.getEmail() }}</span>
        <button class="btn-secondary btn-sm" (click)="auth.logout()">Logout</button>
      </div>
    </header>
  `
})
export class TopbarComponent {
  constructor(public auth: AuthService) {}
}
