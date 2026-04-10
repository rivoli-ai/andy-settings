import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-audit-timeline',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <h1 class="page-header">Audit Timeline</h1>

      <div class="flex gap-3 mb-4">
        <input
          type="text"
          class="form-input max-w-[400px]"
          placeholder="Filter by definition key..."
          [(ngModel)]="keyFilter"
        />
        <button class="btn-primary" (click)="applyFilter()">Filter</button>
        <button class="btn-secondary" *ngIf="keyFilter" (click)="clearFilter()">Clear</button>
      </div>

      <div class="table-wrapper">
        <table class="min-w-full divide-y divide-surface-200 bg-white border border-surface-200 rounded-lg">
          <thead class="bg-surface-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Event Type</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Definition Key</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Scope Type</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Actor</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Created At</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-surface-200">
            <tr *ngFor="let evt of events" class="hover:bg-surface-50 transition-colors">
              <td class="px-4 py-3 text-sm">
                <span [class]="getEventBadgeClass(evt.eventType)">
                  {{ evt.eventType }}
                </span>
              </td>
              <td class="px-4 py-3 text-sm font-mono font-semibold text-primary-500">{{ evt.definitionKey }}</td>
              <td class="px-4 py-3 text-sm">{{ evt.scopeType || '-' }}</td>
              <td class="px-4 py-3 text-sm">{{ evt.actorId || '-' }}</td>
              <td class="px-4 py-3 text-sm text-surface-500 whitespace-nowrap">{{ formatDate(evt.createdAt) }}</td>
            </tr>
            <tr *ngIf="events.length === 0">
              <td colspan="5" class="px-4 py-6 text-center text-sm text-surface-400 italic">No audit events found.</td>
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
export class AuditTimelineComponent implements OnInit {
  events: any[] = [];
  keyFilter = '';
  page = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 1;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadEvents();
  }

  loadEvents() {
    this.api.getAuditEvents({
      definitionKey: this.keyFilter || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe((data: any) => {
      this.events = data.items || [];
      this.totalCount = data.totalCount || 0;
      this.totalPages = Math.max(1, Math.ceil(this.totalCount / this.pageSize));
    });
  }

  applyFilter() {
    this.page = 1;
    this.loadEvents();
  }

  clearFilter() {
    this.keyFilter = '';
    this.page = 1;
    this.loadEvents();
  }

  goToPage(p: number) {
    this.page = p;
    this.loadEvents();
  }

  getEventBadgeClass(eventType: string): string {
    if (!eventType) return 'badge badge-default';
    const lower = eventType.toLowerCase();
    if (lower.includes('created') || lower === 'created') return 'badge badge-success';
    if (lower.includes('updated') || lower === 'updated') return 'badge badge-warning';
    if (lower.includes('deleted') || lower === 'deleted') return 'badge badge-danger';
    if (lower.includes('rotated') || lower.includes('secret')) return 'badge badge-info';
    return 'badge badge-default';
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '-';
    try {
      const d = new Date(dateStr);
      return d.toLocaleString();
    } catch {
      return dateStr;
    }
  }
}
