import { CommonModule, JsonPipe } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Inject, Output } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CdkDragDrop, CdkDrag, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { API_BASE_URL } from './../../app.config'; //'../../main';
import { FileUploadComponent } from '../../common/file-upload';
import { ToastService } from '../../common/toast.service';
import { Observable, tap } from 'rxjs';
import { BaseComponent } from '../../common/base.component';
import { PhotoIconTheme, photoIconClass, pickIconTheme } from '../../common/photo-icon-theme';

interface CropFrame {
  /** Положение и размер квадрата в пикселях внутри .photo-box. */
  left: number;
  top: number;
  side: number;
  /** Вдоль какой оси можно двигать рамку. */
  axis: 'x' | 'y';
  maxOffset: number;
  offset: number;
  /** Масштаб и смещение картинки внутри .photo-box — для перевода в естественные координаты. */
  scaleX: number;
  scaleY: number;
  imgLeft: number;
  imgTop: number;
}

interface CropDrag {
  pointerId: number;
  startX: number;
  startY: number;
  startOffset: number;
}

@Component({
  selector: 'user-photo',
  standalone: true,
  templateUrl: './user-photo.html',
  styleUrl: './user-photo.css',
  imports: [
    CdkDrag, CdkDropList, MatIconModule,
    MatProgressSpinnerModule,
    FileUploadComponent],
})
export class UserPhoto extends BaseComponent {
  /** Сообщаем родителю количество фото — участвует в заполненности профиля. */
  @Output() photoCountChange = new EventEmitter<number>();

  isSaving: boolean = false;
  items: { userId: string; id: string }[] = [];
  file: File | null = null; // Variable to store file
  baseURL = "";
  readonly maxPhotos = 5;

  /** Перемещаемая квадратная рамка на первой фотографии. */
  cropFrame: CropFrame | null = null;
  private cropDrag: CropDrag | null = null;

  constructor(
    private http: HttpClient,
    private toast: ToastService,
    @Inject(API_BASE_URL) private base: string,
    private host: ElementRef<HTMLElement>) {
    super();
    this.baseURL = base;
  }

  protected LoadData(): Observable<{ userId: string; id: string }[]> {
    return this.http
      .get<{ userId: string; id: string }[]>(this.base + "/Photos/list", { withCredentials: true })
      .pipe(tap((result) => {
        //console.log('📸 Photos loaded:', result);
        this.items = result;
        this.emitPhotoCount();
      }));
  }

  private emitPhotoCount(): void {
    this.photoCountChange.emit(this.items.length);
  }

  trackById(index: number, item: any): string {
    //console.log(item);
    return item.id; // Return unique identifier
  }

  /**
   * Цвет иконки удаления подбирается по яркости участка фотографии под ней,
   * чтобы значок не сливался со светлым (или, наоборот, тёмным) фоном.
   */
  private readonly deleteIconTheme = new Map<string, PhotoIconTheme>();

  deleteIconClass(id: string): string {
    return photoIconClass(this.deleteIconTheme.get(id) ?? 'dark');
  }

  onPhotoLoad(event: Event, id: string): void {
    const img = event.target as HTMLImageElement;
    this.deleteIconTheme.set(id, pickIconTheme(img));

    if (this.items[0]?.id === id) {
      this.refreshCropFrame();
    }
  }

  /** Квадрат вписан в видимую область фото: вся высота или вся ширина. */
  private refreshCropFrame(): void {
    const box = this.host.nativeElement.querySelector('.photo-box');
    const img = box?.querySelector('img') as HTMLImageElement | null;
    if (!box || !img || !img.complete || !img.naturalWidth) {
      this.cropFrame = null;
      return;
    }

    const boxRect = box.getBoundingClientRect();
    const imgRect = img.getBoundingClientRect();
    const left = Math.max(0, imgRect.left - boxRect.left);
    const top = Math.max(0, imgRect.top - boxRect.top);
    const right = Math.min(boxRect.width, imgRect.right - boxRect.left);
    const bottom = Math.min(boxRect.height, imgRect.bottom - boxRect.top);
    const visibleWidth = Math.max(0, right - left);
    const visibleHeight = Math.max(0, bottom - top);
    const side = Math.min(visibleWidth, visibleHeight);

    if (side <= 0) {
      this.cropFrame = null;
      return;
    }

    const previousOffset = this.cropFrame?.offset ?? 0;
    const horizontal = visibleWidth >= visibleHeight;
    const maxOffset = Math.abs(visibleWidth - visibleHeight);
    this.cropFrame = {
      left,
      top,
      side,
      axis: horizontal ? 'x' : 'y',
      maxOffset,
      offset: Math.min(previousOffset, maxOffset),
      scaleX: img.naturalWidth / (imgRect.width || 1),
      scaleY: img.naturalHeight / (imgRect.height || 1),
      imgLeft: imgRect.left - boxRect.left,
      imgTop: imgRect.top - boxRect.top
    };
  }

  cropLeft(): number {
    const frame = this.cropFrame;
    return frame ? frame.left + (frame.axis === 'x' ? frame.offset : 0) : 0;
  }

  cropTop(): number {
    const frame = this.cropFrame;
    return frame ? frame.top + (frame.axis === 'y' ? frame.offset : 0) : 0;
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    if (this.cropFrame) {
      this.refreshCropFrame();
    }
  }

