import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent },
  // Lazy-loaded feature routes will be added as components are built
  // { path: 'definitions', loadComponent: () => import('./components/definitions/...') },
  // { path: 'values', loadComponent: () => import('./components/values/...') },
  // { path: 'effective', loadComponent: () => import('./components/effective/...') },
  // { path: 'secrets', loadComponent: () => import('./components/secrets/...') },
  // { path: 'audit', loadComponent: () => import('./components/audit/...') },
  // { path: 'import-export', loadComponent: () => import('./components/import-export/...') },
];
