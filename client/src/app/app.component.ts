import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './components/layout/sidebar.component';
import { TopbarComponent } from './components/layout/topbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  template: `
    <div class="flex min-h-screen">
      <app-sidebar />
      <div class="flex-1 flex flex-col bg-surface-50">
        <app-topbar />
        <div class="flex-1 overflow-y-auto">
          <router-outlet />
        </div>
      </div>
    </div>
  `
})
export class AppComponent {}
