import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, finalize, forkJoin, takeUntil } from 'rxjs';
import {
  AdminEventDto,
  AdminEventUpsert,
  EventSourceDto,
  EventSourcePreviewDto,
  EventSourceUpsert,
  EventsAdminService
} from '../services/EventsAdminService';
import { SeoService } from '../services/SeoService';

type AdminTab = 'sources' | 'events';

@Component({
  selector: 'app-admin-events',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-events.component.html',
  styleUrls: ['./admin-events.component.scss']
})
export class AdminEventsComponent implements OnInit, OnDestroy {
  private readonly adminService = inject(EventsAdminService);
  private readonly seoService = inject(SeoService);
  private readonly destroy$ = new Subject<void>();
  private readonly storageKey = 'made4love.events.adminKey';

  adminKey = sessionStorage.getItem(this.storageKey) ?? '';
  isAuthorized = false;
  activeTab: AdminTab = 'sources';
  isLoading = false;
  error: string | null = null;
  notice: string | null = null;
  busySourceId: string | null = null;

  sources: EventSourceDto[] = [];
  events: AdminEventDto[] = [];
  statusFilter = 'Draft';

  sourceForm: EventSourceUpsert = this.emptySource();
  editingSourceId: string | null = null;
  showSourceForm = false;

  preview: EventSourcePreviewDto | null = null;
  previewSourceName = '';

  eventForm: AdminEventUpsert = this.emptyEvent();
  startsAtLocal = '';
  endsAtLocal = '';
  editingEventId: string | null = null;
  showEventForm = false;

  rejectingId: string | null = null;
  rejectReason = '';

