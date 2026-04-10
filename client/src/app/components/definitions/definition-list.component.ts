import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-definition-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-8 max-w-[1400px]">
      <h1 class="page-header">Settings by Application</h1>

      <div class="flex gap-6">
        <!-- App sidebar -->
        <div class="w-56 flex-shrink-0">
          <div class="card">
            <div class="px-4 py-3 border-b border-surface-200">
              <h3 class="text-xs font-semibold text-surface-400 uppercase tracking-wider">Applications</h3>
            </div>
            <div class="py-1">
              <button
                (click)="selectApp('')"
                class="w-full text-left px-4 py-2 text-sm transition-colors"
                [class]="!selectedApp ? 'bg-primary-50 text-primary-600 font-medium' : 'text-surface-600 hover:bg-surface-50'">
                All ({{ totalCount }})
              </button>
              <button *ngFor="let app of appCodes"
                (click)="selectApp(app.code)"
                class="w-full text-left px-4 py-2 text-sm transition-colors"
                [class]="selectedApp === app.code ? 'bg-primary-50 text-primary-600 font-medium' : 'text-surface-600 hover:bg-surface-50'">
                {{ app.code }} ({{ app.count }})
              </button>
            </div>
          </div>
        </div>

        <!-- Main content -->
        <div class="flex-1 min-w-0">
          <!-- Search -->
          <div class="mb-4">
            <input
              type="text"
              class="form-input"
              placeholder="Search settings..."
              [(ngModel)]="searchTerm"
              (ngModelChange)="onSearchChange($event)"
            />
          </div>

          <!-- Category groups -->
          <div *ngIf="!searchTerm && categories.length > 0" class="space-y-4">
            <div *ngFor="let cat of categories" class="card">
              <div class="px-5 py-3 border-b border-surface-200 flex items-center justify-between cursor-pointer"
                   (click)="toggleCategory(cat.name)">
                <h3 class="text-sm font-semibold text-surface-700">
                  {{ cat.name || 'Uncategorized' }}
                  <span class="text-surface-400 font-normal ml-1">({{ cat.definitions.length }})</span>
                </h3>
                <svg class="w-4 h-4 text-surface-400 transition-transform" [class.rotate-180]="expandedCategory === cat.name"
                     fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                </svg>
              </div>
              <div *ngIf="expandedCategory === cat.name || expandedCategory === '__all__'" class="divide-y divide-surface-100">
                <div *ngFor="let def of cat.definitions"
                     class="px-5 py-3 hover:bg-surface-50 cursor-pointer transition-colors"
                     [class.bg-primary-50]="expandedKey === def.key"
                     (click)="toggleDetail(def); $event.stopPropagation()">
                  <div class="flex items-center justify-between">
                    <div class="min-w-0">
                      <div class="text-sm font-mono font-medium text-primary-600">{{ def.key }}</div>
                      <div class="text-xs text-surface-500 mt-0.5">{{ def.displayName }} &mdash; {{ def.description || 'No description' }}</div>
                    </div>
                    <div class="flex items-center gap-2 flex-shrink-0 ml-4">
                      <span class="badge badge-info">{{ def.dataType }}</span>
                      <span *ngIf="def.isSecret" class="badge badge-danger">Secret</span>
                    </div>
                  </div>

                  <!-- Expanded detail + values -->
                  <div *ngIf="expandedKey === def.key" class="mt-3 pt-3 border-t border-surface-200" (click)="$event.stopPropagation()">
                    <div class="grid grid-cols-2 lg:grid-cols-3 gap-3 mb-4">
                      <div>
                        <span class="text-xs font-semibold text-surface-400 uppercase">Default</span>
                        <div class="text-sm font-mono text-surface-900 mt-0.5">{{ def.defaultValueJson || '(none)' }}</div>
                      </div>
                      <div>
                        <span class="text-xs font-semibold text-surface-400 uppercase">Allowed Scopes</span>
                        <div class="text-sm text-surface-900 mt-0.5">{{ def.allowedScopesJson || '(all)' }}</div>
                      </div>
                      <div>
                        <span class="text-xs font-semibold text-surface-400 uppercase">Assignments</span>
                        <div class="text-sm text-surface-900 mt-0.5">{{ def.assignmentCount }}</div>
                      </div>
                    </div>

                    <!-- Inline value editor -->
                    <div class="bg-surface-50 rounded-lg p-4">
                      <h4 class="text-xs font-semibold text-surface-400 uppercase mb-3">Set Value</h4>
                      <div class="flex gap-2 items-end">
                        <div class="flex-1">
                          <label class="form-label">Scope</label>
                          <select class="form-select" [(ngModel)]="newScope">
                            <option value="Machine">Machine</option>
                            <option value="Application">Application</option>
                            <option value="Service">Service</option>
                            <option value="User">User</option>
                            <option value="Team">Team</option>
                            <option value="Workspace">Workspace</option>
                          </select>
                        </div>
                        <div class="w-32">
                          <label class="form-label">Scope ID</label>
                          <input class="form-input" [(ngModel)]="newScopeId" placeholder="(optional)"/>
                        </div>
                        <div class="flex-1">
                          <label class="form-label">Value</label>
                          <input class="form-input font-mono" [(ngModel)]="newValue" [attr.type]="def.isSecret ? 'password' : 'text'" placeholder='e.g. "docker"'/>
                        </div>
                        <button class="btn-primary btn-sm" (click)="saveValue(def)">Save</button>
                      </div>
                      <div *ngIf="saveMessage" class="mt-2 text-sm" [class]="saveError ? 'text-red-600' : 'text-green-600'">{{ saveMessage }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Flat search results -->
          <div *ngIf="searchTerm" class="card">
            <div class="divide-y divide-surface-100">
              <div *ngFor="let def of definitions"
                   class="px-5 py-3 hover:bg-surface-50 cursor-pointer transition-colors"
                   (click)="toggleDetail(def)">
                <div class="flex items-center justify-between">
                  <div>
                    <div class="text-sm font-mono font-medium text-primary-600">{{ def.key }}</div>
                    <div class="text-xs text-surface-500 mt-0.5">{{ def.applicationCode }} / {{ def.category }} &mdash; {{ def.displayName }}</div>
                  </div>
                  <div class="flex items-center gap-2">
                    <span class="badge badge-info">{{ def.dataType }}</span>
                    <span *ngIf="def.isSecret" class="badge badge-danger">Secret</span>
                  </div>
                </div>
              </div>
              <div *ngIf="definitions.length === 0" class="px-5 py-8 text-center text-sm text-surface-400">
                No definitions found matching "{{ searchTerm }}"
              </div>
            </div>
          </div>

          <!-- Pagination -->
          <div class="flex items-center justify-center gap-4 mt-4" *ngIf="totalPages > 1">
            <button class="btn-secondary btn-sm" [disabled]="page <= 1" (click)="goToPage(page - 1)">Previous</button>
            <span class="text-sm text-surface-500">Page {{ page }} of {{ totalPages }}</span>
            <button class="btn-secondary btn-sm" [disabled]="page >= totalPages" (click)="goToPage(page + 1)">Next</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class DefinitionListComponent implements OnInit {
  definitions: any[] = [];
  searchTerm = '';
  selectedApp = '';
  appCodes: { code: string; count: number }[] = [];
  categories: { name: string; definitions: any[] }[] = [];
  page = 1;
  pageSize = 100;
  totalCount = 0;
  totalPages = 1;
  expandedKey: string | null = null;
  expandedCategory: string | null = '__all__';

  newScope = 'Machine';
  newScopeId = '';
  newValue = '';
  saveMessage = '';
  saveError = false;

  private searchSubject = new Subject<string>();

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => {
      this.page = 1;
      this.loadDefinitions();
    });
    this.loadDefinitions();
    this.loadAppCodes();
  }

  onSearchChange(term: string) {
    this.searchSubject.next(term);
  }

  selectApp(code: string) {
    this.selectedApp = code;
    this.page = 1;
    this.expandedKey = null;
    this.loadDefinitions();
  }

  loadDefinitions() {
    this.api.getDefinitions({
      search: this.searchTerm || undefined,
      applicationCode: this.selectedApp || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe((data: any) => {
      this.definitions = data.items || [];
      this.totalCount = data.totalCount || 0;
      this.totalPages = Math.max(1, Math.ceil(this.totalCount / this.pageSize));
      this.buildCategories();
    });
  }

  loadAppCodes() {
    this.api.getDefinitions({ pageSize: 200 }).subscribe((data: any) => {
      const counts = new Map<string, number>();
      for (const d of data.items || []) {
        counts.set(d.applicationCode, (counts.get(d.applicationCode) || 0) + 1);
      }
      this.appCodes = Array.from(counts.entries())
        .map(([code, count]) => ({ code, count }))
        .sort((a, b) => a.code.localeCompare(b.code));
    });
  }

  buildCategories() {
    const groups = new Map<string, any[]>();
    for (const def of this.definitions) {
      const cat = def.category || '';
      if (!groups.has(cat)) groups.set(cat, []);
      groups.get(cat)!.push(def);
    }
    this.categories = Array.from(groups.entries())
      .map(([name, definitions]) => ({ name, definitions }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  toggleCategory(name: string) {
    this.expandedCategory = this.expandedCategory === name ? null : name;
  }

  toggleDetail(def: any) {
    if (this.expandedKey === def.key) {
      this.expandedKey = null;
      this.saveMessage = '';
    } else {
      this.expandedKey = def.key;
      this.newScope = 'Machine';
      this.newScopeId = '';
      this.newValue = '';
      this.saveMessage = '';
    }
  }

  saveValue(def: any) {
    this.saveMessage = '';
    const valueJson = def.dataType === 'String' || def.dataType === 'Uri'
      ? JSON.stringify(this.newValue)
      : this.newValue;

    this.api.setValue({
      definitionKey: def.key,
      scopeType: this.newScope,
      scopeId: this.newScopeId || undefined,
      valueJson
    }).subscribe({
      next: () => {
        this.saveMessage = 'Value saved successfully';
        this.saveError = false;
        this.loadDefinitions();
      },
      error: (err: any) => {
        this.saveMessage = `Error: ${err.status} ${err.statusText}`;
        this.saveError = true;
      }
    });
  }

  goToPage(p: number) {
    this.page = p;
    this.expandedKey = null;
    this.loadDefinitions();
  }
}
