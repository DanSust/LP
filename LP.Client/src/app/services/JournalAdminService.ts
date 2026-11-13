import { Inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { JOURNAL_API_BASE_URL } from '../app.config';

export interface JournalConfig {
  aiConfigured: boolean;
  aiModel?: string | null;
  maxSimilarityPercent: number;
  crawlEnabled?: boolean;
}

export type JournalSourceType = 'Rss' | 'Html';

export interface JournalSourceDto {
  id: string;
  name: string;
  type: JournalSourceType;
  url?: string | null;
  isEnabled: boolean;
  topic?: string | null;
  city?: string | null;
  defaultTags?: string | null;
  syncIntervalMinutes: number;
  maxItemsPerSync: number;
  includePattern?: string | null;
  aiDrafts: boolean;
  lastSyncAt?: string | null;
  lastSyncStatus?: string | null;
  lastSyncError?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface JournalSourceUpsert {
  name: string;
  type: JournalSourceType;
  url: string;
  isEnabled: boolean;
  topic: string;
  city: string;
  defaultTags: string;
  syncIntervalMinutes: number;
  maxItemsPerSync: number;
  includePattern: string;
  aiDrafts: boolean;
}

export interface JournalSourceTestResult {
  success: boolean;
  error?: string | null;
  itemCount: number;
  items: Array<{ title: string; url?: string | null; publishedAt?: string | null }>;
}

export interface JournalSyncResult {
  success: boolean;
  created: number;
  skipped: number;
  status?: string | null;
  error?: string | null;
}

export interface AdminNewsDto {
  id: string;
  slug: string;
  title: string;
  summary?: string | null;
  body: string;
  topic?: string | null;
  tags?: string | null;
  city?: string | null;
  heroImageUrl?: string | null;
  imageLicense?: string | null;
  sourceName?: string | null;
  sourceUrl?: string | null;
  originalTitle?: string | null;
  sourcePublishedAt?: string | null;
  aiModel?: string | null;
  promptVersion?: string | null;
  aiGenerated: boolean;
  similarityPercent?: number | null;
  authorName?: string | null;
  editorNotes?: string | null;
  status: string;
  factsChecked: boolean;
  attributionChecked: boolean;
  imageRightsChecked: boolean;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string | null;
  rejectionReason?: string | null;
}

export interface NewsUpsert {
  title: string;
  summary: string;
  body: string;
  topic: string;
  tags: string;
  city: string;
  heroImageUrl: string;
  imageLicense: string;
  sourceName: string;
  sourceUrl: string;
  originalTitle: string;
  sourcePublishedAt: string | null;
  authorName: string;
  editorNotes: string;
  factsChecked: boolean;
  attributionChecked: boolean;
  imageRightsChecked: boolean;
  similarityPercent: number | null;
  aiModel: string;
  promptVersion: string;
  aiGenerated: boolean;
}

export interface DraftFromUrlResult {
  success: boolean;
  error?: string | null;
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  sourceName?: string | null;
  sourceUrl?: string | null;
  originalTitle?: string | null;
  sourcePublishedAt?: string | null;
  aiModel?: string | null;
  promptVersion?: string | null;
  similarityPercent?: number | null;
}

@Injectable({ providedIn: 'root' })
export class JournalAdminService {
  constructor(
    private readonly http: HttpClient,
    @Inject(JOURNAL_API_BASE_URL) private readonly journalBaseUrl: string
  ) {}

  getConfig(key: string): Observable<JournalConfig> {
    return this.http.get<JournalConfig>(`${this.journalBaseUrl}/Journal/admin/config`, {
      headers: this.headers(key)
    });
  }

  getItems(key: string, status = 'Draft'): Observable<AdminNewsDto[]> {
    return this.http.get<AdminNewsDto[]>(`${this.journalBaseUrl}/Journal/admin/items`, {
      headers: this.headers(key),
      params: new HttpParams().set('status', status).set('pageSize', 200)
    });
  }

  createItem(key: string, dto: NewsUpsert): Observable<AdminNewsDto> {
    return this.http.post<AdminNewsDto>(`${this.journalBaseUrl}/Journal/admin/items`, dto, {
      headers: this.headers(key)
    });
  }

  updateItem(key: string, id: string, dto: NewsUpsert): Observable<AdminNewsDto> {
    return this.http.put<AdminNewsDto>(`${this.journalBaseUrl}/Journal/admin/items/${id}`, dto, {
      headers: this.headers(key)
    });
  }

  publishItem(key: string, id: string): Observable<AdminNewsDto> {
    return this.http.post<AdminNewsDto>(
      `${this.journalBaseUrl}/Journal/admin/items/${id}/publish`,
      {},
      { headers: this.headers(key) }
    );
  }

  unpublishItem(key: string, id: string): Observable<AdminNewsDto> {
    return this.http.post<AdminNewsDto>(
      `${this.journalBaseUrl}/Journal/admin/items/${id}/unpublish`,
      {},
      { headers: this.headers(key) }
    );
  }

  rejectItem(key: string, id: string, reason: string): Observable<AdminNewsDto> {
    return this.http.post<AdminNewsDto>(
      `${this.journalBaseUrl}/Journal/admin/items/${id}/reject`,
      { reason },
      { headers: this.headers(key) }
    );
  }

  deleteItem(key: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.journalBaseUrl}/Journal/admin/items/${id}`, {
      headers: this.headers(key)
    });
  }

  draftFromUrl(
    key: string,
    url: string,
    topic: string,
    city: string,
    instructions: string
  ): Observable<DraftFromUrlResult> {
    return this.http.post<DraftFromUrlResult>(
      `${this.journalBaseUrl}/Journal/admin/draft-from-url`,
      { url, topic, city, instructions },
      { headers: this.headers(key) }
    );
  }

  getSources(key: string): Observable<JournalSourceDto[]> {
    return this.http.get<JournalSourceDto[]>(`${this.journalBaseUrl}/Journal/admin/sources`, {
      headers: this.headers(key)
    });
  }

  createSource(key: string, dto: JournalSourceUpsert): Observable<JournalSourceDto> {
    return this.http.post<JournalSourceDto>(`${this.journalBaseUrl}/Journal/admin/sources`, dto, {
      headers: this.headers(key)
    });
  }

  updateSource(key: string, id: string, dto: JournalSourceUpsert): Observable<JournalSourceDto> {
    return this.http.put<JournalSourceDto>(
      `${this.journalBaseUrl}/Journal/admin/sources/${id}`,
      dto,
      { headers: this.headers(key) }
    );
  }

  deleteSource(key: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.journalBaseUrl}/Journal/admin/sources/${id}`, {
      headers: this.headers(key)
    });
  }

  testSource(key: string, id: string): Observable<JournalSourceTestResult> {
    return this.http.post<JournalSourceTestResult>(
      `${this.journalBaseUrl}/Journal/admin/sources/${id}/test`,
      {},
      { headers: this.headers(key) }
    );
  }

  syncSource(key: string, id: string): Observable<JournalSyncResult> {
    return this.http.post<JournalSyncResult>(
      `${this.journalBaseUrl}/Journal/admin/sources/${id}/sync`,
      {},
      { headers: this.headers(key) }
    );
  }

  syncAllSources(key: string): Observable<JournalSyncResult> {
    return this.http.post<JournalSyncResult>(
      `${this.journalBaseUrl}/Journal/admin/sources/sync`,
      {},
      { headers: this.headers(key) }
    );
  }

  private headers(key: string): HttpHeaders {
    return new HttpHeaders({ 'X-Admin-Key': key });
  }
}
