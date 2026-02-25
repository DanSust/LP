import { Component, inject, OnInit, OnDestroy, ViewChild, ElementRef, effect, computed, signal, HostListener, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { ChatService, Message } from './../services/ChatService';
import { generateGUID } from './../common/GUID';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserAvatarComponent } from './../avatar/user-avatar.component ';
import { ParseQuotePipe } from './parse-quote.pipe';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../app.config';

const QUOTE_DELIMITER = '[[[QUOTE:';
const QUOTE_END = ']]]';
const EMOJIS = [
  // Smileys & Emotion (самые популярные)
  '😊', '😂', '❤️', '😍', '🔥', '🎉', '😎', '🤔', '😭', '🥺', '👋',
  '😄', '😁', '🥰', '😘', '🤗', '🤩', '😉', '😇', '🙂', '🤪', '😝',

  // СЕРДЦА — ТОЛЬКО САМЫЕ НУЖНЫЕ
  '💖', '💕', '💔', '❤️‍🔥', '💌', '💋',

  // ЖЕСТЫ — УВЕЛИЧЕНО КОЛИЧЕСТВО
  '👍', '👎', '👏', '🙏', '🤝', '💪', '🙌', '👌', '✌️', '🤟', '🤘',
  '🤙', '✊', '🤛', '🤜', '🤞', '✋', '🤚', '🤲', '👐', '🙋', '🙅',
  '🙆', '🙇', '🤦', '🤷', '💅', '🤏', '🫰', '👊', '🤌', '🤏', '✍️',

  // Символы и события
  '💯', '✨', '⭐', '❌', '✅', '⚠️', '💬', '🔕', '🔔', '🎯',

  // Еда и напитки
  //'☕', '🍕', '🍔', '🍰', '🍓', '🍫', '🍿',

  // Природа и погода
  //'🌸', '🌹', '🍀', '☀️', '🌙', '🌈', '🌺', '💫',

  // Активности
  //'🎮', '🎨', '🏃‍♂️', '🏃‍♀️', '🚴', '🏆', '🎸',

  // Животные
  //'🐱', '🐶', '🦊', '🐻', '🐨', '🐼', '🐧', '🦋'
];

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, UserAvatarComponent, ParseQuotePipe,
    MatMenuModule,
    MatIconModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatView implements OnInit, OnDestroy {
  @ViewChild('messageContainer') messageContainer!: ElementRef;
  @ViewChild('messageInput') messageInput!: ElementRef<HTMLTextAreaElement>; 
  newMessage = '';
  userId: any;
  chatService = inject(ChatService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private location = inject(Location);
  private subscription: Subscription | undefined;
  
  showMenu = signal(false);
  showEmojiPicker = signal(false);
  quotedMessage = signal<Message | null>(null);
  emojis = EMOJIS;

  // Вычисляем имя второго участника
  otherParticipantName = computed(() => {
    const messages = this.chatService.filteredMessages();    
    const otherMessage = messages.find(msg => !msg.own && msg.userName);
    return otherMessage?.userName || 'Участник';
  });

  participantName = signal<string>('Участник');

  // Вычисляем аватар
  otherParticipantAvatar = computed(() => {
    return 'assets/default-avatar.png';
  });

  constructor(private http: HttpClient,
    @Inject(API_BASE_URL) private base: string) {
    //this.userId = computed(() => { this.chatService.activeUserId() });
    // Автоматическая отправка read receipts и скролл
    effect(() => {      
      this.userId = this.chatService.activeUserId();
      this.chatService.filteredMessages().forEach(msg => {
        if (!msg.own && msg.status === 'delivered') {
          this.chatService.markAsRead(msg.id);
        }
      });
      this.scrollToBottom();
      if (this.chatService.chats().length === 0) {
        this.router.navigate(['/chat']);
      }
      this.loadParticipantProfile(this.userId);
      console.log('effect ' + this.userId);
    });
  }

  private loadParticipantProfile(userId: string) {
    this.http.get<any>(`${this.base}/users/view?id=${userId}`, { withCredentials: true })
      .subscribe({
        next: (user) => {
          // caption - это имя пользователя (как в vote.service.ts)
          this.participantName.set(user.caption || user.name || 'Участник');
        },
        error: () => {
          // Fallback: берем имя из списка чатов если API недоступно
          const chat = this.chatService.chats().find(c => c.userId === userId);
          this.participantName.set(chat?.name || 'Участник');
        }
      });
  }

  onAvatarClick(): void {
    console.log('onAvatarClick()', this.chatService.otherUser()?.userId);
    const otherUserId = this.chatService.otherUser()?.userId;
    if (otherUserId) {
      console.log('otherUserId', otherUserId);
      this.router.navigate(['/vote', otherUserId]);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!event.target || !(event.target as HTMLElement).closest('.menu-container')) {
      this.showMenu.set(false);
    }
    // Закрываем эмодзи-пикер
    if (!event.target || !(event.target as HTMLElement).closest('.emoji-picker, .emoji-button')) {
      this.showEmojiPicker.set(false);
    }
  }

  goBack(): void {
    if (window.innerWidth <= 768) {
      this.router.navigate(['/chat']);
    } else {
      this.location.back();
    }
  }

  //toggleMenu(event: MouseEvent): void {
  //  event.stopPropagation();
  //  this.showMenu.update(value => !value);
  //}

  toggleEmojiPicker(event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    this.showEmojiPicker.update(value => !value);
  }

  addEmoji(emoji: string): void {
    this.newMessage += emoji;
    this.messageInput.nativeElement.focus();
    this.onInput(); // обновить высоту textarea
  }

  analizeChat(): void {
    this.subscription = this.route.paramMap.subscribe(params => {
      const id = params.get("id");
      if (id) {

        this.chatService.activeChatId.set(id);
        this.chatService.analizeChat(id);
        //console.log('ngOnInit ' + this.userId);
      }
    });
  }

  deleteChat(): void {
    console.log('Удалить чат');
    this.subscription = this.route.paramMap.subscribe(params => {
      const id = params.get("id");
      if (id) {

        this.chatService.activeChatId.set(id);
        this.chatService.deleteChat(id);
        //console.log('ngOnInit ' + this.userId);
      }
    });
    
    this.showMenu.set(false);
  }

  blockUser(): void {
    console.log('Заблокировать пользователя');
    this.showMenu.set(false);
  }

  reportSpam(): void {
    console.log('Сообщить о спаме');
    this.showMenu.set(false);
  }

  replyToMessage(message: Message): void {
    this.quotedMessage.set(message);
  }

  quoteMessage(message: Message): void {
    const quote = `„ ${message.text}“\n`;
    this.newMessage = this.newMessage ? `${quote}${this.newMessage}` : quote;
  }

  clearQuote(): void {
    this.quotedMessage.set(null);
  }

  onEnter(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (!keyboardEvent.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  // Авто-изменение высоты textarea
  onInput(): void {
    const textarea = this.messageInput.nativeElement;
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';
  }

  ngOnInit(): void {
    this.subscription = this.route.paramMap.subscribe(params => {
      const id = params.get("id");
      if (id) {        
        this.userId = this.chatService.activeUserId();
        this.chatService.activeChatId.set(id);
        //this.chatService.loadMessages(id);
        //console.log('ngOnInit ' + this.userId);
      }
    });

    //this.chatService.clearReadReceipts();

    // Подключаемся к SignalR
    //this.chatService.connect(this.chatService.activeChatId(), 'User');

    // Загружаем историю
    setTimeout(() => {
      //this.chatService.loadMessages(this.chatService.activeChatId());
    }, 500);
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    // Покидаем чат при переходе на другой URL
    const currentChatId = this.chatService.activeChatId();
    if (currentChatId) {
      this.chatService.leaveChat(currentChatId);
    }
  }

  async send(): Promise<void> {
    if (this.newMessage.trim()) {
      let messageText = this.newMessage;
      const quote = this.quotedMessage();

      // ДОБАВЛЯЕМ ЦИТАТУ В ТЕКСТ С ПОМОЩЬЮ СПЕЦСИМВОЛОВ
      if (quote) {
        messageText = `${QUOTE_DELIMITER}${quote.text}${QUOTE_END}\n${messageText}`;
        this.quotedMessage.set(null); // Очищаем после добавления
      }

      await this.chatService.sendMessage(messageText);
      this.newMessage = '';

      if (this.messageInput) {
        const textarea = this.messageInput.nativeElement;
        textarea.style.height = 'auto';
      }
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.messageContainer) {
        const element = this.messageContainer.nativeElement;
        element.scrollTop = element.scrollHeight;
      }
    }, 0);
  }
}
