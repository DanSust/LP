import { Inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../app.config';

export interface PublicEventListItem {
  id: string;
  slug: string;
  title: string;
  summary?: string | null;
  startsAt: string;
  endsAt?: string | null;
  city?: string | null;
  venue?: string | null;
  imageUrl?: string | null;
  sourceUrl?: string | null;
  tags: string[];
}

export interface PublicEventDetail extends PublicEventListItem {
  description?: string | null;
  address?: string | null;
  publishedAt?: string | null;
}

export interface PublicEventsQuery {
  city?: string;
  tag?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class EventService {
  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) private readonly baseUrl: string
  ) {}

  list(query: PublicEventsQuery = {}): Observable<PublicEventListItem[]> {
    let params = new HttpParams();

    if (query.city) params = params.set('city', query.city);
    if (query.tag) params = params.set('tag', query.tag);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.page) params = params.set('page', query.page);
    if (query.pageSize) params = params.set('pageSize', query.pageSize);

    return this.http.get<PublicEventListItem[]>(`${this.baseUrl}/Events/public`, { params });
  }

  getBySlug(slug: string): Observable<PublicEventDetail> {
    return this.http.get<PublicEventDetail>(
      `${this.baseUrl}/Events/public/${encodeURIComponent(slug)}`
    );
  }
}
