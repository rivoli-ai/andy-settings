import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ApiService]
    });
    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch definitions', () => {
    const mockResponse = { items: [{ key: 'test.key' }], totalCount: 1, page: 1, pageSize: 25 };
    service.getDefinitions().subscribe(data => {
      expect(data.totalCount).toBe(1);
      expect(data.items.length).toBe(1);
    });
    const req = httpMock.expectOne('/api/definitions');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should fetch definitions with filters', () => {
    service.getDefinitions({ applicationCode: 'containers', search: 'test' }).subscribe();
    const req = httpMock.expectOne(r => r.url === '/api/definitions' && r.params.get('applicationCode') === 'containers');
    expect(req.request.params.get('search')).toBe('test');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 25 });
  });

  it('should get a single definition', () => {
    service.getDefinition('andy.test.key').subscribe(data => {
      expect(data.key).toBe('andy.test.key');
    });
    const req = httpMock.expectOne('/api/definitions/andy.test.key');
    expect(req.request.method).toBe('GET');
    req.flush({ key: 'andy.test.key', displayName: 'Test' });
  });

  it('should create a definition', () => {
    const dto = { key: 'new.key', applicationCode: 'test', displayName: 'New', dataType: 'String' };
    service.createDefinition(dto).subscribe(data => {
      expect(data.key).toBe('new.key');
    });
    const req = httpMock.expectOne('/api/definitions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.key).toBe('new.key');
    req.flush({ ...dto, id: '123' });
  });

  it('should set a value', () => {
    const dto = { definitionKey: 'key', scopeType: 'User', scopeId: 'u1', valueJson: '"val"' };
    service.setValue(dto).subscribe();
    const req = httpMock.expectOne('/api/values');
    expect(req.request.method).toBe('POST');
    req.flush({ id: '123', ...dto });
  });

  it('should resolve effective value', () => {
    service.resolve('andy.test', { userId: 'u1' }).subscribe(data => {
      expect(data.effectiveValue).toBe('"hello"');
    });
    const req = httpMock.expectOne('/api/effective/resolve');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.key).toBe('andy.test');
    req.flush({ key: 'andy.test', effectiveValue: '"hello"', isDefault: false });
  });

  it('should explain resolution', () => {
    service.explain('andy.test', { userId: 'u1' }).subscribe(data => {
      expect(data.sourceChain.length).toBe(2);
    });
    const req = httpMock.expectOne('/api/effective/explain');
    expect(req.request.method).toBe('POST');
    req.flush({ key: 'andy.test', sourceChain: [{ scopeType: 'Machine' }, { scopeType: 'User', isWinner: true }] });
  });

  it('should fetch audit events', () => {
    service.getAuditEvents({ definitionKey: 'test' }).subscribe(data => {
      expect(data.totalCount).toBe(5);
    });
    const req = httpMock.expectOne(r => r.url === '/api/audit');
    expect(req.request.params.get('definitionKey')).toBe('test');
    req.flush({ items: [], totalCount: 5, page: 1, pageSize: 25 });
  });

  it('should export settings', () => {
    service.exportSettings('containers').subscribe(data => {
      expect(data.definitionCount).toBe(10);
    });
    const req = httpMock.expectOne(r => r.url === '/api/export');
    expect(req.request.params.get('applicationCode')).toBe('containers');
    req.flush({ data: '{}', definitionCount: 10, assignmentCount: 0 });
  });

  it('should delete a value', () => {
    service.deleteValue('abc-123').subscribe();
    const req = httpMock.expectOne('/api/values/abc-123');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('should check health', () => {
    service.getHealth().subscribe(data => {
      expect(data.status).toBe('healthy');
    });
    const req = httpMock.expectOne('/health');
    req.flush({ status: 'healthy' });
  });
});
