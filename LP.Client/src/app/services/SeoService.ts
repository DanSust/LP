import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export interface SeoConfig {
  title?: string;
  description?: string;
  image?: string;
  imageAlt?: string;
  url?: string;
  robots?: string;
}

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly document = inject(DOCUMENT);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);

  private readonly defaultTitle = 'Made4Love — знакомства для серьёзных отношений';
  private readonly defaultDescription = 'Made4Love — знакомства для серьёзных отношений. Проверка совместимости, живой чат и денежный приз за свадьбу. Создайте анкету бесплатно.';
  private readonly defaultImage = 'https://made4love.ru/assets/social-preview.jpg';
  private readonly defaultImageAlt = 'Made4Love — знакомства для серьёзных отношений';

  updateMetaTags(config: SeoConfig = {}): void {
    const title = this.formatTitle(config.title);
    const description = config.description || this.defaultDescription;
    const canonicalUrl = new URL(config.url || '/', 'https://made4love.ru').href;
    const imageUrl = new URL(config.image || this.defaultImage, 'https://made4love.ru').href;
    const imageAlt = config.imageAlt || this.defaultImageAlt;

    this.document.documentElement.lang = 'ru';

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    this.metaService.updateTag({ name: 'robots', content: config.robots || 'index,follow' });

    this.metaService.updateTag({ property: 'og:type', content: 'website' });
    this.metaService.updateTag({ property: 'og:site_name', content: 'Made4Love' });
    this.metaService.updateTag({ property: 'og:locale', content: 'ru_RU' });
    this.metaService.updateTag({ property: 'og:title', content: title });
    this.metaService.updateTag({ property: 'og:description', content: description });
    this.metaService.updateTag({ property: 'og:image', content: imageUrl });
    this.metaService.updateTag({ property: 'og:image:type', content: 'image/jpeg' });
    this.metaService.updateTag({ property: 'og:image:width', content: '1200' });
    this.metaService.updateTag({ property: 'og:image:height', content: '630' });
    this.metaService.updateTag({ property: 'og:image:alt', content: imageAlt });
    this.metaService.updateTag({ property: 'og:url', content: canonicalUrl });
    this.metaService.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.metaService.updateTag({ name: 'twitter:title', content: title });
    this.metaService.updateTag({ name: 'twitter:description', content: description });
    this.metaService.updateTag({ name: 'twitter:image', content: imageUrl });
    this.metaService.updateTag({ name: 'twitter:image:alt', content: imageAlt });

    let canonical = this.document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!canonical) {
      canonical = this.document.createElement('link');
      canonical.rel = 'canonical';
      this.document.head.appendChild(canonical);
    }
    canonical.href = canonicalUrl;
  }

  private formatTitle(title?: string): string {
    if (!title) {
      return this.defaultTitle;
    }

    return /made4love/i.test(title) ? title : `${title} | Made4Love`;
  }
}
