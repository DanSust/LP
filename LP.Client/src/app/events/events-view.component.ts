import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Observable, tap } from 'rxjs';
import { EventService, PublicEventListItem } from '../services/EventService';
import { SeoService } from '../services/SeoService';
import { BaseComponent } from '../common/base.component';
import { SplashScreenComponent } from '../splash/splash.component';

@Component({
  selector: 'app-events-view',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, SplashScreenComponent],
  templateUrl: './events-view.component.html',
  styleUrls: ['./events-view.component.scss']
})
export class EventsViewComponent extends BaseComponent {
  private readonly eventService = inject(EventService);
  private readonly seoService = inject(SeoService);

  events: PublicEventListItem[] = [];
  error: string | null = null;

  protected override onInit(): void {
    scroll(0, 0);
    this.updateSeo();
  }

  protected override onDestroy(): void {
    this.seoService.removeJsonLd('events-list-jsonld');
  }

  protected override onLoadStart(): void {
    this.error = null;
  }

  protected override onLoadError(error: unknown): void {
    console.error('Failed to load public events', error);
    this.error = 'Не удалось загрузить события. Попробуйте обновить страницу.';
  }

  retry(): void {
    void this.reload();
  }

  protected LoadData(): Observable<PublicEventListItem[]> {
    return this.eventService.list({ pageSize: 100 }).pipe(
      tap((events) => {
        this.events = events;
        this.updateSeo();
      })
    );
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
