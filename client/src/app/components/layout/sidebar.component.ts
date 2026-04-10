import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="w-[280px] min-h-screen bg-surface-900 text-surface-300 flex flex-col">
      <div class="px-5 py-4 border-b border-surface-700">
        <h2 class="text-lg font-semibold text-white m-0">Andy Settings</h2>
      </div>
      <ul class="list-none p-0 m-0 py-2">
        <li>
          <a routerLink="/dashboard" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Dashboard
          </a>
        </li>
        <li>
          <a routerLink="/definitions" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Definitions
          </a>
        </li>
        <li>
          <a routerLink="/values" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Values
          </a>
        </li>
        <li>
          <a routerLink="/effective" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Effective
          </a>
        </li>
        <li>
          <a routerLink="/secrets" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Secrets
          </a>
        </li>
        <li>
          <a routerLink="/audit" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Audit
          </a>
        </li>
        <li>
          <a routerLink="/import-export" routerLinkActive="bg-surface-800 !text-white !border-l-[3px] !border-primary-500"
             class="block px-5 py-2.5 text-sm text-surface-400 hover:bg-surface-800 hover:text-white transition-colors no-underline border-l-[3px] border-transparent">
            Import / Export
          </a>
        </li>
      </ul>
    </nav>
  `
})
export class SidebarComponent {}
