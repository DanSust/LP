import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, finalize, forkJoin, takeUntil } from 'rxjs';
import {
  AdminNewsDto,
  JournalAdminService,
  JournalConfig,
  JournalSourceDto,
  JournalSourceTestResult,
  JournalSourceUpsert,
  NewsUpsert
} from '../services/JournalAdminService';
import { SeoService } from '../services/SeoService';

type NewsStatusFilter = 'Draft' | 'Published' | 'Rejected' | 'all';
type AdminTab = 'items' | 'sources';

@Component({
  selector: 'app-admin-journal',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-journal.component.html',
  styleUrls: ['./admin-events.component.scss', './admin-journal.component.scss']
})
export class AdminJournalComponent implements OnInit, OnDestroy {
  private readonly adminService = inject(JournalAdminService);
  private readonly seoService = inject(SeoService);
  private readonly destroy$ = new Subject<void>();
  private readonly storageKey = 'made4love.journal.adminKey';

  adminKey = sessionStorage.getItem(this.storageKey) ?? '';
  isAuthorized = false;
  isLoading = false;
  error: string | null = null;
  notice: string | null = null;
  busyId: string | null = null;

  config: JournalConfig = {
    aiConfigured: false,
    aiModel: null,
    maxSimilarityPercent: 20
  };

  items: AdminNewsDto[] = [];
  statusFilter: NewsStatusFilter = 'Draft';

  activeTab: AdminTab = 'items';

  sources: JournalSourceDto[] = [];
  showSourceForm = false;
  editingSourceId: string | null = null;
  sourceForm: JournalSourceUpsert = this.emptySourceForm();
  testingSourceId: string | null = null;
  sourceTestResult: JournalSourceTestResult | null = null;
  isSyncing = false;

  showForm = false;
  editingId: string | null = null;
  form: NewsUpsert = this.emptyForm();

  draftUrl = '';
  draftTopic = '';
  draftCity = '';
  draftInstructions = '';
  isGenerating = false;

  rejectingId: string | null = null;
  rejectReason = '';

