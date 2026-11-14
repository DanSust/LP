import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, catchError, finalize, map, of, switchMap, takeUntil, tap } from 'rxjs';
import { JournalService, PublicNewsDetail } from '../services/JournalService';
import { SeoService } from '../services/SeoService';
import { SplashScreenComponent } from '../splash/splash.component';

@Component({
  selector: 'app-journal-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, SplashScreenComponent],
  templateUrl: './journal-detail.component.html',
  styleUrls: ['../events/event-detail.component.scss', './journal-detail.component.scss']
})
export class JournalDetailComponent implements OnInit, OnDestroy {
  private readonly journalService = inject(JournalService);
  private readonly seoService = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();

  item: PublicNewsDetail | null = null;
  isLoading = true;
  notFound = false;
  error: string | null = null;

  ngOnInit(): void {
    scroll(0, 0);

    this.route.paramMap
      .pipe(
        map((params) => params.get('slug')?.trim() ?? ''),
        tap((slug) => {
          this.isLoading = true;
          this.notFound = false;
          this.error = null;
          this.item = null;

          this.seoService.updateMetaTags({
            title: 'Журнал — Made4Love',
            description: 'Материалы журнала Made4Love: отношения, города и события.',
            url: `/journal/${slug}`,
            robots: 'noindex,follow'
          });
        }),
        switchMap((slug) => {
          if (!slug) {
            this.isLoading = false;
            this.notFound = true;
            return of(null);
          }

          return this.journalService.getBySlug(slug).pipe(
            catchError((error: unknown) => {
              if (error instanceof HttpErrorResponse && error.status === 404) {
                this.notFound = true;
              } else {
                console.error('Failed to load journal article', error);
                this.error = 'Не удалось загрузить материал. Попробуйте обновить страницу.';
              }

              return of(null);
            }),
            finalize(() => {
              this.isLoading = false;
            })
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((item) => {
        this.item = item;

        if (item) {
          this.applySeo(item);
        } else {
          this.applyNotFoundSeo();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.seoService.removeJsonLd('journal-article-jsonld');
  }

  get paragraphs(): string[] {
    if (!this.item?.body) {
      return [];
    }

    return this.item.body
      .split(/\n{2,}/)
      .map((paragraph) => paragraph.trim())
      .filter((paragraph) => paragraph.length > 0);
  }

  get readingMinutes(): number {
    const words = this.item?.body
      ? this.item.body.split(/\s+/).filter((word) => word.length > 0).length
      : 0;

    return Math.max(1, Math.round(words / 180));
  }

  retry(): void {
    window.location.reload();
  }

  private applySeo(item: PublicNewsDetail): void {
    const path = `/journal/${item.slug}`;
    const description =
      item.summary ||
      this.truncate(item.body, 300) ||
      `Материал журнала Made4Love «${item.title}».`;

    this.seoService.updateMetaTags({
      title: `${item.title} — Made4Love`,
      description,
      url: path,
      type: 'article',
      image: item.heroImageUrl || undefined,
      imageAlt: item.title,
      robots: 'index,follow'
    });

    const jsonLd: Record<string, unknown> = {
      '@context': 'https://schema.org',
      '@type': 'NewsArticle',
      headline: item.title,
      description,
      url: this.seoService.absoluteUrl(path),
      mainEntityOfPage: this.seoService.absoluteUrl(path),
      inLanguage: 'ru-RU',
      author: {
        '@type': 'Organization',
        name: item.authorName || 'Редакция Made4Love'
      },
      publisher: {
        '@type': 'Organization',
        name: 'Made4Love',
        url: this.seoService.absoluteUrl('/')
      },
      datePublished: item.publishedAt || undefined,
      dateModified: item.updatedAt || item.publishedAt || undefined
    };

    if (item.heroImageUrl) {
      jsonLd['image'] = [this.seoService.absoluteUrl(item.heroImageUrl)];
    }

    if (item.sourceUrl) {
      jsonLd['isBasedOn'] = item.sourceUrl;
      jsonLd['citation'] = item.sourceUrl;
    }

    this.seoService.setJsonLd('journal-article-jsonld', jsonLd);
  }

  private applyNotFoundSeo(): void {
    this.seoService.updateMetaTags({
      title: 'Материал не найден — Made4Love',
      description: 'Материал журнала не найден или ещё не опубликован.',
      url: '/journal',
      robots: 'noindex,follow'
    });
  }

  private truncate(value: string, maxLength: number): string {
    const normalized = value.replace(/\s+/g, ' ').trim();
    return normalized.length <= maxLength
      ? normalized
      : `${normalized.slice(0, maxLength).trimEnd()}…`;
  }
}
