import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { DefinitionListComponent } from './components/definitions/definition-list.component';
import { ValueEditorComponent } from './components/values/value-editor.component';
import { EffectiveExplorerComponent } from './components/effective/effective-explorer.component';
import { SecretManagerComponent } from './components/secrets/secret-manager.component';
import { AuditTimelineComponent } from './components/audit/audit-timeline.component';
import { ImportExportComponent } from './components/import-export/import-export.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'callback', redirectTo: 'dashboard' },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'definitions', component: DefinitionListComponent, canActivate: [authGuard] },
  { path: 'values', component: ValueEditorComponent, canActivate: [authGuard] },
  { path: 'effective', component: EffectiveExplorerComponent, canActivate: [authGuard] },
  { path: 'secrets', component: SecretManagerComponent, canActivate: [authGuard] },
  { path: 'audit', component: AuditTimelineComponent, canActivate: [authGuard] },
  { path: 'import-export', component: ImportExportComponent, canActivate: [authGuard] },
];
