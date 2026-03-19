import { Injectable } from '@angular/core';
import { Title, Meta } from '@angular/platform-browser';

@Injectable({
  providedIn: 'root'
})
export class SeoService {
  constructor(private titleService: Title, private metaService: Meta) { }

  updateMetaTags(config: { title?: string, description?: string, image?: string, url?: string }) {
    const defaultTitle = 'made4love.ru — Найди свое синхронное сердцебиение';
    const defaultDesc = 'Уникальный сервис знакомств, где технологии помогают найти настоящую любовь. Денежные призы за свадьбу и общение в реальном времени.';

    const title = config.title ? `${config.title} | made4love` : defaultTitle;
    this.titleService.setTitle(title);

    // Стандартные мета-теги
    this.metaService.updateTag({ name: 'description', content: config.description || defaultDesc });
    this.metaService.updateTag({ name: 'keywords', content: 'знакомства, найти любовь, синхронное сердцебиение, бонусы за свадьбу, серьезные отношения, сделано для любви' });

    // Open Graph (для Telegram, VK, WhatsApp)
    this.metaService.updateTag({ property: 'og:title', content: title });
    this.metaService.updateTag({ property: 'og:description', content: config.description || defaultDesc });
    this.metaService.updateTag({ property: 'og:image', content: config.image || 'assets/promo-banner.png' }); // Укажи путь к логотипу
    this.metaService.updateTag({ property: 'og:url', content: `https://made4love.ru${config.url || ''}` });
    this.metaService.updateTag({ property: 'og:type', content: 'website' });
  }
}
