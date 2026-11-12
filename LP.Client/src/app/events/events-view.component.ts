import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';
import { EventService, PublicEventListItem } from '../services/EventService';
import { SeoService } from '../services/SeoService';

@Component({
  selector: 'app-events-view',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './events-view.component.html',
  styleUrls: ['./events-view.component.scss']
})
export class EventsViewComponent implements OnInit, OnDestroy {
  private readonly eventService = inject(EventService);
  private readonly seoService = inject(SeoService);
  private readonly destroy$ = new Subject<void>();

  events: PublicEventListItem[] = [];
  isLoading = true;
  error: string | null = null;

  ngOnInit(): void {
    scroll(0, 0);
    this.updateSeo();
    this.loadEvents();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.seoService.removeJsonLd('events-list-jsonld');
  }

  retry(): void {
    this.loadEvents();
  }

  private loadEvents(): void {
    this.isLoading = true;
    this.error = null;

    this.eventService
      .list({ pageSize: 100 })
      .pipe(
        catchError((error: unknown) => {
          console.error('Failed to load public events', error);
          this.error = 'Не удалось загрузить события. Попробуйте обновить страницу.';
          return of([] as PublicEventListItem[]);
        }),
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((events) => {
        this.events = events;
        this.updateSeo();
      });
  }

  private updateSeo(): void {
    const title = 'События и встречи — Made4Love';
    const description =
      'Афиша Made4Love: реальные встречи, лекции и вечеринки для тех, кто ищет серьёзные отношения.';

    this.seoService.updateMetaTags({
      title,
      description,
      url: '/events',
      type: 'website',
      robots: 'index,follow'
    });

    this.seoService.setJsonLd('events-list-jsonld', {
      '@context': 'https://schema.org',
      '@type': 'ItemList',
      name: title,
      url: this.seoService.absoluteUrl('/events'),
      numberOfItems: this.events.length,
      itemListElement: this.events.slice(0, 20).map((event, index) => ({
        '@type': 'ListItem',
        position: index + 1,
        url: this.seoService.absoluteUrl(`/events/${event.slug}`),
        name: event.title
      }))
    });
  }
}
