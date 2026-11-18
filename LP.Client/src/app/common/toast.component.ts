// components/toast/toast.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Всплывающее уведомление.
 *
 * `message` — это HTML-строка: поддерживаются `<br>`, `<b>`, `<a href>`, списки
 * и т. п. Angular прогоняет её через DomSanitizer, поэтому скрипты и опасные
 * атрибуты вырезаются автоматически.
 */
@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast" [class]="'toast-' + type" role="status" aria-live="polite">
      <span class="icon" aria-hidden="true">{{ icon }}</span>
      <div class="message" [innerHTML]="message"></div>
    </div>
  `,
  styles: [`
    :host {
      position: fixed;
      bottom: 20px;
      right: 20px;
      z-index: 10000;
      max-width: min(420px, calc(100vw - 40px));
      animation: slideIn 0.3s ease-out;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px 24px;
      border-radius: 12px;
      color: white;
      font-weight: 500;
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
      min-width: 300px;
    }
    .toast-success { background: linear-gradient(135deg, #557A94, #7396AE); }
    .toast-error { background: linear-gradient(135deg, #f44336, #da190b); }
    .toast-warning { background: linear-gradient(135deg, #ff9800, #f57c00); }
    .toast-info { background: linear-gradient(135deg, #2196f3, #1976d2); }
    .icon { font-size: 20px; flex: none; }
    .message {
      flex: 1;
      min-width: 0;
      line-height: 1.45;
      overflow-wrap: anywhere;
    }

    /* Стили для HTML внутри сообщения: узлы вставлены через innerHTML
       и не получают атрибут инкапсуляции Angular, поэтому ::ng-deep. */
    .message ::ng-deep a {
      color: #fff;
      font-weight: 600;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    .message ::ng-deep a:hover { opacity: 0.85; }
    .message ::ng-deep b,
    .message ::ng-deep strong { font-weight: 700; }
    .message ::ng-deep em { font-style: italic; }
    .message ::ng-deep small { font-size: 0.85em; opacity: 0.85; }
    .message ::ng-deep p { margin: 0; }
    .message ::ng-deep p + p { margin-top: 6px; }
    .message ::ng-deep ul,
    .message ::ng-deep ol { margin: 4px 0 0; padding-left: 18px; text-align: left; }
    .message ::ng-deep li + li { margin-top: 2px; }
    .message ::ng-deep code {
      padding: 1px 5px;
      border-radius: 4px;
      background: rgba(0, 0, 0, 0.22);
      font-size: 0.92em;
    }

    @keyframes slideIn {
      from { transform: translateX(100%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }

    @media (max-width: 480px) {
      :host {
        left: 16px;
        right: 16px;
        bottom: 16px;
        max-width: none;
      }
      .toast {
        min-width: 0;
        width: 100%;
        padding: 14px 18px;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      :host { animation: none; }
    }
  `]
})

export class ToastComponent {
  /** HTML-сообщение (санитайзится Angular). */
  @Input() message = '';
  @Input() type: 'success' | 'error' | 'warning' | 'info' | 'love' = 'success';

  get icon(): string {
    const icons = {
      success: '✔✓',
      error: '✕',
      warning: '⚠',
      info: 'ℹ',
      love: '💖'
    };
    return icons[this.type];
  }
}
