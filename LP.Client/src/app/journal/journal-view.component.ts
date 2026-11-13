import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';
import { JournalService, PublicNewsListItem } from '../services/JournalService';
import { SeoService } from '../services/SeoService';

@Component({
  selector: 'app-journal-view',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './journal-view.component.html',
  styleUrls: ['./journal-view.component.scss']
})
export class JournalViewComponent implements OnInit, OnDestroy {
  private readonly journalService = inject(JournalService);
  private readonly seoService = inject(SeoService);
  private readonly destroy$ = new Subject<void>();

  items: PublicNewsListItem[] = [];
  isLoading = true;
  error: string | null = null;

  ngOnInit(): void {
    scroll(0, 0);
    this.updateSeo();
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.seoService.removeJsonLd('journal-list-jsonld');
  }

  retry(): void {
    this.load();
  }

  private load(): void {
    this.isLoading = true;
    this.error = null;

    this.journalService
      .list()
      .pipe(
        catchError((error: unknown) => {
          console.error('Failed to load journal', error);
          this.error = 'Не удалось загрузить журнал. Попробуйте обновить страницу.';
          return of([] as PublicNewsListItem[]);
        }),
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((items) => {
        this.items = items;
        this.updateSeo();
      });
  }

  private updateSeo(): void {
    const title = 'Журнал Made4Love — отношения, города и события';
    const description =
      'Журнал Made4Love: разборы, городские подборки и материалы о знакомствах и отношениях. Редакция проверяет каждый текст и указывает источник.';

    this.seoService.updateMetaTags({
      title,
      description,
      url: '/journal',
      type: 'website',
      robots: 'index,follow'
    });

    this.seoService.setJsonLd('journal-list-jsonld', {
      '@context': 'https://schema.org',
      '@type': 'CollectionPage',
      name: title,
      url: this.seoService.absoluteUrl('/journal'),
      inLanguage: 'ru-RU',
      isPartOf: {
        '@type': 'WebSite',
        name: 'Made4Love',
        url: this.seoService.absoluteUrl('/')
      },
      mainEntity: {
        '@type': 'ItemList',
        numberOfItems: this.items.length,
        itemListElement: this.items.slice(0, 20).map((item, index) => ({
          '@type': 'ListItem',
          position: index + 1,
          url: this.seoService.absoluteUrl(`/journal/${item.slug}`),
          name: item.title
        }))
      }
    });
  }
}
