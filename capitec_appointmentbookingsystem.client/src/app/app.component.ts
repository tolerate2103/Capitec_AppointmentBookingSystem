import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  AppointmentConfirmation,
  AppointmentService,
  AvailabilitySlot,
  Branch
} from './services/appointment.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  branches: Branch[] = [];
  slots: AvailabilitySlot[] = [];
  loadingBranches = true;
  loadingSlots = false;
  submitting = false;
  bookingError = '';
  pageError = '';
  confirmation: AppointmentConfirmation | null = null;
  idempotencyKey = this.createRequestKey();
  referenceCopied = false;
  readonly currentYear = new Date().getFullYear();

  readonly minDate = this.toDateInputValue(new Date());
  readonly maxDate = this.toDateInputValue(this.addDays(new Date(), 60));

  readonly form = this.formBuilder.group({
    branchId: [null as number | null, Validators.required],
    date: [this.getNextBusinessDay(), Validators.required],
    time: ['', Validators.required],
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^(?=.*\d)\+?[0-9 ()-]{7,20}$/)]]
  });

  constructor(private readonly appointmentService: AppointmentService) {}

  ngOnInit(): void {
    this.appointmentService.getBranches().subscribe({
      next: (branches: Branch[]) => {
        this.branches = branches;
        this.loadingBranches = false;
      },
      error: () => {
        this.loadingBranches = false;
        this.pageError = 'We could not load branches right now. Please refresh the page to try again.';
      }
    });
  }

  get selectedBranch(): Branch | undefined {
    return this.branches.find(branch => branch.id === Number(this.form.controls.branchId.value));
  }

  get selectedSlot(): AvailabilitySlot | undefined {
    return this.slots.find(slot => slot.time === this.form.controls.time.value);
  }

  get currentStep(): number {
    if (this.confirmation) return 3;
    if (this.form.controls.branchId.value && this.form.controls.date.value && this.form.controls.time.value) return 3;
    if (this.form.controls.branchId.value) return 2;
    return 1;
  }

  onBranchChange(): void {
    this.form.controls.time.setValue('');
    this.bookingError = '';
    this.loadAvailability();
  }

  onDateChange(): void {
    this.form.controls.time.setValue('');
    const selectedDate = this.form.controls.date.value;
    if (selectedDate) {
      const dayOfWeek = new Date(`${selectedDate}T12:00:00`).getDay();
      if (dayOfWeek === 0 || dayOfWeek === 6) {
        this.slots = [];
        this.bookingError = 'Branches are available for appointments Monday to Friday. Please choose a weekday.';
        return;
      }
    }
    this.bookingError = '';
    this.loadAvailability();
  }

  selectSlot(slot: AvailabilitySlot): void {
    this.form.controls.time.setValue(slot.time);
    this.form.controls.time.markAsTouched();
    this.bookingError = '';
  }

  submit(): void {
    this.bookingError = '';
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting) return;

    const value = this.form.getRawValue();
    if (value.branchId === null || !value.date || !value.time) return;

    this.submitting = true;
    this.appointmentService.book({
      branchId: value.branchId,
      date: value.date,
      time: value.time,
      fullName: value.fullName?.trim() ?? '',
      email: value.email?.trim() ?? '',
      phoneNumber: value.phoneNumber?.trim() ?? '',
      idempotencyKey: this.idempotencyKey
    }).pipe(finalize(() => this.submitting = false)).subscribe({
      next: (confirmation: AppointmentConfirmation | null) => {
        this.confirmation = confirmation;
        // A new key is created only after the server confirms the save.
        this.idempotencyKey = this.createRequestKey();
      },
      error: (error: HttpErrorResponse) => {
        this.bookingError = error.error?.message ?? 'Your appointment could not be saved. Please try again.';
        if (error.status === 409) {
          this.form.controls.time.setValue('');
          this.loadAvailability();
        }
      }
    });
  }

  async copyReference(reference: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(reference);
      this.referenceCopied = true;
    } catch {
      this.referenceCopied = false;
    }
  }

  bookAnother(): void {
    this.referenceCopied = false;
    this.confirmation = null;
    this.form.controls.time.setValue('');
    this.form.controls.fullName.setValue('');
    this.form.controls.email.setValue('');
    this.form.controls.phoneNumber.setValue('');
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.loadAvailability();
  }

  fieldInvalid(field: 'branchId' | 'date' | 'time' | 'fullName' | 'email' | 'phoneNumber'): boolean {
    const control = this.form.controls[field];
    return control.invalid && (control.touched || control.dirty);
  }

  formatDate(date: string): string {
    return new Intl.DateTimeFormat('en-ZA', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
      .format(new Date(`${date}T12:00:00`));
  }

  formatTime(time: string): string {
    const [hour, minute] = time.split(':').map(Number);
    const date = new Date();
    date.setHours(hour, minute, 0, 0);
    return new Intl.DateTimeFormat('en-ZA', { hour: 'numeric', minute: '2-digit' }).format(date);
  }

  private loadAvailability(): void {
    const branchId = this.form.controls.branchId.value;
    const date = this.form.controls.date.value;
    this.slots = [];
    if (!branchId || !date) return;

    this.loadingSlots = true;
    this.appointmentService.getAvailability(branchId, date)
      .pipe(finalize(() => this.loadingSlots = false))
      .subscribe({
        next: (slots: AvailabilitySlot[]) => this.slots = slots,
        error: () => this.bookingError = 'We could not load available times. Please try again.'
      });
  }

  private createRequestKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;
  }

  private getNextBusinessDay(): string {
    const date = new Date();
    date.setDate(date.getDate() + 1);
    while (date.getDay() === 0 || date.getDay() === 6) date.setDate(date.getDate() + 1);
    return this.toDateInputValue(date);
  }

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
