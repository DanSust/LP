import { Directive, OnDestroy, OnInit, signal } from '@angular/core';
import { Observable, Subject, firstValueFrom, isObservable, takeUntil } from 'rxjs';

/** Что может вернуть LoadData(): ничего, Promise или Observable. */
export type LoadDataResult = void | Promise<unknown> | Observable<unknown>;

/**
 * Базовый класс для компонентов, которые загружают данные с сервера.
 *
 * Наследник реализует только `LoadData()` — остальное берёт на себя база:
 * - вызывает загрузку в `ngOnInit`;
 * - хранит флаги `isLoading` / `isLoaded` и ошибку `loadError`;
 * - умеет работать с Promise и Observable;
 * - отменяет Observable при уничтожении компонента;
 * - не запускает повторный запрос, пока идёт текущий.
 *
 * Пример:
 * ```ts
 * export class UserViewComponent extends BaseComponent {
 *   profile: Profile | null = null;
 *
 *   protected override async LoadData(): Promise<void> {
 *     this.profile = await firstValueFrom(this.http.get<Profile>('/api/profile'));
 *   }
 * }
 * ```
 *
 * Важно: не переопределяйте `ngOnInit()` и `ngOnDestroy()` — используйте
 * хуки `onInit()` и `onDestroy()`, иначе загрузка и очистка не выполнятся.
 */
@Directive()
export abstract class BaseComponent implements OnInit, OnDestroy {
  protected readonly destroy$ = new Subject<void>();

  /**
   * Запускать LoadData() автоматически в ngOnInit.
   * Отключите, если компонент сам решает, когда загружать данные
   * (например, по параметрам маршрута).
   */
  protected readonly autoLoad: boolean = true;

  private readonly loadingState = signal(false);
  private readonly loadedState = signal(false);
  private readonly errorState = signal<unknown>(null);

  private loadPromise: Promise<void> | null = null;

  /** true, пока выполняется загрузка данных. */
  get isLoading(): boolean {
    return this.loadingState();
  }

  /** true, если последняя загрузка завершилась успешно. */
  get isLoaded(): boolean {
    return this.loadedState();
  }

  /** Ошибка последней загрузки или null. */
  get loadError(): unknown {
    return this.errorState();
  }

  ngOnInit(): void {
    this.onInit();

    if (this.autoLoad) {
      void this.load();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.onDestroy();
  }

  /** Синхронная инициализация до загрузки данных. */
  protected onInit(): void {}

  /** Освобождение ресурсов компонента. */
  protected onDestroy(): void {}

  /** Вызывается перед каждой загрузкой — удобно сбрасывать локальные ошибки. */
  protected onLoadStart(): void {}

  /** Загрузка данных компонента. Реализуется наследником. */
  protected abstract LoadData(): LoadDataResult;

  /** Обработка ошибки LoadData(). По умолчанию пишет в консоль. */
  protected onLoadError(error: unknown): void {
    console.error(`[${this.constructor.name}] LoadData failed`, error);
  }

  /** Запускает LoadData() и управляет флагами. */
  load(): Promise<void> {
    if (this.loadPromise) {
      return this.loadPromise;
    }

    this.onLoadStart();
    this.loadingState.set(true);
    this.loadedState.set(false);
    this.errorState.set(null);

    this.loadPromise = this.runLoad().finally(() => {
      this.loadingState.set(false);
      this.loadPromise = null;
    });

    return this.loadPromise;
  }

  /** Повторная загрузка данных. */
  reload(): Promise<void> {
    return this.load();
  }

  private async runLoad(): Promise<void> {
    try {
      const result = this.LoadData();

      if (isObservable(result)) {
        await firstValueFrom(result.pipe(takeUntil(this.destroy$)));
      } else {
        await result;
      }

      this.loadedState.set(true);
    } catch (error) {
      // Компонент уничтожен — это отмена подписки, а не ошибка загрузки.
      if (this.destroy$.isStopped) {
        return;
      }

      this.errorState.set(error);
      this.onLoadError(error);
    }
  }
}
