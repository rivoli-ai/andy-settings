import { Component, OnInit } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <div class="dashboard">
      <h1>Dashboard</h1>
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-value">{{ definitionCount }}</div>
          <div class="stat-label">Definitions</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ categoryCount }}</div>
          <div class="stat-label">Categories</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ secretCount }}</div>
          <div class="stat-label">Secrets</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ auditCount }}</div>
          <div class="stat-label">Audit Events</div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard { padding: 24px; }
    h1 { margin: 0 0 24px; font-size: 24px; color: #333; }
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
    }
    .stat-card {
      background: #fff;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      padding: 24px;
      text-align: center;
    }
    .stat-value { font-size: 36px; font-weight: 700; color: #6c63ff; }
    .stat-label { font-size: 14px; color: #888; margin-top: 4px; }
  `]
})
export class DashboardComponent implements OnInit {
  definitionCount = 0;
  categoryCount = 0;
  secretCount = 0;
  auditCount = 0;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getDefinitions({ pageSize: 1 }).subscribe((data: any) => {
      this.definitionCount = data.totalCount;
    });
    this.api.getDefinitions({ pageSize: 100 }).subscribe((data: any) => {
      const categories = new Set(data.items?.map((d: any) => d.category).filter(Boolean));
      this.categoryCount = categories.size;
      this.secretCount = data.items?.filter((d: any) => d.isSecret).length ?? 0;
    });
    this.api.getAuditEvents({ pageSize: 1 }).subscribe((data: any) => {
      this.auditCount = data.totalCount;
    });
  }
}
