import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatService, Chat } from './../services/ChatService';
import { UserAvatarComponent } from './../avatar/user-avatar.component ';
import { Router } from '@angular/router';

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

  chats = this.chatService.chats;
  activeChatId = this.chatService.activeChatId;
  isLoading = this.chatService.isLoadingChats;

  searchQuery = signal('');

  filteredChats = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const chats = this.chats();

    if (!query) {
      return chats;
    }

    return chats.filter(chat =>
      chat.name?.toLowerCase().includes(query) ||
      chat.lastMessage?.toLowerCase().includes(query)
    );
  });

  selectChat(chat: Chat): void {
    void this.chatService.openChat(chat.id);
    void this.router.navigate(['/chat', chat.id]);
  }

  startNewChat(): void {
    void this.router.navigate(['/search']);
  }

  clearSearch(): void {
    this.searchQuery.set('');
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
