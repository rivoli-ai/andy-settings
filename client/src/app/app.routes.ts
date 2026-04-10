import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { DefinitionListComponent } from './components/definitions/definition-list.component';
import { ValueEditorComponent } from './components/values/value-editor.component';
import { EffectiveExplorerComponent } from './components/effective/effective-explorer.component';
import { SecretManagerComponent } from './components/secrets/secret-manager.component';
import { AuditTimelineComponent } from './components/audit/audit-timeline.component';
import { ImportExportComponent } from './components/import-export/import-export.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'definitions', component: DefinitionListComponent },
  { path: 'values', component: ValueEditorComponent },
  { path: 'effective', component: EffectiveExplorerComponent },
  { path: 'secrets', component: SecretManagerComponent },
  { path: 'audit', component: AuditTimelineComponent },
  { path: 'import-export', component: ImportExportComponent },
];
