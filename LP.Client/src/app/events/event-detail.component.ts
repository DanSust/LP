import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, catchError, finalize, map, of, switchMap, takeUntil, tap } from 'rxjs';
import { EventService, PublicEventDetail } from '../services/EventService';
import { SeoService } from '../services/SeoService';

@Component({
  selector: 'app-event-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './event-detail.component.html',
  styleUrls: ['./event-detail.component.scss']
})
export class EventDetailComponent implements OnInit, OnDestroy {
  private readonly eventService = inject(EventService);
  private readonly seoService = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();

  event: PublicEventDetail | null = null;
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
          this.event = null;

          this.seoService.updateMetaTags({
            title: 'Событие — Made4Love',
            description: 'Подробности события Made4Love: дата, место и описание встречи.',
            url: `/events/${slug}`,
            robots: 'noindex,follow'
          });
        }),
        switchMap((slug) => {
          if (!slug) {
            this.isLoading = false;
            this.notFound = true;
            return of(null);
          }

          return this.eventService.getBySlug(slug).pipe(
            catchError((error: unknown) => {
              if (error instanceof HttpErrorResponse && error.status === 404) {
                this.notFound = true;
              } else {
                console.error('Failed to load event', error);
                this.error = 'Не удалось загрузить событие. Попробуйте обновить страницу.';
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
      .subscribe((event) => {
        this.event = event;

        if (event) {
          this.applyEventSeo(event);
        } else {
          this.applyNotFoundSeo();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.seoService.removeJsonLd('event-jsonld');
  }

  sameDay(first: string, second: string): boolean {
    const a = new Date(first);
    const b = new Date(second);
    return (
      a.getFullYear() === b.getFullYear() &&
      a.getMonth() === b.getMonth() &&
      a.getDate() === b.getDate()
    );
  }

  retry(): void {
    window.location.reload();
  }

  private applyEventSeo(event: PublicEventDetail): void {
    const path = `/events/${event.slug}`;
    const description =
      event.summary ||
      this.truncate(event.description || '', 300) ||
      `Событие «${event.title}» на Made4Love: дата, место и подробности встречи.`;

    this.seoService.updateMetaTags({
      title: `${event.title} — Made4Love`,
      description,
      url: path,
      type: 'article',
      image: event.imageUrl || undefined,
      imageAlt: event.title,
      robots: 'index,follow'
    });

    const jsonLd: Record<string, unknown> = {
      '@context': 'https://schema.org',
      '@type': 'Event',
      name: event.title,
      description,
      startDate: event.startsAt,
      eventStatus: 'https://schema.org/EventScheduled',
      eventAttendanceMode:
        event.city || event.venue || event.address
          ? 'https://schema.org/OfflineEventAttendanceMode'
          : 'https://schema.org/OnlineEventAttendanceMode',
      url: this.seoService.absoluteUrl(path),
      organizer: {
        '@type': 'Organization',
        name: 'Made4Love',
        url: 'https://made4love.ru/'
      }
    };

    if (event.endsAt) {
      jsonLd['endDate'] = event.endsAt;
    }

    if (event.imageUrl) {
      jsonLd['image'] = [this.seoService.absoluteUrl(event.imageUrl)];
    }

    if (event.city || event.venue || event.address) {
      jsonLd['location'] = {
        '@type': 'Place',
        name: event.venue || event.city || 'Место уточняется',
        address: {
          '@type': 'PostalAddress',
          addressLocality: event.city ?? undefined,
          streetAddress: event.address ?? undefined,
          addressCountry: 'RU'
        }
      };
    }

    this.seoService.setJsonLd('event-jsonld', jsonLd);
  }

  private applyNotFoundSeo(): void {
    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    this.seoService.updateMetaTags({
      title: 'Событие не найдено — Made4Love',
      description: 'Запрошенное событие не найдено или ещё не опубликовано.',
      url: `/events/${slug}`,
      robots: 'noindex,nofollow'
    });
    this.seoService.removeJsonLd('event-jsonld');
  }

  private truncate(value: string, maxLength: number): string {
    const text = value.replace(/\s+/g, ' ').trim();
    if (text.length <= maxLength) {
      return text;
    }

    const cut = text.slice(0, maxLength);
    const lastSpace = cut.lastIndexOf(' ');
    return `${(lastSpace > maxLength / 2 ? cut.slice(0, lastSpace) : cut).trimEnd()}…`;
  }
}
