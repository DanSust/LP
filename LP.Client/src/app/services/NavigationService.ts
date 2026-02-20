import { Injectable } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class NavigationService {
  private history: string[] = [];

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.history.push(event.url);
        // Храним только последние 10 URL
        if (this.history.length > 10) {
          this.history.shift();
        }
      });
  }

  getHistory(): string[] {
    return [...this.history];
  }

  getPreviousUrl(): string | null {
    return this.history.length > 1 ? this.history[this.history.length - 2] : null;
  }

  getCurrentUrl(): string {
    return this.router.url;
  }

  // Проверить, пришли ли с конкретного URL
  cameFrom(urlPattern: string | RegExp): boolean {
    const prev = this.getPreviousUrl();
    if (!prev) return false;

    if (typeof urlPattern === 'string') {
      return prev.includes(urlPattern);
    }
    return urlPattern.test(prev);
  }
}
