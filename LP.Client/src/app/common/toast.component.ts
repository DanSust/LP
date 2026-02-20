// components/toast/toast.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast" [class]="'toast-' + type">
      <span class="icon">{{ icon }}</span>
      <span class="message">{{ message }}</span>
    </div>
  `,
  styles: [`
    :host {
      position: fixed;
      bottom: 20px;
      right: 20px;
      z-index: 10000;
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
    .icon { font-size: 20px; }
    
    @keyframes slideIn {
      from { transform: translateX(100%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }
  `]
})

export class ToastComponent {
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
