// services/seo.service.ts
import { Injectable, inject } from '@angular/core';
import { Title, Meta } from '@angular/platform-browser';
import { Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class SeoService {
  private title = inject(Title);
  private meta = inject(Meta);
  private router = inject(Router);

  setMeta(title: string, description: string, image?: string, url?: string) {
    this.title.setTitle(`${title} | made4love.ru`);
    this.meta.updateTag({ name: 'description', content: description });

    // Open Graph для соцсетей
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:image', content: image || '/assets/og-default.jpg' });
    this.meta.updateTag({ property: 'og:url', content: url || this.router.url });
    this.meta.updateTag({ property: 'og:type', content: 'website' });    
  }
}
