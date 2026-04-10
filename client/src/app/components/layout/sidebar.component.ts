import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="w-[280px] min-h-screen bg-white border-r border-surface-200 flex flex-col">
      <div class="flex items-center h-16 px-5 border-b border-surface-200">
        <span class="text-xl font-semibold text-primary-500 tracking-tight">Andy Settings</span>
      </div>
      <div class="flex-1 py-3 px-3">
        <a routerLink="/dashboard" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Dashboard
        </a>
        <a routerLink="/definitions" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Definitions
        </a>
        <a routerLink="/values" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Values
        </a>
        <a routerLink="/effective" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Effective
        </a>
        <a routerLink="/secrets" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Secrets
        </a>
        <a routerLink="/audit" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Audit
        </a>
        <a routerLink="/import-export" routerLinkActive="!bg-primary-50 !text-primary-500"
           class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium text-surface-500 hover:text-surface-900 hover:bg-surface-100 rounded-md transition-all mb-0.5 no-underline">
          Import / Export
        </a>
      </div>
    </nav>
  `
})
export class SidebarComponent {}
