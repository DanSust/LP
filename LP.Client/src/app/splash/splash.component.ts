import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Фирменный splash-экран Made4Love.
 *
 * Показывается поверх страницы, пока компонент загружает данные.
 * Пример:
 * ```html
 * <app-splash-screen [visible]="isLoading" text="Загружаем журнал" />
 * ```
 */
@Component({
  selector: 'app-splash-screen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './splash.component.html',
  styleUrl: './splash.component.scss'
})
export class SplashScreenComponent {
  /** Показывать ли splash. При false экран плавно исчезает. */
  visible = input.required<boolean>();

  /** Прогресс 0–100. Если не задан — показывается бегущая полоса. */
  progress = input<number | null>(null);

  /** Показывать ли полосу прогресса. */
  showProgress = input<boolean>(true);

  /** Подпись под логотипом. */
  text = input<string>('Загружаем данные');

  /**
   * Показывать splash внутри родителя (например, внутри app-root),
   * а не поверх всего окна. В этом режиме он занимает ширину родителя,
   * поэтому фон и коллаж по бокам остаются видимыми.
   */
  inline = input<boolean>(false);
}
