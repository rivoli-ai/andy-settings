import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-value-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="value-editor">
      <h1>Value Editor</h1>

      <div class="key-selector">
        <label>Definition Key</label>
        <div class="key-input-row">
          <input
            type="text"
            class="key-input"
            placeholder="Search or select a definition key..."
            [(ngModel)]="keySearch"
            (ngModelChange)="onKeySearchChange()"
            (focus)="showSuggestions = true"
          />
        </div>
        <ul class="suggestions" *ngIf="showSuggestions && filteredKeys.length > 0">
          <li *ngFor="let k of filteredKeys" (click)="selectKey(k)">{{ k }}</li>
        </ul>
      </div>

      <div *ngIf="selectedKey" class="assignments-section">
        <h2>Assignments for <span class="key-highlight">{{ selectedKey }}</span></h2>

        <div class="table-container" *ngIf="assignments.length > 0">
          <table>
            <thead>
              <tr>
                <th>Scope Type</th>
                <th>Scope ID</th>
                <th>Value</th>
                <th>Version</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let a of assignments">
                <td><span class="scope-badge">{{ a.scopeType }}</span></td>
                <td>{{ a.scopeId || '(global)' }}</td>
                <td class="value-cell">{{ a.value }}</td>
                <td>{{ a.version }}</td>
                <td>
                  <button class="btn-delete" (click)="deleteAssignment(a.id)">Delete</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p class="empty-msg" *ngIf="assignments.length === 0">No assignments found for this key.</p>

        <div class="form-section">
          <h3>{{ editMode ? 'Edit' : 'Add' }} Assignment</h3>
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
              <input type="text" [(ngModel)]="form.scopeId" placeholder="e.g. user-123" />
            </div>
            <div class="form-group">
              <label>Value</label>
              <input type="text" [(ngModel)]="form.value" placeholder="Setting value" />
            </div>
          </div>
          <div class="form-actions">
            <button class="btn" (click)="saveAssignment()" [disabled]="!form.scopeType || !form.value">
              Save Assignment
            </button>
            <span class="status-msg success" *ngIf="successMsg">{{ successMsg }}</span>
            <span class="status-msg error" *ngIf="errorMsg">{{ errorMsg }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .value-editor { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    h2 { margin: 0 0 16px; font-size: 18px; color: #333; }
    h2 .key-highlight { color: #6c63ff; font-family: monospace; }
    h3 { margin: 0 0 12px; font-size: 16px; color: #555; }
    .key-selector { margin-bottom: 24px; position: relative; }
    .key-selector > label {
      display: block;
      margin-bottom: 6px;
      font-size: 13px;
      font-weight: 600;
      color: #555;
    }
    .key-input-row { display: flex; gap: 8px; }
    .key-input {
      flex: 1;
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .key-input:focus { outline: none; border-color: #6c63ff; }
    .suggestions {
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      max-height: 200px;
      overflow-y: auto;
      background: #fff;
      border: 1px solid #ddd;
      border-radius: 0 0 4px 4px;
      list-style: none;
      margin: 0;
      padding: 0;
      z-index: 10;
      box-shadow: 0 4px 12px rgba(0,0,0,0.1);
    }
    .suggestions li {
      padding: 8px 12px;
      font-size: 14px;
      cursor: pointer;
      font-family: monospace;
    }
    .suggestions li:hover { background: #f5f5ff; color: #6c63ff; }
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
    .scope-badge {
      display: inline-block;
      padding: 2px 8px;
      background: #eeeeff;
      color: #6c63ff;
      border-radius: 10px;
      font-size: 12px;
    }
    .value-cell { font-family: monospace; }
    .btn-delete {
      padding: 4px 10px;
      background: #fff;
      color: #d44;
      border: 1px solid #d44;
      border-radius: 4px;
      cursor: pointer;
      font-size: 12px;
    }
    .btn-delete:hover { background: #fff0f0; }
    .empty-msg { color: #888; font-size: 14px; font-style: italic; }
    .form-section {
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
      gap: 12px;
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
    .status-msg { font-size: 13px; }
    .status-msg.success { color: #2a9d2a; }
    .status-msg.error { color: #d44; }
    .assignments-section { margin-top: 8px; }
  `]
})
export class ValueEditorComponent implements OnInit {
  allKeys: string[] = [];
  filteredKeys: string[] = [];
  keySearch = '';
  selectedKey = '';
  showSuggestions = false;
  assignments: any[] = [];
  editMode = false;
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
    this.loadKeys();
  }

  loadKeys() {
    this.api.getDefinitions({ pageSize: 500 }).subscribe((data: any) => {
      this.allKeys = (data.items || []).map((d: any) => d.key).sort();
      this.filteredKeys = this.allKeys;
    });
  }

  onKeySearchChange() {
    this.showSuggestions = true;
    const term = this.keySearch.toLowerCase();
    this.filteredKeys = this.allKeys.filter(k => k.toLowerCase().includes(term));
  }

  selectKey(key: string) {
    this.selectedKey = key;
    this.keySearch = key;
    this.showSuggestions = false;
    this.clearMessages();
    this.loadAssignments();
  }

  loadAssignments() {
    this.api.getValues({ definitionKey: this.selectedKey }).subscribe((data: any) => {
      this.assignments = data.items || data || [];
    });
  }

  saveAssignment() {
    this.clearMessages();
    const dto = {
      definitionKey: this.selectedKey,
      scopeType: this.form.scopeType,
      scopeId: this.form.scopeId || null,
      value: this.form.value
    };
    this.api.setValue(dto).subscribe({
      next: () => {
        this.successMsg = 'Assignment saved successfully.';
        this.form = { scopeType: '', scopeId: '', value: '' };
        this.loadAssignments();
      },
      error: (err: any) => {
        this.errorMsg = err.error?.message || 'Failed to save assignment.';
      }
    });
  }

  deleteAssignment(id: string) {
    this.clearMessages();
    this.api.deleteValue(id).subscribe({
      next: () => {
        this.successMsg = 'Assignment deleted.';
        this.loadAssignments();
      },
      error: (err: any) => {
        this.errorMsg = err.error?.message || 'Failed to delete assignment.';
      }
    });
  }

  private clearMessages() {
    this.successMsg = '';
    this.errorMsg = '';
  }
}