  ngOnInit(): void {
    this.seoService.updateMetaTags({
      title: 'Журнал: админка — Made4Love',
      description: 'Редактура и модерация материалов журнала Made4Love.',
      url: '/admin/journal',
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
    this.isLoading = true;
    this.error = null;

    this.adminService
      .getConfig(key)
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (config) => {
          this.config = config;
          this.isAuthorized = true;
          sessionStorage.setItem(this.storageKey, key);
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  logout(): void {
    sessionStorage.removeItem(this.storageKey);
    this.adminKey = '';
    this.isAuthorized = false;
    this.items = [];
    this.sources = [];
    this.showForm = false;
    this.editingId = null;
    this.showSourceForm = false;
    this.editingSourceId = null;
    this.sourceTestResult = null;
  }

  refresh(): void {
    this.isLoading = true;
    this.error = null;

    forkJoin({
      config: this.adminService.getConfig(this.adminKey),
      items: this.adminService.getItems(this.adminKey, this.statusFilter)
    })
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: ({ config, items }) => {
          this.config = config;
          this.items = items;
          this.isAuthorized = true;
          sessionStorage.setItem(this.storageKey, this.adminKey);
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  setStatus(status: NewsStatusFilter): void {
    this.statusFilter = status;
    this.loadItems();
  }

  loadItems(): void {
    if (!this.isAuthorized) {
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.adminService
      .getItems(this.adminKey, this.statusFilter)
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (items) => {
          this.items = items;
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  setTab(tab: AdminTab): void {
    this.activeTab = tab;
    this.error = null;
    this.notice = null;

    if (tab === 'sources' && this.sources.length === 0) {
      this.loadSources();
    }
  }

  loadSources(): void {
    if (!this.isAuthorized) {
      return;
    }

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

  startCreateSource(): void {
    this.sourceForm = this.emptySourceForm();
    this.editingSourceId = null;
    this.showSourceForm = true;
    this.sourceTestResult = null;
    this.notice = null;
    this.error = null;
  }

  editSource(source: JournalSourceDto): void {
    this.sourceForm = this.toSourceForm(source);
    this.editingSourceId = source.id;
    this.showSourceForm = true;
    this.sourceTestResult = null;
    this.notice = null;
    this.error = null;
  }

  cancelSourceForm(): void {
    this.showSourceForm = false;
    this.editingSourceId = null;
    this.sourceForm = this.emptySourceForm();
    this.sourceTestResult = null;
  }

  saveSource(): void {
    if (!this.sourceForm.name.trim()) {
      this.error = 'Укажите название источника.';
      return;
    }

    if (!this.sourceForm.url.trim()) {
      this.error = 'Укажите URL источника.';
      return;
    }

    this.isLoading = true;
    this.error = null;
    this.notice = null;

    const request = this.editingSourceId
      ? this.adminService.updateSource(this.adminKey, this.editingSourceId, this.sourceForm)
      : this.adminService.createSource(this.adminKey, this.sourceForm);

    request
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = this.editingSourceId
            ? 'Источник обновлён.'
            : 'Источник добавлен. Проверьте его кнопкой «Проверить».';
          this.showSourceForm = false;
          this.editingSourceId = null;
          this.sourceForm = this.emptySourceForm();
          this.loadSources();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  deleteSource(source: JournalSourceDto): void {
    if (!window.confirm(`Удалить источник «${source.name}»? Черновики останутся в журнале.`)) {
      return;
    }

    this.busyId = source.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .deleteSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.busyId = null;
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

  testSource(source: JournalSourceDto): void {
    this.testingSourceId = source.id;
    this.sourceTestResult = null;
    this.error = null;
    this.notice = null;

    this.adminService
      .testSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.testingSourceId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          this.sourceTestResult = result;
          this.notice = result.success
            ? `Источник доступен. Найдено материалов: ${result.itemCount}.`
            : null;

          if (!result.success) {
            this.error = result.error || 'Не удалось прочитать источник.';
          }
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  syncSource(source: JournalSourceDto): void {
    this.isSyncing = true;
    this.error = null;
    this.notice = null;

    this.adminService
      .syncSource(this.adminKey, source.id)
      .pipe(
        finalize(() => {
          this.isSyncing = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          this.notice = result.success
            ? `Обход выполнен. Новых черновиков: ${result.created}, пропущено: ${result.skipped}.`
            : result.error || 'Не удалось выполнить обход источника.';
          this.loadSources();
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  syncAllSources(): void {
    this.isSyncing = true;
    this.error = null;
    this.notice = null;

    this.adminService
      .syncAllSources(this.adminKey)
      .pipe(
        finalize(() => {
          this.isSyncing = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          this.notice = result.success
            ? `Обход выполнен. Новых черновиков: ${result.created}, пропущено: ${result.skipped}.`
            : result.error || 'Не удалось выполнить обход источников.';
          this.loadSources();
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  sourceTypeLabel(type: string): string {
    return type === 'Html' ? 'HTML-страница' : 'RSS / Atom';
  }

  startCreate(): void {
    this.form = this.emptyForm();
    this.editingId = null;
    this.showForm = true;
    this.notice = null;
    this.error = null;
    this.scrollToForm();
  }

  editItem(item: AdminNewsDto): void {
    this.form = this.toForm(item);
    this.editingId = item.id;
    this.showForm = true;
    this.notice = null;
    this.error = null;
    this.scrollToForm();
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
    this.form = this.emptyForm();
  }

  save(): void {
    if (!this.form.title.trim()) {
      this.error = 'Заполните заголовок.';
      return;
    }

    if (!this.form.body.trim()) {
      this.error = 'Заполните текст материала.';
      return;
    }

    this.isLoading = true;
    this.error = null;
    this.notice = null;

    const request = this.editingId
      ? this.adminService.updateItem(this.adminKey, this.editingId, this.form)
      : this.adminService.createItem(this.adminKey, this.form);

    request
      .pipe(
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (item) => {
          this.notice = this.editingId
            ? 'Изменения сохранены.'
            : 'Черновик создан. Проверьте текст и опубликуйте его.';
          this.showForm = false;
          this.editingId = null;
          this.form = this.emptyForm();
          this.statusFilter = item.status === 'Published' ? 'Published' : 'Draft';
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  generateDraft(): void {
    if (!this.draftUrl.trim()) {
      this.error = 'Вставьте ссылку на источник.';
      return;
    }

    this.isGenerating = true;
    this.error = null;
    this.notice = null;

    this.adminService
      .draftFromUrl(
        this.adminKey,
        this.draftUrl.trim(),
        this.draftTopic.trim(),
        this.draftCity.trim(),
        this.draftInstructions.trim()
      )
      .pipe(
        finalize(() => {
          this.isGenerating = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          if (!result.success) {
            this.error = result.error || 'Не удалось собрать черновик.';
            return;
          }

          this.form = {
            ...this.emptyForm(),
            title: result.title || '',
            summary: result.summary || '',
            body: result.body || '',
            topic: this.draftTopic.trim(),
            city: this.draftCity.trim(),
            sourceName: result.sourceName || '',
            sourceUrl: result.sourceUrl || '',
            originalTitle: result.originalTitle || '',
            sourcePublishedAt: result.sourcePublishedAt || null,
            similarityPercent: result.similarityPercent ?? null,
            aiModel: result.aiModel || '',
            promptVersion: result.promptVersion || '',
            aiGenerated: true,
            authorName: 'Редакция Made4Love'
          };

          this.editingId = null;
          this.showForm = true;

          if (result.similarityPercent != null) {
            this.notice =
              `Черновик готов. Пересечение с источником: ${result.similarityPercent}% ` +
              `(допустимо до ${this.config.maxSimilarityPercent}%).`;
          } else {
            this.notice = 'Черновик готов. Проверьте текст перед публикацией.';
          }

          this.scrollToForm();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  publish(item: AdminNewsDto): void {
    if (
      !window.confirm(
        `Опубликовать «${item.title}»? Материал появится на сайте и в sitemap.`
      )
    ) {
      return;
    }

    this.busyId = item.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .publishItem(this.adminKey, item.id)
      .pipe(
        finalize(() => {
          this.busyId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = 'Материал опубликован.';
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  unpublish(item: AdminNewsDto): void {
    if (
      !window.confirm(
        `Снять «${item.title}» с публикации? Материал вернётся в черновики и исчезнет с сайта.`
      )
    ) {
      return;
    }

    this.busyId = item.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .unpublishItem(this.adminKey, item.id)
      .pipe(
        finalize(() => {
          this.busyId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = 'Материал снят с публикации.';
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  startReject(item: AdminNewsDto): void {
    this.rejectingId = item.id;
    this.rejectReason = '';
    this.error = null;
    this.notice = null;
  }

  cancelReject(): void {
    this.rejectingId = null;
    this.rejectReason = '';
  }

  confirmReject(item: AdminNewsDto): void {
    const reason = this.rejectReason.trim();
    if (!reason) {
      this.error = 'Укажите причину отклонения.';
      return;
    }

    this.busyId = item.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .rejectItem(this.adminKey, item.id, reason)
      .pipe(
        finalize(() => {
          this.busyId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.rejectingId = null;
          this.rejectReason = '';
          this.notice = 'Материал отклонён.';
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  deleteItem(item: AdminNewsDto): void {
    if (!window.confirm(`Удалить материал «${item.title}» без возможности восстановления?`)) {
      return;
    }

    this.busyId = item.id;
    this.error = null;
    this.notice = null;

    this.adminService
      .deleteItem(this.adminKey, item.id)
      .pipe(
        finalize(() => {
          this.busyId = null;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.notice = 'Материал удалён.';
          this.loadItems();
        },
        error: (error: unknown) => {
          this.error = this.describeError(error);
        }
      });
  }

  get sourcePublishedAtLocal(): string {
    const value = this.form.sourcePublishedAt;
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const pad = (part: number) => `${part}`.padStart(2, '0');

    return (
      `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
      `T${pad(date.getHours())}:${pad(date.getMinutes())}`
    );
  }

  set sourcePublishedAtLocal(value: string) {
    this.form.sourcePublishedAt = value ? new Date(value).toISOString() : null;
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Draft':
        return 'черновик';
      case 'Published':
        return 'опубликован';
      case 'Rejected':
        return 'отклонён';
      case 'Archived':
        return 'в архиве';
      default:
        return status;
    }
  }

  private scrollToForm(): void {
    setTimeout(() => {
      document.querySelector('.form-card')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 0);
  }

  private emptyForm(): NewsUpsert {
    return {
      title: '',
      summary: '',
      body: '',
      topic: '',
      tags: '',
      city: '',
      heroImageUrl: '',
      imageLicense: '',
      sourceName: '',
      sourceUrl: '',
      originalTitle: '',
      sourcePublishedAt: null,
      authorName: 'Редакция Made4Love',
      editorNotes: '',
      factsChecked: false,
      attributionChecked: false,
      imageRightsChecked: false,
      similarityPercent: null,
      aiModel: '',
      promptVersion: '',
      aiGenerated: false
    };
  }

  private toForm(item: AdminNewsDto): NewsUpsert {
    return {
      title: item.title,
      summary: item.summary ?? '',
      body: item.body,
      topic: item.topic ?? '',
      tags: item.tags ?? '',
      city: item.city ?? '',
      heroImageUrl: item.heroImageUrl ?? '',
      imageLicense: item.imageLicense ?? '',
      sourceName: item.sourceName ?? '',
      sourceUrl: item.sourceUrl ?? '',
      originalTitle: item.originalTitle ?? '',
      sourcePublishedAt: item.sourcePublishedAt ?? null,
      authorName: item.authorName ?? '',
      editorNotes: item.editorNotes ?? '',
      factsChecked: item.factsChecked,
      attributionChecked: item.attributionChecked,
      imageRightsChecked: item.imageRightsChecked,
      similarityPercent: item.similarityPercent ?? null,
      aiModel: item.aiModel ?? '',
      promptVersion: item.promptVersion ?? '',
      aiGenerated: item.aiGenerated
    };
  }

  private emptySourceForm(): JournalSourceUpsert {
    return {
      name: '',
      type: 'Rss',
      url: '',
      isEnabled: true,
      topic: '',
      city: '',
      defaultTags: '',
      syncIntervalMinutes: 180,
      maxItemsPerSync: 10,
      includePattern: '',
      aiDrafts: true
    };
  }

  private toSourceForm(source: JournalSourceDto): JournalSourceUpsert {
    return {
      name: source.name,
      type: source.type,
      url: source.url ?? '',
      isEnabled: source.isEnabled,
      topic: source.topic ?? '',
      city: source.city ?? '',
      defaultTags: source.defaultTags ?? '',
      syncIntervalMinutes: source.syncIntervalMinutes,
      maxItemsPerSync: source.maxItemsPerSync,
      includePattern: source.includePattern ?? '',
      aiDrafts: source.aiDrafts
    };
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 503) {
        return 'Админ-доступ не настроен на сервере. Задайте Journal:AdminKey (или Events:AdminKey).';
      }

      if (error.status === 401) {
        return 'Неверный админ-ключ.';
      }

      const message = error.error?.error;
      if (typeof message === 'string' && message.trim()) {
        return message;
      }

      return `Ошибка сервера (${error.status}). Попробуйте ещё раз.`;
    }

    return 'Не удалось выполнить запрос. Проверьте соединение.';
  }
}
