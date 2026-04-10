import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  let component: SidebarComponent;
  let fixture: ComponentFixture<SidebarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(SidebarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render navigation links', () => {
    const links = fixture.nativeElement.querySelectorAll('.sidebar-nav a');
    expect(links.length).toBe(7);
  });

  it('should have Dashboard as first link', () => {
    const firstLink = fixture.nativeElement.querySelector('.sidebar-nav a');
    expect(firstLink.textContent.trim()).toBe('Dashboard');
  });

  it('should render all section links', () => {
    const linkTexts = Array.from(fixture.nativeElement.querySelectorAll('.sidebar-nav a'))
      .map((a: any) => a.textContent.trim());
    expect(linkTexts).toContain('Definitions');
    expect(linkTexts).toContain('Values');
    expect(linkTexts).toContain('Effective');
    expect(linkTexts).toContain('Secrets');
    expect(linkTexts).toContain('Audit');
    expect(linkTexts).toContain('Import / Export');
  });
});
