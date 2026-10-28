import { CommonModule } from '@angular/common';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { AppComponent } from './app.component';

const branches = [
  { id: 1, name: 'Sandton City', address: 'Shop L24', city: 'Johannesburg' }
];

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      imports: [CommonModule, HttpClientTestingModule, ReactiveFormsModule]
    }).compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates the booking screen and loads branches', () => {
    fixture.detectChanges();
    const request = httpMock.expectOne('/api/branches');
    expect(request.request.method).toBe('GET');
    request.flush(branches);
    expect(component.branches).toEqual(branches);
    expect(component.loadingBranches).toBeFalse();
  });

  it('loads availability when a branch and date are selected', () => {
    component.form.controls.branchId.setValue(1);
    component.form.controls.date.setValue('2026-10-27');
    component.onBranchChange();

    const request = httpMock.expectOne(req => req.url === '/api/appointments/availability');
    expect(request.request.params.get('branchId')).toBe('1');
    expect(request.request.params.get('date')).toBe('2026-10-27');
    request.flush([{ time: '09:00', remainingCapacity: 4, capacity: 4 }]);
    expect(component.slots.length).toBe(1);
  });

  it('does not submit an invalid form', () => {
    component.submit();
    httpMock.expectNone('/api/appointments');
    expect(component.form.invalid).toBeTrue();
  });
});
