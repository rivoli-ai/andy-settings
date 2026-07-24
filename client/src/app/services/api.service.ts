import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Definitions
  getDefinitions(params?: { applicationCode?: string; category?: string; search?: string; page?: number; pageSize?: number }): Observable<any> {
    let httpParams = new HttpParams();
    if (params?.applicationCode) httpParams = httpParams.set('applicationCode', params.applicationCode);
    if (params?.category) httpParams = httpParams.set('category', params.category);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get(`${this.baseUrl}/definitions`, { params: httpParams });
  }

  getDefinition(key: string): Observable<any> {
    return this.http.get(`${this.baseUrl}/definitions/${encodeURIComponent(key)}`);
  }

  createDefinition(dto: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/definitions`, dto);
  }

  updateDefinition(key: string, dto: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/definitions/${encodeURIComponent(key)}`, dto);
  }

  deleteDefinition(key: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/definitions/${encodeURIComponent(key)}`);
  }

  // Values
  // page/pageSize are forwarded so the values view can page. Without them the
  // API applies its default of 25 and the screen silently showed only the
  // first page, with no control and no indication more existed.
  getValues(params?: {
    definitionKey?: string;
    scopeType?: string;
    scopeId?: string;
    page?: number;
    pageSize?: number;
  }): Observable<any> {
    let httpParams = new HttpParams();
    if (params?.definitionKey) httpParams = httpParams.set('definitionKey', params.definitionKey);
    if (params?.scopeType) httpParams = httpParams.set('scopeType', params.scopeType);
    if (params?.scopeId) httpParams = httpParams.set('scopeId', params.scopeId);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get(`${this.baseUrl}/values`, { params: httpParams });
  }

  setValue(dto: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/values`, dto);
  }

  deleteValue(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/values/${encodeURIComponent(id)}`);
  }

  // Secrets are deliberately isolated from ordinary value endpoints.
  setSecret(definitionKey: string, body: { scopeType: string; scopeId: string | null; value: string }): Observable<any> {
    return this.http.post(`${this.baseUrl}/secrets/${encodeURIComponent(definitionKey)}`, body);
  }

  rotateSecret(definitionKey: string, body: { scopeType: string; scopeId: string | null; newValue: string }): Observable<any> {
    return this.http.post(`${this.baseUrl}/secrets/${encodeURIComponent(definitionKey)}/rotate`, body);
  }

  // Deletion is per-scope. allScopes must be requested explicitly so clearing
  // one user's credential cannot silently wipe every other scope too.
  deleteSecret(
    definitionKey: string,
    opts: { scopeType?: string; scopeId?: string | null; allScopes?: boolean }
  ): Observable<any> {
    let params = new HttpParams();
    if (opts.scopeType) params = params.set('scopeType', opts.scopeType);
    if (opts.scopeId) params = params.set('scopeId', opts.scopeId);
    if (opts.allScopes) params = params.set('allScopes', 'true');
    return this.http.delete(`${this.baseUrl}/secrets/${encodeURIComponent(definitionKey)}`, { params });
  }

  // Effective resolution
  resolve(key: string, context: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/effective/resolve`, { key, context });
  }

  explain(key: string, context: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/effective/explain`, { key, context });
  }

  // Audit
  getAuditEvents(params?: { definitionKey?: string; page?: number; pageSize?: number }): Observable<any> {
    let httpParams = new HttpParams();
    if (params?.definitionKey) httpParams = httpParams.set('definitionKey', params.definitionKey);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get(`${this.baseUrl}/audit`, { params: httpParams });
  }

  // Export
  exportSettings(applicationCode?: string): Observable<any> {
    let httpParams = new HttpParams();
    if (applicationCode) httpParams = httpParams.set('applicationCode', applicationCode);
    return this.http.get(`${this.baseUrl}/export`, { params: httpParams });
  }

  // Import
  importPreview(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/import/preview`, data);
  }

  importSettings(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/import`, data);
  }

  // Health
  getHealth(): Observable<any> {
    return this.http.get('/health');
  }
}
