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

@Injectable({ providedIn: 'root' })
export class ToastService {
  private appRef = inject(ApplicationRef);
  private injector = inject(EnvironmentInjector);

  show(message: string, type: ToastType = ToastType.Success, duration = 3000): void {
    const toastRef = createComponent(ToastComponent, {
      environmentInjector: this.injector
    });

    toastRef.instance.message = message;
    toastRef.instance.type = type;

    // Добавляем в DOM
    document.body.appendChild(toastRef.location.nativeElement);
    this.appRef.attachView(toastRef.hostView);

    // Авто-удаление
    setTimeout(() => {
      this.appRef.detachView(toastRef.hostView);
      toastRef.destroy();
    }, duration);
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
