import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="sidebar">
      <div class="sidebar-header">
        <h2>Andy Settings</h2>
      </div>
      <ul class="sidebar-nav">
        <li><a routerLink="/dashboard" routerLinkActive="active">Dashboard</a></li>
        <li><a routerLink="/definitions" routerLinkActive="active">Definitions</a></li>
        <li><a routerLink="/values" routerLinkActive="active">Values</a></li>
        <li><a routerLink="/effective" routerLinkActive="active">Effective</a></li>
        <li><a routerLink="/secrets" routerLinkActive="active">Secrets</a></li>
        <li><a routerLink="/audit" routerLinkActive="active">Audit</a></li>
        <li><a routerLink="/import-export" routerLinkActive="active">Import / Export</a></li>
      </ul>
    </nav>
  `,
  styles: [`
    .sidebar {
      width: 240px;
      min-height: 100vh;
      background: #1a1a2e;
      color: #e0e0e0;
      padding: 0;
      display: flex;
      flex-direction: column;
    }
    .sidebar-header {
      padding: 20px;
      border-bottom: 1px solid #2a2a4a;
    }
    .sidebar-header h2 {
      margin: 0;
      font-size: 18px;
      color: #fff;
    }
    .sidebar-nav {
      list-style: none;
      padding: 8px 0;
      margin: 0;
    }
    .sidebar-nav li a {
      display: block;
      padding: 10px 20px;
      color: #b0b0c0;
      text-decoration: none;
      font-size: 14px;
      transition: background 0.15s, color 0.15s;
    }
    .sidebar-nav li a:hover {
      background: #2a2a4a;
      color: #fff;
    }
    .sidebar-nav li a.active {
      background: #3a3a6a;
      color: #fff;
      border-left: 3px solid #6c63ff;
    }
  `]
})
export class SidebarComponent {}
