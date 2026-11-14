import { Component, inject, input, OnChanges, SimpleChanges, Renderer2, ElementRef, Injector, runInInjectionContext, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatusService } from './../services/UserStatusService';
import { API_BASE_URL } from '../app.config';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: 'user-avatar.component.html',
  styleUrl: 'user-avatar.component.scss'
})
export class UserAvatarComponent implements OnChanges {
  readonly userId = input<string>('');
  readonly name = input<string>('');

  private statService = inject(UserStatusService);
  private renderer = inject(Renderer2);
  private el = inject(ElementRef);
  private injector = inject(Injector);

  isOnline = () => this.statService.onlineUsers().has(this.userId());

  // Ссылка на реальную картинку
  currentAvatarSrc: string | null = null;
  defaultAvatar = 'assets/default-avatar.svg';

  constructor(    
    @Inject(API_BASE_URL) private base: string
  ) {
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['userId']) {
      const id = this.userId();
      this.currentAvatarSrc = null; // Сбрасываем при смене пользователя

      if (id) {
        this.loadRealAvatar(id);
      }
    }
  }

  private loadRealAvatar(id: string): void {
    const realUrl = `${this.base}/Photos/avatar/${id}`;
    const img = new Image();

    // Когда картинка успешно загрузилась
    img.onload = () => {
      // Используем runInInjectionContext, так как коллбэк может выполняться вне контекста Angular
      runInInjectionContext(this.injector, () => {
        this.currentAvatarSrc = realUrl;
        this.updateImage();
      });
    };

    // Если картинки нет (404), ничего не делаем - останется дефолтная
    img.onerror = () => {
      // Опционально: можно залогировать, что аватарки нет
    };

    img.src = realUrl;
  }

  private updateImage(): void {
    // Находим img элемент внутри нашего компонента и обновляем src
    const imgElement = this.el.nativeElement.querySelector('img');
    if (imgElement) {
      this.renderer.setAttribute(imgElement, 'src', this.currentAvatarSrc || this.defaultAvatar);
      // Убираем возможные классы прозрачности, если вы добавляли их
    }
  }
}
