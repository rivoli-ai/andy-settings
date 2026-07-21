import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-secret-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <h1 class="page-header">Secret Manager</h1>

      <div class="table-wrapper mb-6" *ngIf="secrets.length > 0">
        <table class="min-w-full divide-y divide-surface-200 bg-white border border-surface-200 rounded-lg">
          <thead class="bg-surface-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Key</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">App</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Category</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Storage</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-surface-200">
            <tr *ngFor="let s of secrets"
                class="hover:bg-surface-50 transition-colors"
                [class.bg-primary-50]="selectedKey === s.key">
              <td class="px-4 py-3 text-sm font-mono font-semibold text-primary-500">{{ s.key }}</td>
              <td class="px-4 py-3 text-sm">{{ s.applicationCode }}</td>
              <td class="px-4 py-3 text-sm">{{ s.category }}</td>
              <td class="px-4 py-3 text-sm">
                <span class="badge badge-success">Encrypted</span>
              </td>
              <td class="px-4 py-3 text-sm">
                <div class="flex gap-2">
                  <button class="btn-primary btn-sm" (click)="openSetForm(s)">Set Secret</button>
                  <button class="btn-secondary btn-sm" (click)="rotateSecret(s)">Rotate</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <p class="text-sm text-surface-400 italic" *ngIf="secrets.length === 0 && !loading">
        No secret definitions found.
      </p>
      <p class="text-sm text-surface-400 italic" *ngIf="loading">Loading...</p>

      <div class="card" *ngIf="selectedKey">
        <div class="card-body">
          <h2 class="text-lg font-semibold text-surface-900 mb-4">
            {{ rotationMode ? 'Rotate' : 'Set' }} Secret: <span class="text-primary-500 font-mono">{{ selectedKey }}</span>
          </h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            <div>
              <label class="form-label">Scope Type</label>
              <select class="form-select" [(ngModel)]="form.scopeType">
                <option value="">-- Select --</option>
                <option *ngFor="let st of scopeTypes" [value]="st">{{ st }}</option>
              </select>
            </div>
            <div>
              <label class="form-label">Scope ID</label>
              <input type="text" class="form-input" [(ngModel)]="form.scopeId" placeholder="Optional scope ID" />
            </div>
            <div>
              <label class="form-label">Secret Value</label>
              <input type="password" class="form-input" [(ngModel)]="form.value" placeholder="Enter secret value" />
            </div>
          </div>
          <div class="flex items-center gap-3">
            <button class="btn-primary" (click)="saveSecret()" [disabled]="!form.value">Save Secret</button>
            <button class="btn-secondary" (click)="cancelForm()">Cancel</button>
            <span class="text-sm text-green-600" *ngIf="successMsg">{{ successMsg }}</span>
            <span class="text-sm text-red-500" *ngIf="errorMsg">{{ errorMsg }}</span>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SecretManagerComponent implements OnInit {
  secrets: any[] = [];
  loading = true;
  selectedKey = '';
  successMsg = '';
  errorMsg = '';
  rotationMode = false;

  scopeTypes = ['Machine', 'Application', 'Service', 'User', 'Team', 'Workspace'];

  form = {
    scopeType: 'Machine',
    scopeId: '',
    value: ''
  };

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadSecrets();
  }

  loadSecrets() {
    this.loading = true;
    this.api.getDefinitions({ pageSize: 500 }).subscribe((data: any) => {
      const secretDefs = (data.items || []).filter((d: any) => d.isSecret);

      this.secrets = secretDefs;

      this.loading = false;
    });
  }

  openSetForm(secret: any) {
    this.selectedKey = secret.key;
    this.rotationMode = false;
    this.form = { scopeType: 'Machine', scopeId: '', value: '' };
    this.clearMessages();
  }

  cancelForm() {
    this.selectedKey = '';
    this.clearMessages();
  }

  saveSecret() {
    this.clearMessages();
    const dto = {
      scopeType: this.form.scopeType || 'Machine',
      scopeId: this.form.scopeId || null,
      value: this.form.value
    };
    const request = this.rotationMode
      ? this.api.rotateSecret(this.selectedKey, {
          scopeType: dto.scopeType,
          scopeId: dto.scopeId,
          newValue: dto.value
        })
      : this.api.setSecret(this.selectedKey, dto);
    request.subscribe({
      next: () => {
        this.successMsg = 'Secret saved successfully.';
        this.form = { scopeType: 'Machine', scopeId: '', value: '' };
        this.loadSecrets();
      },
      error: (err: any) => {
        this.errorMsg = err.error?.error || err.error?.message || 'Failed to save secret.';
      }
    });
  }

  rotateSecret(secret: any) {
    this.openSetForm(secret);
    this.rotationMode = true;
  }

  private clearMessages() {
    this.successMsg = '';
    this.errorMsg = '';
  }
}
