import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-import-export',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <h1 class="page-header">Import / Export</h1>

      <div class="card mb-6">
        <div class="card-body">
          <h2 class="text-lg font-semibold text-surface-900 mb-4">Export Settings</h2>
          <div class="flex gap-3 items-end mb-4">
            <div class="flex-1">
              <label class="form-label">Application Code (optional)</label>
              <input
                type="text"
                class="form-input"
                [(ngModel)]="exportAppCode"
                placeholder="Filter by app code..."
              />
            </div>
            <button class="btn-primary" (click)="exportSettings()">Export</button>
          </div>

          <div *ngIf="exportResult">
            <div class="flex gap-4 mb-3">
              <span class="text-sm text-surface-500 bg-surface-100 px-2.5 py-1 rounded">Definitions: {{ exportResult.definitionCount }}</span>
              <span class="text-sm text-surface-500 bg-surface-100 px-2.5 py-1 rounded">Assignments: {{ exportResult.assignmentCount }}</span>
            </div>
            <div class="relative">
              <button class="absolute top-2 right-2 z-10 btn-primary btn-sm" (click)="copyExport()">{{ copyLabel }}</button>
              <pre class="bg-surface-900 text-green-400 p-4 rounded-lg text-xs font-mono overflow-x-auto max-h-96 overflow-y-auto m-0"><code>{{ exportJson }}</code></pre>
            </div>
          </div>

          <div *ngIf="exportError" class="mt-3 p-3 bg-red-50 border border-red-200 rounded text-red-600 text-sm">
            {{ exportError }}
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-body">
          <h2 class="text-lg font-semibold text-surface-900 mb-4">Import Settings</h2>
          <div class="mb-3">
            <label class="form-label">Paste JSON</label>
            <textarea
              class="form-input font-mono"
              [(ngModel)]="importJson"
              rows="10"
              placeholder="Paste exported JSON here..."
            ></textarea>
          </div>
          <div class="flex gap-3 mt-3">
            <button class="btn-secondary" (click)="previewImport()" [disabled]="!importJson.trim()">
              Preview
            </button>
            <button class="btn-primary" (click)="runImport()" [disabled]="!importJson.trim()">
              Import
            </button>
          </div>

          <div *ngIf="previewResult" class="mt-4 p-4 bg-surface-50 border border-surface-200 rounded-lg">
            <h3 class="text-base font-semibold text-surface-700 mb-3">Import Preview</h3>
            <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-3">
              <div class="flex flex-col gap-0.5">
                <span class="text-xs text-surface-400 uppercase font-semibold">Definitions to create</span>
                <span class="text-xl font-bold text-primary-500">{{ previewResult.definitionsToCreate ?? 0 }}</span>
              </div>
              <div class="flex flex-col gap-0.5">
                <span class="text-xs text-surface-400 uppercase font-semibold">Definitions to update</span>
                <span class="text-xl font-bold text-primary-500">{{ previewResult.definitionsToUpdate ?? 0 }}</span>
              </div>
              <div class="flex flex-col gap-0.5">
                <span class="text-xs text-surface-400 uppercase font-semibold">Assignments to create</span>
                <span class="text-xl font-bold text-primary-500">{{ previewResult.assignmentsToCreate ?? 0 }}</span>
              </div>
              <div class="flex flex-col gap-0.5">
                <span class="text-xs text-surface-400 uppercase font-semibold">Assignments to update</span>
                <span class="text-xl font-bold text-primary-500">{{ previewResult.assignmentsToUpdate ?? 0 }}</span>
              </div>
            </div>
            <pre *ngIf="previewJson" class="bg-surface-900 text-green-400 p-4 rounded-lg text-xs font-mono overflow-x-auto max-h-96 overflow-y-auto m-0 mt-3"><code>{{ previewJson }}</code></pre>
          </div>

          <div *ngIf="importSuccess" class="mt-3 p-3 bg-green-50 border border-green-200 rounded text-green-700 text-sm">
            {{ importSuccess }}
          </div>
          <div *ngIf="importError" class="mt-3 p-3 bg-red-50 border border-red-200 rounded text-red-600 text-sm">
            {{ importError }}
          </div>
        </div>
      </div>
    </div>
  `
})
export class ImportExportComponent {
  // Export
  exportAppCode = '';
  exportResult: any = null;
  exportJson = '';
  exportError = '';
  copyLabel = 'Copy';

  // Import
  importJson = '';
  previewResult: any = null;
  previewJson = '';
  importSuccess = '';
  importError = '';

  constructor(private api: ApiService) {}

  exportSettings() {
    this.exportError = '';
    this.exportResult = null;
    this.exportJson = '';
    this.api.exportSettings(this.exportAppCode || undefined).subscribe({
      next: (data: any) => {
        this.exportResult = data;
        this.exportJson = JSON.stringify(data.data || data, null, 2);
      },
      error: (err: any) => {
        this.exportError = err.error?.message || 'Export failed.';
      }
    });
  }

  copyExport() {
    navigator.clipboard.writeText(this.exportJson).then(() => {
      this.copyLabel = 'Copied!';
      setTimeout(() => this.copyLabel = 'Copy', 2000);
    });
  }

  previewImport() {
    this.importError = '';
    this.importSuccess = '';
    this.previewResult = null;
    this.previewJson = '';
    try {
      const parsed = JSON.parse(this.importJson);
      this.api.importPreview(parsed).subscribe({
        next: (data: any) => {
          this.previewResult = data;
          this.previewJson = JSON.stringify(data, null, 2);
        },
        error: (err: any) => {
          this.importError = err.error?.message || 'Preview failed.';
        }
      });
    } catch {
      this.importError = 'Invalid JSON. Please check your input.';
    }
  }

  runImport() {
    this.importError = '';
    this.importSuccess = '';
    try {
      const parsed = JSON.parse(this.importJson);
      this.api.importSettings(parsed).subscribe({
        next: () => {
          this.importSuccess = 'Import completed successfully.';
          this.previewResult = null;
          this.previewJson = '';
        },
        error: (err: any) => {
          this.importError = err.error?.message || 'Import failed.';
        }
      });
    } catch {
      this.importError = 'Invalid JSON. Please check your input.';
    }
  }
}
