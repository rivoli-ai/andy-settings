import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './components/layout/sidebar.component';
import { TopbarComponent } from './components/layout/topbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  template: `
    <div class="app-layout">
      <app-sidebar />
      <div class="app-main">
        <app-topbar />
        <div class="app-content">
          <router-outlet />
        </div>
      </div>
    </div>
  `,
  styles: [`
    .app-layout {
      display: flex;
      min-height: 100vh;
    }
    .app-main {
      flex: 1;
      display: flex;
      flex-direction: column;
      background: #f5f5f8;
    }
    .app-content {
      flex: 1;
      overflow-y: auto;
    }
  `]
})
export class AppComponent {}
