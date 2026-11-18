// services/custom-toast.service.ts
import { Injectable, ApplicationRef, createComponent, EnvironmentInjector, inject } from '@angular/core';
import { ToastComponent } from './toast.component';

export enum ToastType {
  Success = 'success',
  Error = 'error',
  Warning = 'warning',
  Info = 'info',
  Love = 'love'
}

/**
 * Сервис всплывающих уведомлений.
 *
 * Все методы принимают не только текст, но и HTML (`<b>`, `<br>`,
 * `<a href="/auth">Войти</a>` и т. п.). Строка рендерится через
 * `[innerHTML]` и проходит санитайзер Angular, поэтому скрипты и опасные
 * атрибуты вырезаются. Если нужно показать «сырой» HTML без санитайзера,
 * используйте `bypassSecurityTrustHtml` осознанно — здесь такого API нет.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private appRef = inject(ApplicationRef);
  private injector = inject(EnvironmentInjector);

  /**
   * @param message HTML или обычный текст.
   * @param duration миллисекунды; `0` — не закрывать автоматически.
   */
  show(message: string, type: ToastType = ToastType.Success, duration = 3000): void {
    const toastRef = createComponent(ToastComponent, {
      environmentInjector: this.injector
    });

    toastRef.instance.message = message;
    toastRef.instance.type = type;

    const host = toastRef.location.nativeElement as HTMLElement;

    // Добавляем в DOM
    document.body.appendChild(host);
    this.appRef.attachView(toastRef.hostView);

    let timer: ReturnType<typeof setTimeout> | undefined;
    let remaining = duration;
    let startedAt = 0;

    const dismiss = () => {
      if (timer !== undefined) {
        clearTimeout(timer);
        timer = undefined;
      }
      host.removeEventListener('mouseenter', pause);
      host.removeEventListener('mouseleave', resume);
      this.appRef.detachView(toastRef.hostView);
      toastRef.destroy();
    };

    const startTimer = () => {
      if (remaining <= 0) {
        return;
      }
      startedAt = Date.now();
      timer = setTimeout(dismiss, remaining);
    };

    // Пауза при наведении: успеваешь прочитать и кликнуть по ссылке.
    const pause = () => {
      if (timer === undefined) {
        return;
      }
      clearTimeout(timer);
      timer = undefined;
      remaining -= Date.now() - startedAt;
      if (remaining < 500) {
        remaining = 500;
      }
    };

    const resume = () => startTimer();

    host.addEventListener('mouseenter', pause);
    host.addEventListener('mouseleave', resume);

    startTimer();
  }

  // Удобные методы
  success(message: string, duration = 3000): void {
    this.show(message, ToastType.Success, duration);
  }

  error(message: string, duration = 5000): void {
    this.show(message, ToastType.Error, duration);
  }

  warning(message: string, duration = 4000): void {
    this.show(message, ToastType.Warning, duration);
  }

  info(message: string, duration = 3000): void {
    this.show(message, ToastType.Info, duration);
  }
}
