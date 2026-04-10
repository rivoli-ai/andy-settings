import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-value-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <h1 class="page-header">Value Editor</h1>

      <div class="mb-6 relative">
        <label class="form-label">Definition Key</label>
        <div class="flex gap-2">
          <input
            type="text"
            class="form-input"
            placeholder="Search or select a definition key..."
            [(ngModel)]="keySearch"
            (ngModelChange)="onKeySearchChange()"
            (focus)="showSuggestions = true"
          />
        </div>
        <ul *ngIf="showSuggestions && filteredKeys.length > 0"
            class="absolute z-10 left-0 right-0 max-h-[200px] overflow-y-auto bg-white border border-surface-200 rounded-b shadow-lg list-none m-0 p-0">
          <li *ngFor="let k of filteredKeys"
              (click)="selectKey(k)"
              class="px-3 py-2 text-sm font-mono cursor-pointer hover:bg-surface-50 hover:text-primary-500">
            {{ k }}
          </li>
        </ul>
      </div>

      <div *ngIf="selectedKey" class="mt-2">
        <h2 class="text-lg font-semibold text-surface-900 mb-4">
          Assignments for <span class="text-primary-500 font-mono">{{ selectedKey }}</span>
        </h2>

        <div class="table-wrapper mb-6" *ngIf="assignments.length > 0">
          <table class="min-w-full divide-y divide-surface-200 bg-white border border-surface-200 rounded-lg">
            <thead class="bg-surface-50">
              <tr>
                <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Scope Type</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Scope ID</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Value</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Version</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-surface-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-surface-200">
              <tr *ngFor="let a of assignments" class="hover:bg-surface-50">
                <td class="px-4 py-3 text-sm"><span class="badge badge-info">{{ a.scopeType }}</span></td>
                <td class="px-4 py-3 text-sm">{{ a.scopeId || '(global)' }}</td>
                <td class="px-4 py-3 text-sm font-mono">{{ a.value }}</td>
                <td class="px-4 py-3 text-sm">{{ a.version }}</td>
                <td class="px-4 py-3 text-sm">
                  <button class="btn-danger btn-sm" (click)="deleteAssignment(a.id)">Delete</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p class="text-sm text-surface-400 italic" *ngIf="assignments.length === 0">No assignments found for this key.</p>

        <div class="card mt-6">
          <div class="card-body">
            <h3 class="text-base font-semibold text-surface-700 mb-3">{{ editMode ? 'Edit' : 'Add' }} Assignment</h3>
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
                <input type="text" class="form-input" [(ngModel)]="form.scopeId" placeholder="e.g. user-123" />
              </div>
              <div>
                <label class="form-label">Value</label>
                <input type="text" class="form-input" [(ngModel)]="form.value" placeholder="Setting value" />
              </div>
            </div>
            <div class="flex items-center gap-3">
              <button class="btn-primary" (click)="saveAssignment()" [disabled]="!form.scopeType || !form.value">
                Save Assignment
              </button>
              <span class="text-sm text-green-600" *ngIf="successMsg">{{ successMsg }}</span>
              <span class="text-sm text-red-500" *ngIf="errorMsg">{{ errorMsg }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
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
