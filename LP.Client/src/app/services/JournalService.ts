import { Inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../app.config';

export interface PublicNewsListItem {
  id: string;
  slug: string;
  title: string;
  summary?: string | null;
  topic?: string | null;
  tags: string[];
  city?: string | null;
  heroImageUrl?: string | null;
  authorName?: string | null;
  aiGenerated: boolean;
  publishedAt?: string | null;
}

export interface PublicNewsDetail extends PublicNewsListItem {
  body: string;
  imageLicense?: string | null;
  sourceName?: string | null;
  sourceUrl?: string | null;
  originalTitle?: string | null;
  sourcePublishedAt?: string | null;
  updatedAt?: string | null;
}

@Injectable({ providedIn: 'root' })
export class JournalService {
  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) private readonly baseUrl: string
  ) {}

  list(topic?: string): Observable<PublicNewsListItem[]> {
    let params = new HttpParams().set('pageSize', 100);

    if (topic) {
      params = params.set('topic', topic);
    }

    return this.http.get<PublicNewsListItem[]>(`${this.baseUrl}/Journal/public`, { params });
  }

  getBySlug(slug: string): Observable<PublicNewsDetail> {
    return this.http.get<PublicNewsDetail>(
      `${this.baseUrl}/Journal/public/${encodeURIComponent(slug)}`
    );
  }
}