  ngOnInit(): void {
    this.seoService.updateMetaTags({
      title: 'Админка событий — Made4Love',
      description: 'Управление источниками и модерация событий Made4Love.',
      url: '/admin/events',
      robots: 'noindex,nofollow'
    });

    if (this.adminKey) {
      this.refresh();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  login(): void {
    const key = this.adminKey.trim();
    if (!key) {
      this.error = 'Введите админ-ключ.';
      return;
    }

    this.adminKey = key;
    sessionStorage.setItem(this.storageKey, key);
    this.refresh();
  }

  logout(): void {
    sessionStorage.removeItem(this.storageKey);
    this.adminKey = '';
    this.isAuthorized = false;
    this.sources = [];
    this.events = [];
    this.preview = null;
    this.error = null;
    this.notice = null;
  }

  setTab(tab: AdminTab): void {
    this.activeTab = tab;
    this.error = null;
    this.notice = null;

    if (!this.isAuthorized) {
      return;
    }

    if (tab === 'sources') {
      this.loadSources();
    } else {
      this.loadEvents();
    }
  }

  // ---------- Источники ----------

  startCreateSource(): void {
    this.sourceForm = this.emptySource();
    this.editingSourceId = null;
    this.showSourceForm = true;
    this.error = null;
    this.notice = null;
  }

  editSource(source: EventSourceDto): void {
    this.sourceForm = {
      name: source.name,
      type: source.type,
      url: source.url ?? '',
      isEnabled: source.isEnabled,
      city: source.city ?? '',
      defaultTags: source.defaultTags ?? '',
      syncIntervalMinutes: source.syncIntervalMinutes,
      configJson: source.configJson ?? ''
    };
    this.editingSourceId = source.id;
    this.showSourceForm = true;
    this.error = null;
    this.notice = null;
  }

  cancelSourceForm(): void {
    this.showSourceForm = false;
    this.editingSourceId = null;
    this.sourceForm = this.emptySource();
  }

  saveSource(): void {
    if (!this.sourceForm.name.trim()) {
      this.error = 'Укажите название источника.';
      return;
    }

    if (this.sourceForm.type !== 'Manual' && !this.sourceForm.url.trim()) {
      this.error = 'Для RSS/JSON источника нужен URL.';
      return;
    }

    const request = this.editingSourceId
      ? this.adminService.updateSource(this.adminKey, this.editingSourceId, this.sourceForm)
      : this.adminService.createSource(this.adminKey, this.sourceForm);

    this.isLoading = true;
    this.error = null;

    request
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = this.editingSourceId ? 'Источник обновлён.' : 'Источник создан.';
          this.cancelSourceForm();
          this.loadSources();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  testSource(source: EventSourceDto): void {
    this.busySourceId = source.id;
    this.preview = null;
    this.previewSourceName = source.name;
    this.error = null;

    this.adminService
      .testSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.busySourceId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (preview) => {
          this.preview = preview;
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  syncSource(source: EventSourceDto): void {
    this.busySourceId = source.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .syncSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.busySourceId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          this.notice = result.success
            ? `Импорт завершён: новых ${result.created}, обновлено ${result.updated}, пропущено ${result.skipped}.`
            : `Ошибка импорта: ${result.error ?? 'неизвестная ошибка'}`;
          this.loadSources();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  deleteSource(source: EventSourceDto): void {
    if (!window.confirm(`Удалить источник «${source.name}»? События останутся, но потеряют связь с источником.`)) {
      return;
    }

    this.busySourceId = source.id;
    this.error = null;

    this.adminService
      .deleteSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.busySourceId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = 'Источник удалён.';
          this.loadSources();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  closePreview(): void {
    this.preview = null;
  }

  // ---------- События ----------

  loadEvents(): void {
    this.isLoading = true;
    this.error = null;

    this.adminService
      .getEvents(this.adminKey, this.statusFilter)
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (events) => {
          this.events = events;
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  startCreateEvent(): void {
    this.resetEventForm();
    this.showEventForm = true;
    this.error = null;
    this.notice = null;
  }

  editEvent(event: AdminEventDto): void {
    this.eventForm = {
      title: event.title,
      summary: event.summary ?? '',
      description: event.description ?? '',
      startsAt: '',
      endsAt: '',
      city: event.city ?? '',
      venue: event.venue ?? '',
      address: event.address ?? '',
      imageUrl: event.imageUrl ?? '',
      sourceUrl: event.sourceUrl ?? '',
      tags: event.tags ?? '',
      publishImmediately: false
    };
    this.startsAtLocal = this.toLocalInput(event.startsAt);
    this.endsAtLocal = this.toLocalInput(event.endsAt);
    this.editingEventId = event.id;
    this.showEventForm = true;
    this.error = null;
    this.notice = null;
  }

  cancelEventForm(): void {
    this.resetEventForm();
    this.showEventForm = false;
  }

  saveEvent(): void {
    if (!this.eventForm.title.trim()) {
      this.error = 'Укажите название события.';
      return;
    }

    if (!this.startsAtLocal) {
      this.error = 'Укажите дату и время начала.';
      return;
    }

    const dto: AdminEventUpsert = {
      ...this.eventForm,
      startsAt: new Date(this.startsAtLocal).toISOString(),
      endsAt: this.endsAtLocal ? new Date(this.endsAtLocal).toISOString() : ''
    };

    const request = this.editingEventId
      ? this.adminService.updateEvent(this.adminKey, this.editingEventId, dto)
      : this.adminService.createEvent(this.adminKey, dto);

    this.isLoading = true;
    this.error = null;

    request
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (event) => {
          this.notice = event.status === 'Published'
            ? 'Событие сохранено и опубликовано.'
            : 'Событие сохранено как черновик.';
          this.cancelEventForm();
          this.statusFilter = event.status === 'Published' ? 'Published' : 'Draft';
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  publishEvent(event: AdminEventDto): void {
    this.error = null;
    this.notice = null;

    this.adminService
      .publishEvent(this.adminKey, event.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notice = 'Событие опубликовано.';
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  unpublishEvent(event: AdminEventDto): void {
    if (!window.confirm(`Снять событие «${event.title}» с публикации? Оно вернётся в черновики и исчезнет с сайта.`)) {
      return;
    }

    this.error = null;
    this.notice = null;

    this.adminService
      .unpublishEvent(this.adminKey, event.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notice = 'Событие снято с публикации и возвращено в черновики.';
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  startReject(event: AdminEventDto): void {
    this.rejectingId = event.id;
    this.rejectReason = '';
    this.error = null;
    this.notice = null;
  }

  cancelReject(): void {
    this.rejectingId = null;
    this.rejectReason = '';
  }

  confirmReject(event: AdminEventDto): void {
    if (!this.rejectReason.trim()) {
      this.error = 'Укажите причину отклонения.';
      return;
    }

    this.adminService
      .rejectEvent(this.adminKey, event.id, this.rejectReason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notice = 'Событие отклонено.';
          this.cancelReject();
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  deleteEvent(event: AdminEventDto): void {
    if (!window.confirm(`Удалить событие «${event.title}»?`)) {
      return;
    }

    this.adminService
      .deleteEvent(this.adminKey, event.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notice = 'Событие удалено.';
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Draft':
        return 'черновик';
      case 'Published':
        return 'опубликовано';
      case 'Rejected':
        return 'отклонено';
      case 'Archived':
        return 'в архиве';
      default:
        return status;
    }
  }

  sourceTypeLabel(type: string): string {
    switch (type) {
      case 'Rss':
        return 'RSS / Atom';
      case 'Json':
        return 'JSON API';
      case 'Manual':
        return 'ручной';
      default:
        return type;
    }
  }

  trackById(_index: number, item: { id: string }): string {
    return item.id;
  }

  // ---------- Внутреннее ----------

  private refresh(): void {
    this.isLoading = true;
    this.error = null;
    this.notice = null;

    forkJoin({
      sources: this.adminService.getSources(this.adminKey),
      events: this.adminService.getEvents(this.adminKey, this.statusFilter)
    })
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: ({ sources, events }) => {
          this.sources = sources;
          this.events = events;
          this.isAuthorized = true;
        },
        error: (error: unknown) => {
          this.isAuthorized = false;
          this.error = this.describeError(error);
        }
      });
  }

  private loadSources(): void {
    this.isLoading = true;
    this.error = null;

    this.adminService
      .getSources(this.adminKey)
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (sources) => {
          this.sources = sources;
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  private resetEventForm(): void {
    this.eventForm = this.emptyEvent();
    this.startsAtLocal = '';
    this.endsAtLocal = '';
    this.editingEventId = null;
  }

  private toLocalInput(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const pad = (part: number): string => part.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  private emptySource(): EventSourceUpsert {
    return {
      name: '',
      type: 'Rss',
      url: '',
      isEnabled: true,
      city: '',
      defaultTags: '',
      syncIntervalMinutes: 360,
      configJson: ''
    };
  }

  private emptyEvent(): AdminEventUpsert {
    return {
      title: '',
      summary: '',
      description: '',
      startsAt: '',
      endsAt: '',
      city: '',
      venue: '',
      address: '',
      imageUrl: '',
      sourceUrl: '',
      tags: '',
      publishImmediately: false
    };
  }

  private describeError(error: unknown): string {
    const httpError = error as {
      status?: number;
      error?: { error?: string } | string;
      message?: string;
    };

    if (httpError?.status === 503) {
      return 'Админ-API не настроен: задайте Events:AdminKey в конфигурации сервера.';
    }

    if (httpError?.status === 401 || httpError?.status === 403) {
      return 'Неверный админ-ключ.';
    }

    if (typeof httpError?.error === 'string') {
      return httpError.error;
    }

    if (httpError?.error && typeof httpError.error === 'object' && httpError.error.error) {
      return httpError.error.error;
    }

    return httpError?.message || 'Не удалось выполнить запрос.';
  }
}
