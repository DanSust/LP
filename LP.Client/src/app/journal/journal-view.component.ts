import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Observable, tap } from 'rxjs';
import { JournalService, PublicNewsListItem } from '../services/JournalService';
import { SeoService } from '../services/SeoService';
import { BaseComponent } from '../common/base.component';
import { SplashScreenComponent } from '../splash/splash.component';

@Component({
  selector: 'app-journal-view',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, SplashScreenComponent],
  templateUrl: './journal-view.component.html',
  styleUrls: ['./journal-view.component.scss']
})
export class JournalViewComponent extends BaseComponent {
  private readonly journalService = inject(JournalService);
  private readonly seoService = inject(SeoService);

  items: PublicNewsListItem[] = [];
  error: string | null = null;

  protected override onInit(): void {
    scroll(0, 0);
    this.updateSeo();
  }

  protected override onDestroy(): void {
    this.seoService.removeJsonLd('journal-list-jsonld');
  }

  protected override onLoadStart(): void {
    this.error = null;
  }

  protected override onLoadError(error: unknown): void {
    console.error('Failed to load journal', error);
    this.error = 'Не удалось загрузить журнал. Попробуйте обновить страницу.';
  }

  retry(): void {
    void this.reload();
  }

  protected LoadData(): Observable<PublicNewsListItem[]> {
    return this.journalService.list().pipe(
      tap((items) => {
        this.items = items;
        this.updateSeo();
      })
    );
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
