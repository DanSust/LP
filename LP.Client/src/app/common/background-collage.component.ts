// background-collage.component.ts
import { Component, ElementRef, Inject, OnInit, PLATFORM_ID, Renderer2 } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { API_BASE_URL } from './../app.config';

@Component({
  selector: 'app-back-collage',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './background-collage.component.html',
  styleUrls: ['./background-collage.component.scss']
})
export class BackCollageComponent implements OnInit {
  photoUrls: string[] = [];

  constructor(
    @Inject(API_BASE_URL) public baseUrl: string,
    @Inject(PLATFORM_ID) private platformId: Object,
    private elementRef: ElementRef<HTMLElement>,
    private renderer: Renderer2
  ) { }

  ngOnInit(): void {
    // Коллаж должен жить на уровне body, перед app-root, чтобы быть фоном
    // всей страницы, а не только центральной колонки приложения.
    this.moveToBody();

    // 1. Сначала проверяем, нужно ли ВООБЩЕ что-то делать
    if (!this.shouldLoadImages()) {
      this.photoUrls = []; // Гарантируем пустоту
      return;
    }
    // Генерируем массив URL и перемешиваем
    const urls = Array.from({ length: 12 }, (_, i) =>
      `${this.baseUrl}/Photos/back/${i + 1}.jpg`
      //`/Photos/back/${i + 1}.jpg`
    );
    this.photoUrls = this.shuffleArray(urls);
  }

  /** Переносит хост-элемент компонента прямо в <body> перед <app-root>. */
  private moveToBody(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const host = this.elementRef.nativeElement;
    const appRoot = document.querySelector('app-root');
    if (!appRoot || host.parentElement === document.body) {
      return;
    }

    this.renderer.insertBefore(document.body, host, appRoot);
  }

  private shouldLoadImages(): boolean {
    // Проверяем, что мы в браузере
    if (!isPlatformBrowser(this.platformId)) {
      return false;
    }

    // Проверяем ширину экрана
    return window.innerWidth > 768;
  }

  private shuffleArray<T>(array: T[]): T[] {
    // Fisher-Yates shuffle algorithm
    const shuffled = [...array];
    for (let i = shuffled.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    return shuffled;
  }
}
