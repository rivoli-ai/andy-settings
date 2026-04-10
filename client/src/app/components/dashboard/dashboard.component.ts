import { Component, OnInit } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <div class="p-6">
      <h1 class="page-header">Dashboard</h1>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="card">
          <div class="card-body text-center">
            <div class="text-3xl font-bold text-primary-500">{{ definitionCount }}</div>
            <div class="text-sm text-surface-500 mt-1">Definitions</div>
          </div>
        </div>
        <div class="card">
          <div class="card-body text-center">
            <div class="text-3xl font-bold text-primary-500">{{ categoryCount }}</div>
            <div class="text-sm text-surface-500 mt-1">Categories</div>
          </div>
        </div>
        <div class="card">
          <div class="card-body text-center">
            <div class="text-3xl font-bold text-primary-500">{{ secretCount }}</div>
            <div class="text-sm text-surface-500 mt-1">Secrets</div>
          </div>
        </div>
        <div class="card">
          <div class="card-body text-center">
            <div class="text-3xl font-bold text-primary-500">{{ auditCount }}</div>
            <div class="text-sm text-surface-500 mt-1">Audit Events</div>
          </div>
        </div>
      </div>
    </div>
  `
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
