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
    <div class="definition-list">
      <h1>Definitions</h1>

      <div class="toolbar">
        <input
          type="text"
          class="search-input"
          placeholder="Search definitions..."
          [(ngModel)]="searchTerm"
          (ngModelChange)="onSearchChange($event)"
        />
        <select class="app-filter" [(ngModel)]="appCodeFilter" (ngModelChange)="onAppCodeChange()">
          <option value="">All Applications</option>
          <option *ngFor="let app of appCodes" [value]="app">{{ app }}</option>
        </select>
      </div>

      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Key</th>
              <th>App</th>
              <th>Display Name</th>
              <th>Data Type</th>
              <th>Category</th>
              <th>Secret</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let def of definitions"
                (click)="toggleDetail(def)"
                [class.selected]="expandedKey === def.key">
              <td class="key-cell">{{ def.key }}</td>
              <td>{{ def.applicationCode }}</td>
              <td>{{ def.displayName }}</td>
              <td><span class="badge">{{ def.dataType }}</span></td>
              <td>{{ def.category }}</td>
              <td>
                <span class="secret-badge" [class.is-secret]="def.isSecret">
                  {{ def.isSecret ? 'Yes' : 'No' }}
                </span>
              </td>
            </tr>
            <tr *ngIf="expandedKey" class="detail-row">
              <td colspan="6">
                <div class="detail-panel" *ngIf="expandedDef">
                  <h3>{{ expandedDef.key }}</h3>
                  <div class="detail-grid">
                    <div class="detail-item">
                      <label>Description</label>
                      <span>{{ expandedDef.description || '(none)' }}</span>
                    </div>
                    <div class="detail-item">
                      <label>Default Value</label>
                      <span>{{ expandedDef.defaultValue ?? '(none)' }}</span>
                    </div>
                    <div class="detail-item">
                      <label>Validation Regex</label>
                      <span>{{ expandedDef.validationRegex || '(none)' }}</span>
                    </div>
                    <div class="detail-item">
                      <label>Allowed Values</label>
                      <span>{{ expandedDef.allowedValues?.join(', ') || '(none)' }}</span>
                    </div>
                    <div class="detail-item">
                      <label>Created At</label>
                      <span>{{ expandedDef.createdAt }}</span>
                    </div>
                    <div class="detail-item">
                      <label>Updated At</label>
                      <span>{{ expandedDef.updatedAt }}</span>
                    </div>
                  </div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="pagination">
        <button class="btn" [disabled]="page <= 1" (click)="goToPage(page - 1)">Previous</button>
        <span class="page-info">Page {{ page }} of {{ totalPages }} ({{ totalCount }} total)</span>
        <button class="btn" [disabled]="page >= totalPages" (click)="goToPage(page + 1)">Next</button>
      </div>
    </div>
  `,
  styles: [`
    .definition-list { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    .toolbar {
      display: flex;
      gap: 12px;
      margin-bottom: 16px;
    }
    .search-input {
      flex: 1;
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .search-input:focus { outline: none; border-color: #6c63ff; }
    .app-filter {
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
      min-width: 180px;
    }
    .app-filter:focus { outline: none; border-color: #6c63ff; }
    .table-container { overflow-x: auto; }
    table {
      width: 100%;
      border-collapse: collapse;
      background: #fff;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
    }
    thead th {
      text-align: left;
      padding: 10px 14px;
      background: #f8f8fc;
      border-bottom: 2px solid #e0e0e0;
      font-size: 13px;
      color: #555;
      font-weight: 600;
    }
    tbody tr {
      cursor: pointer;
      transition: background 0.1s;
    }
    tbody tr:hover { background: #f5f5ff; }
    tbody tr.selected { background: #eeeeff; }
    tbody td {
      padding: 10px 14px;
      border-bottom: 1px solid #f0f0f0;
      font-size: 14px;
      color: #333;
    }
    .key-cell { font-family: monospace; font-weight: 600; color: #6c63ff; }
    .badge {
      display: inline-block;
      padding: 2px 8px;
      background: #eeeeff;
      color: #6c63ff;
      border-radius: 10px;
      font-size: 12px;
    }
    .secret-badge {
      display: inline-block;
      padding: 2px 8px;
      background: #f0f0f0;
      border-radius: 10px;
      font-size: 12px;
      color: #888;
    }
    .secret-badge.is-secret { background: #fff0f0; color: #d44; }
    .detail-row td { padding: 0; background: #fafafe; }
    .detail-panel {
      padding: 16px 20px;
      border-top: 2px solid #6c63ff;
    }
    .detail-panel h3 {
      margin: 0 0 12px;
      font-size: 16px;
      color: #333;
    }
    .detail-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
      gap: 12px;
    }
    .detail-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .detail-item label {
      font-size: 12px;
      color: #888;
      font-weight: 600;
      text-transform: uppercase;
    }
    .detail-item span { font-size: 14px; color: #333; }
    .pagination {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 16px;
      margin-top: 16px;
    }
    .page-info { font-size: 14px; color: #666; }
    .btn {
      padding: 8px 16px;
      background: #6c63ff;
      color: #fff;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 14px;
    }
    .btn:hover { background: #5a52e0; }
    .btn:disabled { background: #ccc; cursor: not-allowed; }
  `]
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
