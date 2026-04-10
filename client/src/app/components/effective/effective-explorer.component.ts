import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-effective-explorer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="effective-explorer">
      <h1>Effective Value Explorer</h1>

      <div class="form-card">
        <div class="form-grid">
          <div class="form-group full-width">
            <label>Key</label>
            <input type="text" [(ngModel)]="key" placeholder="e.g. my-app:feature-flag" />
          </div>
          <div class="form-group">
            <label>User ID</label>
            <input type="text" [(ngModel)]="context.userId" placeholder="user-123" />
          </div>
          <div class="form-group">
            <label>Team ID</label>
            <input type="text" [(ngModel)]="context.teamId" placeholder="team-abc" />
          </div>
          <div class="form-group">
            <label>Workspace ID</label>
            <input type="text" [(ngModel)]="context.workspaceId" placeholder="ws-456" />
          </div>
          <div class="form-group">
            <label>Application Code</label>
            <input type="text" [(ngModel)]="context.applicationCode" placeholder="my-app" />
          </div>
          <div class="form-group">
            <label>Service Code</label>
            <input type="text" [(ngModel)]="context.serviceCode" placeholder="svc-001" />
          </div>
        </div>
        <div class="form-actions">
          <button class="btn" (click)="resolve()" [disabled]="!key">Resolve</button>
          <button class="btn btn-outline" (click)="explain()" [disabled]="!key">Explain</button>
        </div>
      </div>

      <div class="result-card" *ngIf="result">
        <h2>Result</h2>
        <div class="result-grid">
          <div class="result-item">
            <label>Key</label>
            <span class="mono">{{ result.key }}</span>
          </div>
          <div class="result-item">
            <label>Effective Value</label>
            <span class="value-display">{{ result.effectiveValue ?? '(null)' }}</span>
          </div>
          <div class="result-item">
            <label>Winning Scope</label>
            <span class="scope-badge">{{ result.winningScopeType || 'Default' }}</span>
          </div>
          <div class="result-item">
            <label>Is Default</label>
            <span [class.default-yes]="result.isDefault" [class.default-no]="!result.isDefault">
              {{ result.isDefault ? 'Yes' : 'No' }}
            </span>
          </div>
        </div>

        <div class="source-chain" *ngIf="result.sourceChain && result.sourceChain.length > 0">
          <h3>Source Chain</h3>
          <table>
            <thead>
              <tr>
                <th></th>
                <th>Scope Type</th>
                <th>Scope ID</th>
                <th>Value</th>
                <th>Priority</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let link of result.sourceChain" [class.winner]="link.isWinner">
                <td>
                  <span class="winner-icon" *ngIf="link.isWinner">&#9733;</span>
                </td>
                <td><span class="scope-badge">{{ link.scopeType }}</span></td>
                <td>{{ link.scopeId || '(global)' }}</td>
                <td class="mono">{{ link.value ?? '(not set)' }}</td>
                <td>{{ link.priority }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="error-card" *ngIf="errorMsg">
        <p>{{ errorMsg }}</p>
      </div>
    </div>
  `,
  styles: [`
    .effective-explorer { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    h2 { margin: 0 0 16px; font-size: 18px; color: #333; }
    h3 { margin: 16px 0 12px; font-size: 16px; color: #555; }
    .form-card {
      background: #f8f8fc;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      padding: 20px;
      margin-bottom: 24px;
    }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 16px;
      margin-bottom: 16px;
    }
    .full-width { grid-column: 1 / -1; }
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
    .form-group input {
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .form-group input:focus { outline: none; border-color: #6c63ff; }
    .form-actions {
      display: flex;
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
    .btn-outline {
      background: #fff;
      color: #6c63ff;
      border: 1px solid #6c63ff;
    }
    .btn-outline:hover { background: #f0f0ff; }
    .btn-outline:disabled { background: #f5f5f5; color: #ccc; border-color: #ccc; }
    .result-card {
      background: #fff;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      padding: 20px;
    }
    .result-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 8px;
    }
    .result-item {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .result-item label {
      font-size: 12px;
      font-weight: 600;
      color: #888;
      text-transform: uppercase;
    }
    .result-item span { font-size: 15px; color: #333; }
    .mono { font-family: monospace; }
    .value-display {
      font-family: monospace;
      font-size: 16px !important;
      font-weight: 700;
      color: #6c63ff !important;
    }
    .scope-badge {
      display: inline-block;
      padding: 2px 8px;
      background: #eeeeff;
      color: #6c63ff;
      border-radius: 10px;
      font-size: 12px;
    }
    .default-yes { color: #2a9d2a; font-weight: 600; }
    .default-no { color: #333; }
    .source-chain { margin-top: 16px; }
    table {
      width: 100%;
      border-collapse: collapse;
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
    tbody tr.winner {
      background: #eaffea;
      font-weight: 600;
    }
    tbody tr.winner:hover { background: #ddf5dd; }
    .winner-icon { color: #f5a623; font-size: 16px; }
    .error-card {
      background: #fff5f5;
      border: 1px solid #ffcccc;
      border-radius: 6px;
      padding: 16px;
      color: #d44;
    }
  `]
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
