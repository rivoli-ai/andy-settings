import { Component, OnInit } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <div class="p-8 max-w-[1400px]">
      <h1 class="page-header">Dashboard</h1>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">

        <div class="card">
          <div class="card-body flex items-center gap-4">
            <div class="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
              <svg class="w-6 h-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/>
              </svg>
            </div>
            <div>
              <div class="text-2xl font-bold text-primary-600">{{ definitionCount }}</div>
              <div class="text-sm text-surface-500">Definitions</div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-body flex items-center gap-4">
            <div class="w-12 h-12 rounded-lg bg-green-100 flex items-center justify-center">
              <svg class="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A2 2 0 013 12V7a4 4 0 014-4z"/>
              </svg>
            </div>
            <div>
              <div class="text-2xl font-bold text-green-600">{{ categoryCount }}</div>
              <div class="text-sm text-surface-500">Categories</div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-body flex items-center gap-4">
            <div class="w-12 h-12 rounded-lg bg-purple-100 flex items-center justify-center">
              <svg class="w-6 h-6 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"/>
              </svg>
            </div>
            <div>
              <div class="text-2xl font-bold text-purple-600">{{ secretCount }}</div>
              <div class="text-sm text-surface-500">Secrets</div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-body flex items-center gap-4">
            <div class="w-12 h-12 rounded-lg bg-orange-100 flex items-center justify-center">
              <svg class="w-6 h-6 text-orange-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
            </div>
            <div>
              <div class="text-2xl font-bold text-orange-600">{{ auditCount }}</div>
              <div class="text-sm text-surface-500">Audit Events</div>
            </div>
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
