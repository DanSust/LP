import { Component, inject, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatService, Chat } from './../services/ChatService';
import { UserStatusService } from './../services/UserStatusService';
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
  statService = inject(UserStatusService);
  router = inject(Router);

  chats = this.chatService.chats;  
  activeChatId = this.chatService.activeChatId;
  showMobileMenu = false;

  constructor() {
    // Автоматически следит за изменениями chats    
    //effect(() => {
    //  if (!this.statService.isConnected()) {
    //    console.log('⏳ Waiting for connection...');
    //    return; // Пропускаем, пока не подключимся
    //  }

    //  console.log('✅ Connection established, loading statuses...');
    //  const chatsList = this.chats(); // Получаем массив из сигнала
    //  chatsList.forEach((item) => {
    //    //this.statService.initialize(item.id); // Предполагаю, что нужен id
    //    this.statService.getUserStatus(item.userId).then(status => { item.status = status });
    //  });
    //});
  }

  selectChat(chat: Chat): void {
    console.log('selectChat ' + chat.userId);
    this.chatService.activeChatId.set(chat.id);
    this.chatService.activeUserId.set(chat.userId);
    this.chatService.markChatAsRead(chat.id);

    //if (window.innerWidth <= 768) {
    //  this.showMobileMenu = false;
    //}

    this.router.navigate(['/chat', chat.id]);
  }

  toggleMobileMenu(): void {
    this.showMobileMenu = !this.showMobileMenu;
  }

  getRelativeTime(date: Date | string): string {
    const now = new Date();
    const chatDate = new Date(date);
    const diff = now.getTime() - chatDate.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return 'Только что';
    if (minutes < 60) return `${minutes} мин`;
    if (minutes < 1440) return `${Math.floor(minutes / 60)} ч`;
    return `${Math.floor(minutes / 1440)} дн`;
  }
}
