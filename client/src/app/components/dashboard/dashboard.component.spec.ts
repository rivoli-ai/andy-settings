import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { DashboardComponent } from './dashboard.component';
import { ApiService } from '../../services/api.service';
import { of } from 'rxjs';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let apiService: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('ApiService', ['getDefinitions', 'getAuditEvents']);
    spy.getDefinitions.and.returnValue(of({ items: [{ isSecret: true }, { isSecret: false }], totalCount: 2, page: 1, pageSize: 25 }));
    spy.getAuditEvents.and.returnValue(of({ items: [], totalCount: 10, page: 1, pageSize: 25 }));

    await TestBed.configureTestingModule({
      imports: [DashboardComponent, HttpClientTestingModule],
      providers: [{ provide: ApiService, useValue: spy }]
    }).compileComponents();

    apiService = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load definition count', () => {
    expect(component.definitionCount).toBe(2);
  });

  it('should load audit count', () => {
    expect(component.auditCount).toBe(10);
  });

  it('should count secret definitions', () => {
    expect(component.secretCount).toBe(1);
  });
});
