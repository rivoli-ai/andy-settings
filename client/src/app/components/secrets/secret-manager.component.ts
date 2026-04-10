import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-secret-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="secret-manager">
      <h1>Secret Manager</h1>

      <div class="table-container" *ngIf="secrets.length > 0">
        <table>
          <thead>
            <tr>
              <th>Key</th>
              <th>App</th>
              <th>Category</th>
              <th>Has Value</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let s of secrets" [class.selected]="selectedKey === s.key">
              <td class="key-cell">{{ s.key }}</td>
              <td>{{ s.applicationCode }}</td>
              <td>{{ s.category }}</td>
              <td>
                <span class="has-value-badge" [class.has-value]="s.hasValue">
                  {{ s.hasValue ? 'Set' : 'Not set' }}
                </span>
              </td>
              <td>
                <button class="btn-sm" (click)="openSetForm(s)">Set Secret</button>
                <button class="btn-sm btn-rotate" (click)="rotateSecret(s)" title="Rotate secret">
                  Rotate
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <p class="empty-msg" *ngIf="secrets.length === 0 && !loading">
        No secret definitions found.
      </p>
      <p class="empty-msg" *ngIf="loading">Loading...</p>

      <div class="form-card" *ngIf="selectedKey">
        <h2>Set Secret: <span class="key-highlight">{{ selectedKey }}</span></h2>
        <div class="form-grid">
          <div class="form-group">
            <label>Scope Type</label>
            <select [(ngModel)]="form.scopeType">
              <option value="">-- Select --</option>
              <option *ngFor="let st of scopeTypes" [value]="st">{{ st }}</option>
            </select>
          </div>
          <div class="form-group">
            <label>Scope ID</label>
            <input type="text" [(ngModel)]="form.scopeId" placeholder="Optional scope ID" />
          </div>
          <div class="form-group">
            <label>Secret Value</label>
            <input type="password" [(ngModel)]="form.value" placeholder="Enter secret value" />
          </div>
        </div>
        <div class="form-actions">
          <button class="btn" (click)="saveSecret()" [disabled]="!form.value">Save Secret</button>
          <button class="btn btn-cancel" (click)="cancelForm()">Cancel</button>
          <span class="status-msg success" *ngIf="successMsg">{{ successMsg }}</span>
          <span class="status-msg error" *ngIf="errorMsg">{{ errorMsg }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .secret-manager { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    h2 { margin: 0 0 16px; font-size: 18px; color: #333; }
    h2 .key-highlight { color: #6c63ff; font-family: monospace; }
    .table-container { overflow-x: auto; margin-bottom: 24px; }
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
    tbody tr.selected { background: #eeeeff; }
    .key-cell { font-family: monospace; font-weight: 600; color: #6c63ff; }
    .has-value-badge {
      display: inline-block;
      padding: 2px 8px;
      background: #fff0f0;
      color: #d44;
      border-radius: 10px;
      font-size: 12px;
    }
    .has-value-badge.has-value { background: #eaffea; color: #2a9d2a; }
    .btn-sm {
      padding: 4px 10px;
      background: #6c63ff;
      color: #fff;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 12px;
      margin-right: 6px;
    }
    .btn-sm:hover { background: #5a52e0; }
    .btn-rotate {
      background: #fff;
      color: #6c63ff;
      border: 1px solid #6c63ff;
    }
    .btn-rotate:hover { background: #f0f0ff; }
    .empty-msg { color: #888; font-size: 14px; font-style: italic; }
    .form-card {
      background: #f8f8fc;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      padding: 20px;
    }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 16px;
      margin-bottom: 16px;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .form-group label {
      font-size: 12px;
      font-weight: 600;
      color: #555;
      text-transform: uppercase;
    }
    .form-group input,
    .form-group select {
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .form-group input:focus,
    .form-group select:focus { outline: none; border-color: #6c63ff; }
    .form-actions {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .btn {
      padding: 8px 20px;
      background: #6c63ff;
      color: #fff;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 14px;
    }
    .btn:hover { background: #5a52e0; }
    .btn:disabled { background: #ccc; cursor: not-allowed; }
    .btn-cancel {
      background: #fff;
      color: #888;
      border: 1px solid #ddd;
    }
    .btn-cancel:hover { background: #f5f5f5; }
    .status-msg { font-size: 13px; }
    .status-msg.success { color: #2a9d2a; }
    .status-msg.error { color: #d44; }
  `]
})
export class SecretManagerComponent implements OnInit {
  secrets: any[] = [];
  loading = true;
  selectedKey = '';
  successMsg = '';
  errorMsg = '';

  scopeTypes = ['Machine', 'Application', 'Service', 'User', 'Team', 'Workspace'];

  form = {
    scopeType: '',
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

      // For each secret, check if a value exists
      this.secrets = secretDefs.map((d: any) => ({
        ...d,
        hasValue: false
      }));

      // Check assignments for each secret key
      this.secrets.forEach((s, idx) => {
        this.api.getValues({ definitionKey: s.key }).subscribe((valData: any) => {
          const items = valData.items || valData || [];
          this.secrets[idx].hasValue = items.length > 0;
        });
      });

      this.loading = false;
    });
  }

  openSetForm(secret: any) {
    this.selectedKey = secret.key;
    this.form = { scopeType: '', scopeId: '', value: '' };
    this.clearMessages();
  }

  cancelForm() {
    this.selectedKey = '';
    this.clearMessages();
  }

  saveSecret() {
    this.clearMessages();
    const dto = {
      definitionKey: this.selectedKey,
      scopeType: this.form.scopeType || null,
      scopeId: this.form.scopeId || null,
      value: this.form.value
    };
    this.api.setValue(dto).subscribe({
      next: () => {
        this.successMsg = 'Secret saved successfully.';
        this.form = { scopeType: '', scopeId: '', value: '' };
        this.loadSecrets();
      },
      error: (err: any) => {
        this.errorMsg = err.error?.message || 'Failed to save secret.';
      }
    });
  }

  rotateSecret(secret: any) {
    // Placeholder: rotation would involve a dedicated API endpoint
    alert(`Secret rotation for "${secret.key}" is not yet implemented on the server.`);
  }

  private clearMessages() {
    this.successMsg = '';
    this.errorMsg = '';
  }
}
