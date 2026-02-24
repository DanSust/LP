// loadable.component.ts
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

// Состояния загрузки
export type LoadingState = 'idle' | 'loading' | 'success' | 'error';

// Результат загрузки
export interface LoadResult<T> {
  data?: T;
  error?: Error;
}

@Component({
  template: '', // пустой — базовый класс
  standalone: true,
  imports: [CommonModule]
})
export abstract class LoadableComponent<T = unknown> implements OnInit {
  // Сигналы для реактивности
  protected state = signal<LoadingState>('idle');
  protected progress = signal(0);
  protected data = signal<T | undefined>(undefined);
  protected error = signal<Error | undefined>(undefined);

  // Публичные readonly сигналы
  readonly loadingState = this.state.asReadonly();
  readonly progressValue = this.progress.asReadonly();
  readonly loadedData = this.data.asReadonly();
  readonly loadError = this.error.asReadonly();

  // Флаги для удобства в шаблоне
  get isLoading() { return this.state() === 'loading'; }
  get isSuccess() { return this.state() === 'success'; }
  get isError() { return this.state() === 'error'; }
  get isIdle() { return this.state() === 'idle'; }

  ngOnInit() {
    this.load();
  }

  // Абстрактный метод — каждый компонент реализует свою загрузку
  protected abstract fetchData(): Promise<T>;

  // Основной метод загрузки
  async load(): Promise<void> {
    this.state.set('loading');
    this.progress.set(0);
    this.error.set(undefined);

    try {
      // Имитация прогресса
      const progressInterval = this.simulateProgress();

      const result = await this.fetchData();

      clearInterval(progressInterval);
      this.progress.set(100);

      // Небольшая задержка для плавности
      await this.delay(200);

      this.data.set(result);
      this.state.set('success');

      this.onLoadSuccess(result);

    } catch (err) {
      this.state.set('error');
      this.error.set(err instanceof Error ? err : new Error(String(err)));
      this.onLoadError(this.error()!);
    }
  }

  // Перезагрузка данных
  reload(): void {
    this.load();
  }

  // Хуки для переопределения в наследниках
  protected onLoadSuccess(data: T): void { }
  protected onLoadError(error: Error): void { }

  // Утилиты
  private simulateProgress(): ReturnType<typeof setInterval> {
    return setInterval(() => {
      const current = this.progress();
      if (current < 90) {
        this.progress.set(current + Math.random() * 15);
      }
    }, 200);
  }

  private delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
  }
}
