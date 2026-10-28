import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Branch {
  id: number;
  name: string;
  address: string;
  city: string;
}

export interface AvailabilitySlot {
  time: string;
  remainingCapacity: number;
  capacity: number;
}

export interface BookAppointmentRequest {
  branchId: number;
  date: string;
  time: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  idempotencyKey: string;
}

export interface AppointmentConfirmation {
  id: number;
  reference: string;
  branchName: string;
  branchAddress: string;
  customerName: string;
  email: string;
  phoneNumber: string;
  startsAt: string;
  status: string;
  isNewBooking: boolean;
}

@Injectable({ providedIn: 'root' })
export class AppointmentService {
  constructor(private readonly http: HttpClient) {}

  getBranches(): Observable<Branch[]> {
    return this.http.get<Branch[]>('/api/branches');
  }

  getAvailability(branchId: number, date: string): Observable<AvailabilitySlot[]> {
    const params = new HttpParams().set('branchId', branchId).set('date', date);
    return this.http.get<AvailabilitySlot[]>('/api/appointments/availability', { params });
  }

  book(request: BookAppointmentRequest): Observable<AppointmentConfirmation> {
    return this.http.post<AppointmentConfirmation>('/api/appointments', request);
  }
}
