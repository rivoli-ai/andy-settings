import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-effective-explorer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <h1 class="page-header">Effective Value Explorer</h1>

      <div class="card mb-6">
        <div class="card-body">
          <div class="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            <div class="col-span-2 lg:col-span-3">
              <label class="form-label">Key</label>
              <input type="text" class="form-input" [(ngModel)]="key" placeholder="e.g. my-app:feature-flag" />
            </div>
            <div>
              <label class="form-label">User ID</label>
              <input type="text" class="form-input" [(ngModel)]="context.userId" placeholder="user-123" />
            </div>
            <div>
              <label class="form-label">Team ID</label>
              <input type="text" class="form-input" [(ngModel)]="context.teamId" placeholder="team-abc" />
            </div>
            <div>
              <label class="form-label">Workspace ID</label>
              <input type="text" class="form-input" [(ngModel)]="context.workspaceId" placeholder="ws-456" />
            </div>
            <div>
              <label class="form-label">Application Code</label>
              <input type="text" class="form-input" [(ngModel)]="context.applicationCode" placeholder="my-app" />
            </div>
            <div>
              <label class="form-label">Service Code</label>
              <input type="text" class="form-input" [(ngModel)]="context.serviceCode" placeholder="svc-001" />
            </div>
          </div>
          <div class="flex gap-3">
            <button class="btn-primary" (click)="resolve()" [disabled]="!key">Resolve</button>
            <button class="btn-secondary" (click)="explain()" [disabled]="!key">Explain</button>
          </div>
        </div>
      </div>

      <div class="card" *ngIf="result">
        <div class="card-body">
          <h2 class="text-lg font-semibold text-surface-900 mb-4">Result</h2>
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
            <div class="flex flex-col gap-0.5">
              <span class="text-xs font-semibold text-surface-400 uppercase">Key</span>
              <span class="text-sm font-mono text-surface-900">{{ result.key }}</span>
            </div>
            <div class="flex flex-col gap-0.5">
              <span class="text-xs font-semibold text-surface-400 uppercase">Effective Value</span>
              <span class="text-base font-bold font-mono text-primary-500">{{ result.effectiveValue ?? '(null)' }}</span>
            </div>
            <div class="flex flex-col gap-0.5">
              <span class="text-xs font-semibold text-surface-400 uppercase">Winning Scope</span>
              <span class="badge badge-info">{{ result.winningScopeType || 'Default' }}</span>
            </div>
            <div class="flex flex-col gap-0.5">
              <span class="text-xs font-semibold text-surface-400 uppercase">Is Default</span>
              <span [class]="result.isDefault ? 'text-sm font-semibold text-green-600' : 'text-sm text-surface-900'">
                {{ result.isDefault ? 'Yes' : 'No' }}
              </span>
            </div>
          </div>

          <div *ngIf="result.sourceChain && result.sourceChain.length > 0" class="mt-4">
            <h3 class="text-base font-semibold text-surface-700 mb-3">Source Chain</h3>
            <div class="table-wrapper">
              <table class="min-w-full divide-y divide-surface-200 border border-surface-200 rounded-lg">
                <thead class="bg-surface-50">
                  <tr>
                    <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider w-10"></th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Scope Type</th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Scope ID</th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Value</th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Priority</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-surface-200">
                  <tr *ngFor="let link of result.sourceChain"
                      [class]="link.isWinner ? 'bg-green-50 font-semibold' : 'hover:bg-surface-50'">
                    <td class="px-4 py-3 text-sm">
                      <span *ngIf="link.isWinner" class="text-yellow-500 text-base">&#9733;</span>
                    </td>
                    <td class="px-4 py-3 text-sm"><span class="badge badge-info">{{ link.scopeType }}</span></td>
                    <td class="px-4 py-3 text-sm">{{ link.scopeId || '(global)' }}</td>
                    <td class="px-4 py-3 text-sm font-mono">{{ link.value ?? '(not set)' }}</td>
                    <td class="px-4 py-3 text-sm">{{ link.priority }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <div *ngIf="errorMsg" class="mt-4 p-4 bg-red-50 border border-red-200 rounded-lg text-red-600 text-sm">
        <p>{{ errorMsg }}</p>
      </div>
    </div>
  `
})
export class EffectiveExplorerComponent {
  key = '';
  context = {
    userId: '',
    teamId: '',
    workspaceId: '',
    applicationCode: '',
    serviceCode: ''
  };
  result: any = null;
  errorMsg = '';

  constructor(private api: ApiService) {}

  resolve() {
    this.errorMsg = '';
    this.result = null;
    const ctx = this.buildContext();
    this.api.resolve(this.key, ctx).subscribe({
      next: (data: any) => this.result = data,
      error: (err: any) => this.errorMsg = err.error?.message || 'Failed to resolve.'
    });
  }

  explain() {
    this.errorMsg = '';
    this.result = null;
    const ctx = this.buildContext();
    this.api.explain(this.key, ctx).subscribe({
      next: (data: any) => this.result = data,
      error: (err: any) => this.errorMsg = err.error?.message || 'Failed to explain.'
    });
  }

  private buildContext(): any {
    const ctx: any = {};
    if (this.context.userId) ctx.userId = this.context.userId;
    if (this.context.teamId) ctx.teamId = this.context.teamId;
    if (this.context.workspaceId) ctx.workspaceId = this.context.workspaceId;
    if (this.context.applicationCode) ctx.applicationCode = this.context.applicationCode;
    if (this.context.serviceCode) ctx.serviceCode = this.context.serviceCode;
    return ctx;
  }
}
