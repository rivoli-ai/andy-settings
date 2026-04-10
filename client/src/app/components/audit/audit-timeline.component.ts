import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-audit-timeline',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="audit-timeline">
      <h1>Audit Timeline</h1>

      <div class="toolbar">
        <input
          type="text"
          class="filter-input"
          placeholder="Filter by definition key..."
          [(ngModel)]="keyFilter"
        />
        <button class="btn" (click)="applyFilter()">Filter</button>
        <button class="btn btn-outline" *ngIf="keyFilter" (click)="clearFilter()">Clear</button>
      </div>

      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Event Type</th>
              <th>Definition Key</th>
              <th>Scope Type</th>
              <th>Actor</th>
              <th>Created At</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let evt of events">
              <td>
                <span class="event-badge" [ngClass]="getEventClass(evt.eventType)">
                  {{ evt.eventType }}
                </span>
              </td>
              <td class="key-cell">{{ evt.definitionKey }}</td>
              <td>{{ evt.scopeType || '-' }}</td>
              <td>{{ evt.actorId || '-' }}</td>
              <td class="date-cell">{{ formatDate(evt.createdAt) }}</td>
            </tr>
            <tr *ngIf="events.length === 0">
              <td colspan="5" class="empty-cell">No audit events found.</td>
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
    .audit-timeline { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    .toolbar {
      display: flex;
      gap: 10px;
      margin-bottom: 16px;
    }
    .filter-input {
      flex: 1;
      max-width: 400px;
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .filter-input:focus { outline: none; border-color: #6c63ff; }
    .table-container { overflow-x: auto; }
    table {
      width: 100%;
      border-collapse: collapse;
      background: #fff;
      border: 1px solid #e0e0e0;
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
    tbody td {
      padding: 10px 14px;
      border-bottom: 1px solid #f0f0f0;
      font-size: 14px;
      color: #333;
    }
    tbody tr:hover { background: #f5f5ff; }
    .key-cell { font-family: monospace; color: #6c63ff; font-weight: 600; }
    .date-cell { font-size: 13px; color: #666; white-space: nowrap; }
    .empty-cell { text-align: center; color: #888; font-style: italic; padding: 24px 14px; }
    .event-badge {
      display: inline-block;
      padding: 3px 10px;
      border-radius: 10px;
      font-size: 12px;
      font-weight: 600;
      text-transform: uppercase;
    }
    .event-created { background: #eaffea; color: #1a8a1a; }
    .event-updated { background: #fff8e0; color: #b08800; }
    .event-deleted { background: #fff0f0; color: #d44; }
    .event-rotated { background: #e8f0ff; color: #3366cc; }
    .event-default { background: #f0f0f0; color: #666; }
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
    .btn-outline {
      background: #fff;
      color: #6c63ff;
      border: 1px solid #6c63ff;
    }
    .btn-outline:hover { background: #f0f0ff; }
  `]
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

  getEventClass(eventType: string): string {
    if (!eventType) return 'event-default';
    const lower = eventType.toLowerCase();
    if (lower.includes('created') || lower === 'created') return 'event-created';
    if (lower.includes('updated') || lower === 'updated') return 'event-updated';
    if (lower.includes('deleted') || lower === 'deleted') return 'event-deleted';
    if (lower.includes('rotated') || lower.includes('secret')) return 'event-rotated';
    return 'event-default';
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
