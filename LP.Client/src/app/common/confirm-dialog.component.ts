import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Output,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type ConfirmDialogTone = 'default' | 'danger' | 'warning';
export type ConfirmDialogIcon = 'block' | 'flag' | 'alert' | 'question' | 'trash';

export interface ConfirmDialogCommentOptions {
  label?: string;
  placeholder?: string;
  maxLength?: number;
}

export interface ConfirmDialogOptions {
  title: string;
  message?: string;
  details?: string[];
  confirmText?: string;
  cancelText?: string;
  tone?: ConfirmDialogTone;
  icon?: ConfirmDialogIcon;
  /** Если задано, в диалоге появляется поле для комментария. */
  comment?: ConfirmDialogCommentOptions;
}

export interface ConfirmDialogResult {
  confirmed: boolean;
  comment: string;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="backdrop" [class.closing]="closing" (click)="cancel()">
      <div
        class="dialog"
        [class.dialog-danger]="tone === 'danger'"
        [class.dialog-warning]="tone === 'warning'"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title"
        (click)="$event.stopPropagation()"
      >
        @if (icon) {
          <div class="dialog-icon" aria-hidden="true">
            @switch (icon) {
              @case ('block') {
                <svg viewBox="0 0 24 24">
                  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zM4 12c0-4.42 3.58-8 8-8 1.85 0 3.55.63 4.9 1.69L5.69 16.9C4.63 15.55 4 13.85 4 12zm8 8c-1.85 0-3.55-.63-4.9-1.69L18.31 7.1C19.37 8.45 20 10.15 20 12c0 4.42-3.58 8-8 8z" />
                </svg>
              }
              @case ('flag') {
                <svg viewBox="0 0 24 24">
                  <path d="M14.4 6L14 4H5v17h2v-7h5.6l.4 2h7V6z" />
                </svg>
              }
              @case ('alert') {
                <svg viewBox="0 0 24 24">
                  <path d="M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z" />
                </svg>
              }
              @case ('trash') {
                <svg viewBox="0 0 24 24">
                  <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z" />
                </svg>
              }
              @default {
                <svg viewBox="0 0 24 24">
                  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z" />
                </svg>
              }
            }
          </div>
        }

        <h2 class="dialog-title">{{ title }}</h2>

        @if (message) {
          <p class="dialog-message">{{ message }}</p>
        }

        @if (details.length) {
          <ul class="dialog-details">
            @for (item of details; track item) {
              <li>{{ item }}</li>
            }
          </ul>
        }

        @if (commentOptions) {
          <div class="dialog-comment">
            <label for="confirm-dialog-comment">{{ commentLabel }}</label>
            <textarea
              id="confirm-dialog-comment"
              #commentInput
              [(ngModel)]="comment"
              [placeholder]="commentPlaceholder"
              [attr.maxlength]="commentOptions.maxLength ?? null"
              rows="3"
            ></textarea>
          </div>
        }

        <div class="dialog-actions">
          <button type="button" class="btn btn-cancel" (click)="cancel()" #cancelButton>
            {{ cancelText }}
          </button>
          <button type="button" class="btn btn-confirm" (click)="confirm()" #confirmButton>
            {{ confirmText }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .backdrop {
        position: fixed;
        inset: 0;
        z-index: 9500;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 20px;
        background: rgba(18, 28, 36, 0.55);
        backdrop-filter: blur(6px);
        -webkit-backdrop-filter: blur(6px);
        animation: backdropIn 0.18s ease-out;
      }

      .backdrop.closing {
        animation: backdropOut 0.16s ease-in forwards;
      }

      .backdrop.closing .dialog {
        animation: dialogOut 0.16s ease-in forwards;
      }

      .dialog {
        position: relative;
        width: min(440px, 100%);
        max-height: calc(100vh - 40px);
        overflow-y: auto;
        box-sizing: border-box;
        padding: 28px;
        border-radius: 22px;
        background:
          radial-gradient(120% 120% at 0% 0%, rgba(255, 255, 255, 0.16) 0%, rgba(255, 255, 255, 0) 55%),
          linear-gradient(160deg, #5b8098 0%, #6f93ab 55%, #7ba0b7 100%);
        border: 1px solid rgba(255, 255, 255, 0.18);
        box-shadow: 0 24px 60px rgba(10, 20, 28, 0.45), inset 0 1px 0 rgba(255, 255, 255, 0.15);
        color: #fff;
        font-family: 'Roboto', 'Helvetica Neue', sans-serif;
        animation: dialogIn 0.24s cubic-bezier(0.18, 0.89, 0.32, 1.18);
      }

      .dialog::-webkit-scrollbar {
        width: 6px;
      }

      .dialog::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, 0.25);
        border-radius: 3px;
      }

