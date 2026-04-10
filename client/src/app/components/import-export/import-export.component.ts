import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-import-export',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="import-export">
      <h1>Import / Export</h1>

      <div class="section-card">
        <h2>Export Settings</h2>
        <div class="export-controls">
          <div class="form-group">
            <label>Application Code (optional)</label>
            <input
              type="text"
              [(ngModel)]="exportAppCode"
              placeholder="Filter by app code..."
            />
          </div>
          <button class="btn" (click)="exportSettings()">Export</button>
        </div>

        <div class="export-result" *ngIf="exportResult">
          <div class="export-stats">
            <span class="stat">Definitions: {{ exportResult.definitionCount }}</span>
            <span class="stat">Assignments: {{ exportResult.assignmentCount }}</span>
          </div>
          <div class="code-block-wrapper">
            <button class="btn-copy" (click)="copyExport()">{{ copyLabel }}</button>
            <pre><code>{{ exportJson }}</code></pre>
          </div>
        </div>

        <div class="error-msg" *ngIf="exportError">{{ exportError }}</div>
      </div>

      <div class="section-card">
        <h2>Import Settings</h2>
        <div class="form-group">
          <label>Paste JSON</label>
          <textarea
            [(ngModel)]="importJson"
            rows="10"
            placeholder="Paste exported JSON here..."
          ></textarea>
        </div>
        <div class="import-actions">
          <button class="btn btn-outline" (click)="previewImport()" [disabled]="!importJson.trim()">
            Preview
          </button>
          <button class="btn" (click)="runImport()" [disabled]="!importJson.trim()">
            Import
          </button>
        </div>

        <div class="preview-result" *ngIf="previewResult">
          <h3>Import Preview</h3>
          <div class="preview-stats">
            <div class="preview-stat">
              <span class="label">Definitions to create</span>
              <span class="value">{{ previewResult.definitionsToCreate ?? 0 }}</span>
            </div>
            <div class="preview-stat">
              <span class="label">Definitions to update</span>
              <span class="value">{{ previewResult.definitionsToUpdate ?? 0 }}</span>
            </div>
            <div class="preview-stat">
              <span class="label">Assignments to create</span>
              <span class="value">{{ previewResult.assignmentsToCreate ?? 0 }}</span>
            </div>
            <div class="preview-stat">
              <span class="label">Assignments to update</span>
              <span class="value">{{ previewResult.assignmentsToUpdate ?? 0 }}</span>
            </div>
          </div>
          <pre class="preview-json" *ngIf="previewJson"><code>{{ previewJson }}</code></pre>
        </div>

        <div class="import-result success" *ngIf="importSuccess">{{ importSuccess }}</div>
        <div class="error-msg" *ngIf="importError">{{ importError }}</div>
      </div>
    </div>
  `,
  styles: [`
    .import-export { padding: 24px; }
    h1 { margin: 0 0 20px; font-size: 24px; color: #333; }
    h2 { margin: 0 0 16px; font-size: 18px; color: #333; }
    h3 { margin: 16px 0 12px; font-size: 16px; color: #555; }
    .section-card {
      background: #fff;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      padding: 20px;
      margin-bottom: 24px;
    }
    .export-controls {
      display: flex;
      gap: 12px;
      align-items: flex-end;
      margin-bottom: 16px;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
      flex: 1;
    }
    .form-group label {
      font-size: 12px;
      font-weight: 600;
      color: #555;
      text-transform: uppercase;
    }
    .form-group input,
    .form-group textarea {
      padding: 8px 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
      font-family: inherit;
    }
    .form-group input:focus,
    .form-group textarea:focus { outline: none; border-color: #6c63ff; }
    .form-group textarea {
      font-family: monospace;
      resize: vertical;
    }
    .export-stats {
      display: flex;
      gap: 16px;
      margin-bottom: 12px;
    }
    .export-stats .stat {
      font-size: 13px;
      color: #555;
      background: #f8f8fc;
      padding: 4px 10px;
      border-radius: 4px;
    }
    .code-block-wrapper { position: relative; }
    .btn-copy {
      position: absolute;
      top: 8px;
      right: 8px;
      padding: 4px 10px;
      background: #6c63ff;
      color: #fff;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 12px;
      z-index: 1;
    }
    .btn-copy:hover { background: #5a52e0; }
    pre {
      background: #1a1a2e;
      color: #e0e0e0;
      padding: 16px;
      border-radius: 6px;
      overflow-x: auto;
      font-size: 13px;
      line-height: 1.5;
      max-height: 400px;
      overflow-y: auto;
      margin: 0;
    }
    code { font-family: 'Courier New', monospace; }
    .import-actions {
      display: flex;
      gap: 10px;
      margin-top: 12px;
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
    .preview-result {
      margin-top: 16px;
      padding: 16px;
      background: #f8f8fc;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
    }
    .preview-stats {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 12px;
    }
    .preview-stat {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .preview-stat .label {
      font-size: 12px;
      color: #888;
      text-transform: uppercase;
      font-weight: 600;
    }
    .preview-stat .value { font-size: 20px; font-weight: 700; color: #6c63ff; }
    .preview-json {
      margin-top: 12px;
    }
    .error-msg {
      margin-top: 12px;
      padding: 10px 14px;
      background: #fff5f5;
      border: 1px solid #ffcccc;
      border-radius: 4px;
      color: #d44;
      font-size: 14px;
    }
    .import-result {
      margin-top: 12px;
      padding: 10px 14px;
      border-radius: 4px;
      font-size: 14px;
    }
    .import-result.success {
      background: #eaffea;
      border: 1px solid #b0e0b0;
      color: #1a8a1a;
    }
  `]
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
