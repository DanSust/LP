import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Фирменный splash-экран Made4Love.
 *
 * Показывается поверх страницы, пока компонент загружает данные.
 * Пример:
 * ```html
 * <app-splash-screen [visible]="isLoading" text="Загружаем журнал" />
 * ```
 *
 * С кнопкой действия (например, экран взаимного лайка):
 * ```html
 * <app-splash-screen
 *   [visible]="!!matchId()"
 *   [showProgress]="false"
 *   text="У вас есть пара!"
 *   actionText="Начать диалог"
 *   (action)="startChat()" />
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

  /** Текст основной кнопки. Если задан, splash становится интерактивным. */
  actionText = input<string | null>(null);

  /** Текст второстепенной кнопки, которая закрывает splash. */
  dismissText = input<string | null>(null);

  /** Клик по основной кнопке (например, «Начать диалог»). */
  action = output<void>();

  /** Клик по второстепенной кнопке. */
  dismiss = output<void>();

  /**
   * Показывать splash внутри родителя (например, внутри app-root),
   * а не поверх всего окна. В этом режиме он занимает ширину родителя,
   * поэтому фон и коллаж по бокам остаются видимыми.
   */
  inline = input<boolean>(false);
}
