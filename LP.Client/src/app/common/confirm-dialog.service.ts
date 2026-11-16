import {
  ApplicationRef,
  EnvironmentInjector,
  Injectable,
  createComponent,
  inject
} from '@angular/core';
import {
  ConfirmDialogComponent,
  ConfirmDialogOptions,
  ConfirmDialogResult
} from './confirm-dialog.component';

/**
 * Показывает модальное подтверждение в стиле сайта вместо window.confirm.
 *
 * const ok = await this.dialog.confirm({ title: 'Удалить чат?' });
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly appRef = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);

  /** Возвращает true, если пользователь подтвердил действие. */
  async confirm(options: ConfirmDialogOptions): Promise<boolean> {
    const result = await this.ask(options);
    return result.confirmed;
  }

  /** Как confirm, но дополнительно возвращает введённый комментарий. */
  ask(options: ConfirmDialogOptions): Promise<ConfirmDialogResult> {
    const ref = createComponent(ConfirmDialogComponent, {
      environmentInjector: this.injector
    });

    ref.instance.apply(options);

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const previouslyFocused = document.activeElement as HTMLElement | null;

    document.body.appendChild(ref.location.nativeElement);
    this.appRef.attachView(ref.hostView);
    ref.changeDetectorRef.detectChanges();
    ref.instance.focusInitial();

    return new Promise<ConfirmDialogResult>(resolve => {
      ref.instance.closed.subscribe((result: ConfirmDialogResult) => {
        document.body.style.overflow = previousOverflow;
        this.appRef.detachView(ref.hostView);
        ref.destroy();
        previouslyFocused?.focus?.({ preventScroll: true });
        resolve(result);
      });
    });
  }
}