  onCropPointerDown(event: PointerEvent): void {
    if (!this.cropFrame) return;
    // Не даём родительскому cdkDrag начать перетаскивание фотографии.
    event.stopPropagation();
    event.preventDefault();

    const target = event.currentTarget as HTMLElement;
    target.setPointerCapture(event.pointerId);
    this.cropDrag = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      startOffset: this.cropFrame.offset
    };
  }

  onCropPointerMove(event: PointerEvent): void {
    const frame = this.cropFrame;
    const drag = this.cropDrag;
    if (!frame || !drag || drag.pointerId !== event.pointerId) return;

    event.stopPropagation();
    const delta = frame.axis === 'x'
      ? event.clientX - drag.startX
      : event.clientY - drag.startY;
    frame.offset = Math.max(0, Math.min(drag.startOffset + delta, frame.maxOffset));
  }

  onCropPointerUp(event: PointerEvent): void {
    if (!this.cropDrag || this.cropDrag.pointerId !== event.pointerId) return;
    event.stopPropagation();

    const target = event.currentTarget as HTMLElement;
    if (target.hasPointerCapture(event.pointerId)) {
      target.releasePointerCapture(event.pointerId);
    }
    this.cropDrag = null;
  }

  /** Обрезаем выбранную область и отправляем её как новый аватар. */
  onCropAvatarClick(event: MouseEvent): void {
    event.stopPropagation();
    const frame = this.cropFrame;
    const box = this.host.nativeElement.querySelector('.photo-box');
    const img = box?.querySelector('img') as HTMLImageElement | null;
    if (!frame || !img || !img.complete || !img.naturalWidth) return;

    try {
      const naturalSide = Math.min(
        frame.side * frame.scaleX,
        frame.side * frame.scaleY,
        img.naturalWidth,
        img.naturalHeight
      );
      const x = Math.max(
        0,
        Math.min((this.cropLeft() - frame.imgLeft) * frame.scaleX, img.naturalWidth - naturalSide)
      );
      const y = Math.max(
        0,
        Math.min((this.cropTop() - frame.imgTop) * frame.scaleY, img.naturalHeight - naturalSide)
      );

      const canvas = document.createElement('canvas');
      const size = 256;
      canvas.width = size;
      canvas.height = size;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      ctx.drawImage(img, x, y, naturalSide, naturalSide, 0, 0, size, size);
      canvas.toBlob(
        blob => {
          if (!blob) {
            this.toast.error('Не удалось создать аватар');
            return;
          }

          const formData = new FormData();
          formData.append('file', blob, 'avatar.jpg');
          this.http.post(`${this.base}/Photos/avatar`, formData, { withCredentials: true }).subscribe({
            next: () => this.toast.success('Аватар обновлён'),
            error: err => {
              console.error('Не удалось сохранить аватар', err);
              this.toast.error('Не удалось сохранить аватар');
            }
          });
        },
        'image/jpeg',
        0.92
      );
    } catch (err) {
      console.error('Не удалось создать аватар', err);
      this.toast.error('Не удалось создать аватар');
    }
  }

  drop(event: CdkDragDrop<string[]>) {
    this.isSaving = true;
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    // Первая фотография могла измениться — пересчитываем рамку под неё.
    setTimeout(() => this.refreshCropFrame(), 0);
    console.log(this.items[event.previousIndex].id);
    console.log(this.items[event.currentIndex].id);
    console.log(this.base);
    this.http.post(this.base + '/Photos/order/' + this.items[event.currentIndex].id, null, { withCredentials: true })
      .subscribe(() => { this.isSaving = false; });
  }

  // On file Select
  onChange(event: any) {
    const file: File = event.target.files[0];

    if (file) {
      //this.status = "initial";
      this.file = file;
    }
  }

  onDelete(id: string) {    
    this.http.post(/*this.base + "/Photos/delete/{id}"*/ `${this.base}/Photos/delete/${id}`, null, { withCredentials: true })
      .subscribe({
      next: () => {
        const wasFirst = this.items[0]?.id === id;
        this.items = this.items.filter(item => item.id !== id);
        this.emitPhotoCount();
        if (wasFirst) {
          this.cropFrame = null;
          // Новая первая фотография уже загружена — пересчитываем рамку после рендера.
          setTimeout(() => this.refreshCropFrame(), 0);
        }
      },
      error: (error: any) => {
        console.log(error);
        return (() => error);
      },
    });
  }

  onUpload() {
    if (this.items.length >= this.maxPhotos) {
      this.toast.warning('Достигнут лимит в 5 фотографий')
      return;
    }

    if (this.file) {
      const formData = new FormData();

      formData.append('file', this.file, this.file.name);

      const upload$ = this.http.post<{ userId: string; id: string }>(this.base + "/Photos/add", formData, { withCredentials: true });           

      upload$.subscribe(result => {
        //console.log(result);
        this.items = [...this.items, result];
        this.emitPhotoCount();
      });
    }
  }

  onFileUploaded(newPhoto: { userId: string; id: string }): void {
    if (this.items.length < this.maxPhotos) {
      this.items = [...this.items, newPhoto];
      this.emitPhotoCount();
      console.log('✅ New photo added:', newPhoto);
    } else {
      // Здесь можно вызвать toast-уведомление, если оно доступно в этом компоненте
      this.toast.info('Лимит достигнут, фотография не добавлена');
    }
  }
  
}
