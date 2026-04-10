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
    <div class="p-6">
      <h1 class="page-header">Definitions</h1>

      <div class="flex gap-3 mb-4">
        <input
          type="text"
          class="form-input flex-1"
          placeholder="Search definitions..."
          [(ngModel)]="searchTerm"
          (ngModelChange)="onSearchChange($event)"
        />
        <select class="form-select min-w-[180px] w-auto" [(ngModel)]="appCodeFilter" (ngModelChange)="onAppCodeChange()">
          <option value="">All Applications</option>
          <option *ngFor="let app of appCodes" [value]="app">{{ app }}</option>
        </select>
      </div>

      <div class="table-wrapper">
        <table class="min-w-full divide-y divide-surface-200 bg-white border border-surface-200 rounded-lg">
          <thead class="bg-surface-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Key</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">App</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Display Name</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Data Type</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Category</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Secret</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-surface-200">
            <tr *ngFor="let def of definitions"
                (click)="toggleDetail(def)"
                class="cursor-pointer hover:bg-surface-50 transition-colors"
                [class.bg-primary-50]="expandedKey === def.key">
              <td class="px-4 py-3 text-sm font-mono font-semibold text-primary-500">{{ def.key }}</td>
              <td class="px-4 py-3 text-sm">{{ def.applicationCode }}</td>
              <td class="px-4 py-3 text-sm">{{ def.displayName }}</td>
              <td class="px-4 py-3 text-sm"><span class="badge badge-info">{{ def.dataType }}</span></td>
              <td class="px-4 py-3 text-sm">{{ def.category }}</td>
              <td class="px-4 py-3 text-sm">
                <span [class]="def.isSecret ? 'badge badge-danger' : 'badge badge-default'">
                  {{ def.isSecret ? 'Yes' : 'No' }}
                </span>
              </td>
            </tr>
            <tr *ngIf="expandedKey">
              <td colspan="6" class="p-0 bg-surface-50">
                <div class="p-5 border-t-2 border-primary-500" *ngIf="expandedDef">
                  <h3 class="text-base font-semibold text-surface-900 mb-3">{{ expandedDef.key }}</h3>
                  <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Description</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.description || '(none)' }}</span>
                    </div>
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Default Value</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.defaultValue ?? '(none)' }}</span>
                    </div>
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Validation Regex</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.validationRegex || '(none)' }}</span>
                    </div>
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Allowed Values</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.allowedValues?.join(', ') || '(none)' }}</span>
                    </div>
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Created At</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.createdAt }}</span>
                    </div>
                    <div class="flex flex-col gap-0.5">
                      <span class="text-xs font-semibold text-surface-400 uppercase">Updated At</span>
                      <span class="text-sm text-surface-900">{{ expandedDef.updatedAt }}</span>
                    </div>
                  </div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-center gap-4 mt-4">
        <button class="btn-primary btn-sm" [disabled]="page <= 1" (click)="goToPage(page - 1)"
                [class.opacity-50]="page <= 1" [class.cursor-not-allowed]="page <= 1">Previous</button>
        <span class="text-sm text-surface-500">Page {{ page }} of {{ totalPages }} ({{ totalCount }} total)</span>
        <button class="btn-primary btn-sm" [disabled]="page >= totalPages" (click)="goToPage(page + 1)"
                [class.opacity-50]="page >= totalPages" [class.cursor-not-allowed]="page >= totalPages">Next</button>
      </div>
    </div>
  `
})
export class DefinitionListComponent implements OnInit {
  definitions: any[] = [];
  searchTerm = '';
  appCodeFilter = '';
  appCodes: string[] = [];
  page = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 1;
  expandedKey: string | null = null;
  expandedDef: any = null;

  private searchSubject = new Subject<string>();

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(() => {
      this.page = 1;
      this.loadDefinitions();
    });

    this.loadDefinitions();
    this.loadAppCodes();
  }

  onSearchChange(term: string) {
    this.searchSubject.next(term);
  }

  onAppCodeChange() {
    this.page = 1;
    this.loadDefinitions();
  }

  loadDefinitions() {
    this.api.getDefinitions({
      search: this.searchTerm || undefined,
      applicationCode: this.appCodeFilter || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe((data: any) => {
      this.definitions = data.items || [];
      this.totalCount = data.totalCount || 0;
      this.totalPages = Math.max(1, Math.ceil(this.totalCount / this.pageSize));
    });
  }

  loadAppCodes() {
    this.api.getDefinitions({ pageSize: 200 }).subscribe((data: any) => {
      const codes = new Set<string>(
        (data.items || []).map((d: any) => d.applicationCode).filter(Boolean)
      );
      this.appCodes = Array.from(codes).sort();
    });
  }

  toggleDetail(def: any) {
    if (this.expandedKey === def.key) {
      this.expandedKey = null;
      this.expandedDef = null;
    } else {
      this.expandedKey = def.key;
      this.expandedDef = null;
      this.api.getDefinition(def.key).subscribe((detail: any) => {
        this.expandedDef = detail;
      });
    }
  }

  goToPage(p: number) {
    this.page = p;
    this.expandedKey = null;
    this.expandedDef = null;
    this.loadDefinitions();
  }
}
