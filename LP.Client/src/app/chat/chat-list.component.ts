import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatService, Chat } from './../services/ChatService';
import { UserAvatarComponent } from './../avatar/user-avatar.component ';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChatExpiry, getEmptyChatExpiry } from './chat-expiry';

@Component({
  selector: 'app-chat-list',
  standalone: true,
  imports: [CommonModule, UserAvatarComponent],
  templateUrl: './chat-list.component.html',
  styleUrl: './chat-list.component.scss'
})
export class ChatListComponent {
  chatService = inject(ChatService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  chats = this.chatService.chats;
  activeChatId = this.chatService.activeChatId;
  showMobileMenu = false;

  private readonly now = signal(Date.now());
  private readonly openedChatId = signal<string | null>(null);

  constructor() {
    // Обновляем таймер раз в минуту, чтобы часы в подсказке уменьшались.
    const timer = setInterval(() => this.now.set(Date.now()), 60_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    // Пока чат открыт в chat.component, подсказку показываем там, а не в списке.
    this.updateOpenedChat(this.router.url);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(event => this.updateOpenedChat(event.urlAfterRedirects));
  }

  selectChat(chat: Chat): void {
    this.openedChatId.set(chat.id);
    void this.chatService.openChat(chat.id);
    void this.router.navigate(['/chat', chat.id]);
  }

  toggleMobileMenu(): void {
    this.showMobileMenu = !this.showMobileMenu;
  }

  /**
   * Для нового чата без сообщений возвращает оставшееся время до удаления.
   * В списке показываем только когда этот чат не открыт в chat.component.
   */
  getChatExpiry(chat: Chat): ChatExpiry | null {
    if (chat.id === this.openedChatId()) {
      return null;
    }

    return getEmptyChatExpiry(chat, this.now());
  }

  private updateOpenedChat(url: string): void {
    const match = /^\/chat\/([^/?#]+)/.exec(url);
    this.openedChatId.set(match ? match[1] : null);
  }

  getRelativeTime(date: Date | string): string {
    const chatDate = new Date(date);

    if (isNaN(chatDate.getTime()) || chatDate.getFullYear() <= 1) {
      return 'Давно';
    }

    const now = new Date();
    const diffInMs = now.getTime() - chatDate.getTime();

    if (diffInMs < 0) return 'Только что';

    const minutes = Math.floor(diffInMs / 60000);

    if (minutes < 1) return 'Только что';
    if (minutes < 60) return `${minutes} мин`;
    if (minutes < 1440) return `${Math.floor(minutes / 60)} ч`;

    const days = Math.floor(minutes / 1440);
    if (days > 365) return chatDate.toLocaleDateString();
    return `${days} дн`;
  }
}