      .dialog-icon {
        width: 56px;
        height: 56px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-bottom: 18px;
        background: rgba(255, 255, 255, 0.14);
        border: 1px solid rgba(255, 255, 255, 0.2);
        box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.2);
      }

      .dialog-icon svg {
        width: 28px;
        height: 28px;
        fill: currentColor;
      }

      .dialog-danger .dialog-icon {
        background: linear-gradient(135deg, rgba(233, 30, 99, 0.92), rgba(194, 24, 91, 0.92));
        border-color: rgba(255, 255, 255, 0.25);
        box-shadow: 0 8px 20px rgba(233, 30, 99, 0.35);
      }

      .dialog-warning .dialog-icon {
        background: linear-gradient(135deg, rgba(255, 152, 0, 0.92), rgba(245, 124, 0, 0.92));
        box-shadow: 0 8px 20px rgba(245, 124, 0, 0.32);
      }

      .dialog-title {
        margin: 0 0 10px;
        font-size: 20px;
        font-weight: 600;
        letter-spacing: 0.2px;
      }

      .dialog-message {
        margin: 0;
        font-size: 14.5px;
        line-height: 1.55;
        color: rgba(255, 255, 255, 0.82);
      }

      .dialog-details {
        margin: 16px 0 0;
        padding: 14px 16px;
        list-style: none;
        border-radius: 14px;
        background: rgba(0, 0, 0, 0.14);
        border: 1px solid rgba(255, 255, 255, 0.08);
        font-size: 13px;
        color: rgba(255, 255, 255, 0.78);
      }

      .dialog-details li {
        position: relative;
        padding-left: 20px;
        line-height: 1.5;
      }

      .dialog-details li + li {
        margin-top: 8px;
      }

      .dialog-details li::before {
        content: '';
        position: absolute;
        left: 4px;
        top: 7px;
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.55);
      }

      .dialog-danger .dialog-details li::before {
        background: #ff7aa8;
      }

      .dialog-warning .dialog-details li::before {
        background: #ffc266;
      }

      .dialog-comment {
        margin-top: 16px;
      }

      .dialog-comment label {
        display: block;
        margin-bottom: 6px;
        font-size: 12.5px;
        color: rgba(255, 255, 255, 0.7);
      }

      .dialog-comment textarea {
        width: 100%;
        box-sizing: border-box;
        resize: vertical;
        min-height: 76px;
        max-height: 160px;
        padding: 12px 14px;
        border-radius: 12px;
        border: 1px solid rgba(255, 255, 255, 0.2);
        background: rgba(0, 0, 0, 0.16);
        color: #fff;
        font: inherit;
        font-size: 14px;
        line-height: 1.45;
        outline: none;
        transition: border-color 0.2s, box-shadow 0.2s;
      }

      .dialog-comment textarea::placeholder {
        color: rgba(255, 255, 255, 0.42);
      }

      .dialog-comment textarea:focus {
        border-color: rgba(255, 255, 255, 0.45);
        box-shadow: 0 0 0 3px rgba(255, 255, 255, 0.12);
      }

      .dialog-actions {
        display: flex;
        justify-content: flex-end;
        gap: 10px;
        margin-top: 24px;
      }

      .btn {
        border: none;
        cursor: pointer;
        font: inherit;
        font-size: 14px;
        font-weight: 600;
        padding: 12px 22px;
        border-radius: 14px;
        transition: transform 0.15s, box-shadow 0.2s, background 0.2s, filter 0.2s;
      }

      .btn:active {
        transform: translateY(1px);
      }

      .btn:focus-visible {
        outline: 2px solid rgba(255, 255, 255, 0.75);
        outline-offset: 2px;
      }

      .btn-cancel {
        background: rgba(255, 255, 255, 0.1);
        color: #fff;
        border: 1px solid rgba(255, 255, 255, 0.22);
      }

      .btn-cancel:hover {
        background: rgba(255, 255, 255, 0.18);
      }

      .btn-confirm {
        background: linear-gradient(135deg, #557a94, #7396ae);
        color: #fff;
        box-shadow: 0 10px 22px rgba(0, 0, 0, 0.25);
      }

      .dialog-danger .btn-confirm {
        background: linear-gradient(135deg, #e91e63, #c2185b);
        box-shadow: 0 10px 24px rgba(233, 30, 99, 0.35);
      }

      .dialog-warning .btn-confirm {
        background: linear-gradient(135deg, #ff9800, #ef6c00);
        box-shadow: 0 10px 24px rgba(239, 108, 0, 0.3);
      }

      .btn-confirm:hover {
        transform: translateY(-1px);
        filter: brightness(1.05);
      }

      @keyframes backdropIn {
        from { opacity: 0; }
        to { opacity: 1; }
      }

      @keyframes backdropOut {
        from { opacity: 1; }
        to { opacity: 0; }
      }

      @keyframes dialogIn {
        from { opacity: 0; transform: translateY(16px) scale(0.96); }
        to { opacity: 1; transform: translateY(0) scale(1); }
      }

      @keyframes dialogOut {
        from { opacity: 1; transform: translateY(0) scale(1); }
        to { opacity: 0; transform: translateY(8px) scale(0.97); }
      }

      @media (max-width: 480px) {
        .backdrop {
          padding: 16px;
        }

        .dialog {
          padding: 22px;
          border-radius: 20px;
        }

        .dialog-actions {
          flex-direction: column-reverse;
        }

        .btn {
          width: 100%;
        }
      }
    `
  ]
})
export class ConfirmDialogComponent {
  @Output() readonly closed = new EventEmitter<ConfirmDialogResult>();

  @ViewChild('cancelButton') private cancelButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('confirmButton') private confirmButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('commentInput') private commentInput?: ElementRef<HTMLTextAreaElement>;

  title = '';
  message = '';
  details: string[] = [];
  confirmText = 'Подтвердить';
  cancelText = 'Отмена';
  tone: ConfirmDialogTone = 'default';
  icon: ConfirmDialogIcon | '' = '';
  commentOptions?: ConfirmDialogCommentOptions;
  comment = '';
  closing = false;

  private readonly cdr = inject(ChangeDetectorRef);

  get commentLabel(): string {
    return this.commentOptions?.label ?? 'Комментарий';
  }

  get commentPlaceholder(): string {
    return this.commentOptions?.placeholder ?? '';
  }

  apply(options: ConfirmDialogOptions): void {
    this.title = options.title;
    this.message = options.message ?? '';
    this.details = options.details ?? [];
    this.confirmText = options.confirmText ?? 'Подтвердить';
    this.cancelText = options.cancelText ?? 'Отмена';
    this.tone = options.tone ?? 'default';
    this.icon = options.icon ?? '';
    this.commentOptions = options.comment;
  }

  /** Вызывается сервисом после вставки диалога в DOM. */
  focusInitial(): void {
    const target = this.commentInput?.nativeElement
      ?? (this.tone === 'danger' ? this.cancelButton?.nativeElement : this.confirmButton?.nativeElement);

    target?.focus({ preventScroll: true });
  }

  confirm(): void {
    this.close(true);
  }

  cancel(): void {
    this.close(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancel();
  }

  private close(confirmed: boolean): void {
    if (this.closing) {
      return;
    }

    this.closing = true;
    this.cdr.detectChanges();

    const result: ConfirmDialogResult = {
      confirmed,
      comment: confirmed ? this.comment.trim() : ''
    };

    setTimeout(() => this.closed.emit(result), 160);
  }
}
