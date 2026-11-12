import { Inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../app.config';

export interface EventSourceDto {
  id: string;
  name: string;
  type: string;
  url?: string | null;
  isEnabled: boolean;
  city?: string | null;
  defaultTags?: string | null;
  syncIntervalMinutes: number;
  configJson?: string | null;
  lastSyncAt?: string | null;
  lastSyncStatus?: string | null;
  lastSyncError?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface EventSourceUpsert {
  name: string;
  type: string;
  url: string;
  isEnabled: boolean;
  city: string;
  defaultTags: string;
  syncIntervalMinutes: number;
  configJson: string;
}

export interface AdminEventDto {
  id: string;
  slug: string;
  title: string;
  summary?: string | null;
  description?: string | null;
  startsAt: string;
  endsAt?: string | null;
  city?: string | null;
  venue?: string | null;
  address?: string | null;
  imageUrl?: string | null;
  sourceUrl?: string | null;
  tags?: string | null;
  status: string;
  isActive: boolean;
  sourceId?: string | null;
  sourceName?: string | null;
  externalId?: string | null;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string | null;
  rejectionReason?: string | null;
}

export interface AdminEventUpsert {
  title: string;
  summary: string;
  description: string;
  startsAt: string;
  endsAt: string;
  city: string;
  venue: string;
  address: string;
  imageUrl: string;
  sourceUrl: string;
  tags: string;
  publishImmediately: boolean;
}

export interface EventImportResultDto {
  success: boolean;
  created: number;
  updated: number;
  skipped: number;
  error?: string | null;
}

export interface EventSourcePreviewItemDto {
  title: string;
  summary?: string | null;
  startsAt: string;
  endsAt?: string | null;
  city?: string | null;
  venue?: string | null;
  imageUrl?: string | null;
  sourceUrl?: string | null;
}

export interface EventSourcePreviewDto {
  success: boolean;
  error?: string | null;
  total: number;
  items: EventSourcePreviewItemDto[];
}

@Injectable({ providedIn: 'root' })
export class EventsAdminService {
  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) private readonly baseUrl: string
  ) {}

  getSources(key: string): Observable<EventSourceDto[]> {
    return this.http.get<EventSourceDto[]>(`${this.baseUrl}/Events/admin/sources`, {
      headers: this.headers(key)
    });
  }

  createSource(key: string, dto: EventSourceUpsert): Observable<EventSourceDto> {
    return this.http.post<EventSourceDto>(`${this.baseUrl}/Events/admin/sources`, dto, {
      headers: this.headers(key)
    });
  }

  updateSource(key: string, id: string, dto: EventSourceUpsert): Observable<EventSourceDto> {
    return this.http.put<EventSourceDto>(`${this.baseUrl}/Events/admin/sources/${id}`, dto, {
      headers: this.headers(key)
    });
  }

  deleteSource(key: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/Events/admin/sources/${id}`, {
      headers: this.headers(key)
    });
  }

  syncSource(key: string, id: string): Observable<EventImportResultDto> {
    return this.http.post<EventImportResultDto>(
      `${this.baseUrl}/Events/admin/sources/${id}/sync`,
      {},
      { headers: this.headers(key) }
    );
  }

  testSource(key: string, id: string): Observable<EventSourcePreviewDto> {
    return this.http.post<EventSourcePreviewDto>(
      `${this.baseUrl}/Events/admin/sources/${id}/test`,
      {},
      { headers: this.headers(key) }
    );
  }

  getEvents(key: string, status = 'Draft'): Observable<AdminEventDto[]> {
    return this.http.get<AdminEventDto[]>(`${this.baseUrl}/Events/admin/events`, {
      headers: this.headers(key),
      params: { status, pageSize: 100 }
    });
  }

  createEvent(key: string, dto: AdminEventUpsert): Observable<AdminEventDto> {
    return this.http.post<AdminEventDto>(`${this.baseUrl}/Events/admin/events`, dto, {
      headers: this.headers(key)
    });
  }

  updateEvent(key: string, id: string, dto: AdminEventUpsert): Observable<AdminEventDto> {
    return this.http.put<AdminEventDto>(`${this.baseUrl}/Events/admin/events/${id}`, dto, {
      headers: this.headers(key)
    });
  }

  publishEvent(key: string, id: string): Observable<AdminEventDto> {
    return this.http.post<AdminEventDto>(
      `${this.baseUrl}/Events/admin/events/${id}/publish`,
      {},
      { headers: this.headers(key) }
    );
  }

  unpublishEvent(key: string, id: string): Observable<AdminEventDto> {
    return this.http.post<AdminEventDto>(
      `${this.baseUrl}/Events/admin/events/${id}/unpublish`,
      {},
      { headers: this.headers(key) }
    );
  }

  rejectEvent(key: string, id: string, reason: string): Observable<AdminEventDto> {
    return this.http.post<AdminEventDto>(
      `${this.baseUrl}/Events/admin/events/${id}/reject`,
      { reason },
      { headers: this.headers(key) }
    );
  }

  deleteEvent(key: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/Events/admin/events/${id}`, {
      headers: this.headers(key)
    });
  }

  private headers(key: string): HttpHeaders {
    return new HttpHeaders({ 'X-Admin-Key': key });
  }
}
