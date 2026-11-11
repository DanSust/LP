import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  ViewChild,
  computed,
  effect,
  inject,
  signal
} from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChatService, Message } from './../services/ChatService';
import { UserAvatarComponent } from './../avatar/user-avatar.component ';
import { ParseQuotePipe } from './parse-quote.pipe';
import { getEmptyChatExpiry } from './chat-expiry';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';

const QUOTE_DELIMITER = '[[[QUOTE:';
const QUOTE_END = ']]]';
const EMOJIS = [
  '😊', '😂', '❤️', '😍', '🔥', '🎉', '😎', '🤔', '😭', '🥺', '👋',
  '😄', '😁', '🥰', '😘', '🤗', '🤩', '😉', '😇', '🙂', '🤪', '😝',

  '💖', '💕', '💔', '❤️‍🔥', '💌', '💋',

  '👍', '👎', '👏', '🙏', '🤝', '💪', '🙌', '👌', '✌️', '🤟', '🤘',
  '🤙', '✊', '🤛', '🤜', '🤞', '✋', '🤚', '🤲', '👐', '🙋', '🙅',
  '🙆', '🙇', '🤦', '🤷', '💅', '🤏', '🫰', '👊', '🤌', '🤏', '✍️',

  '💯', '✨', '⭐', '❌', '✅', '⚠️', '💬', '🔕', '🔔', '🎯'
];

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, UserAvatarComponent, ParseQuotePipe, MatMenuModule, MatIconModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatView {
  @ViewChild('messageContainer') messageContainer?: ElementRef<HTMLElement>;
  @ViewChild('messageInput') messageInput?: ElementRef<HTMLTextAreaElement>;

  newMessage = '';
  chatService = inject(ChatService);

  showMenu = signal(false);
  showEmojiPicker = signal(false);
  quotedMessage = signal<Message | null>(null);
  sendError = signal<string | null>(null);
  isSending = signal(false);
  emojis = EMOJIS;

  participantName = computed(() => {
    return this.chatService.otherUser()?.name || this.chatService.activeChat()?.name || 'Участник';
  });

  private readonly now = signal(Date.now());

  emptyChatExpiry = computed(() => {
    const chat = this.chatService.activeChat();
    return chat ? getEmptyChatExpiry(chat, this.now()) : null;
  });

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private location = inject(Location);
  private destroyRef = inject(DestroyRef);

  constructor() {
    // Обновляем таймер раз в минуту, чтобы часы в подсказке уменьшались.
    const timer = setInterval(() => this.now.set(Date.now()), 60_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const chatId = params.get('id');
        if (!chatId) {
          return;
        }

        this.sendError.set(null);
        void this.chatService.openChat(chatId);
      });

    // Отмечаем входящие сообщения прочитанными и держим скролл внизу.
    effect(() => {
      const chatId = this.chatService.activeChatId();
      const messages = this.chatService.filteredMessages();

      if (chatId) {
        for (const message of messages) {
          if (!message.own && message.status === 'delivered') {
            void this.chatService.markAsRead(chatId, message.id);
          }
        }
      }

      this.scrollToBottom();
    });
  }

  onAvatarClick(): void {
    const otherUserId = this.chatService.otherUser()?.userId;
    if (otherUserId) {
      void this.router.navigate(['/vote', otherUserId]);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;

    if (!target?.closest('.menu-container')) {
      this.showMenu.set(false);
    }

    if (!target?.closest('.emoji-picker, .emoji-button')) {
      this.showEmojiPicker.set(false);
    }
  }

  goBack(): void {
    if (window.innerWidth <= 768) {
      void this.router.navigate(['/chat']);
    } else {
      this.location.back();
    }
  }

  toggleEmojiPicker(event?: MouseEvent): void {
    event?.stopPropagation();
    this.showEmojiPicker.update(value => !value);
  }

  addEmoji(emoji: string): void {
    this.newMessage += emoji;
    this.messageInput?.nativeElement.focus();
    this.onInput();
  }

  async analizeChat(): Promise<void> {
    this.showMenu.set(false);
    const chatId = this.chatService.activeChatId();
    if (chatId) {
      await this.chatService.analizeChat(chatId);
    }
  }

  async deleteChat(): Promise<void> {
    this.showMenu.set(false);
    const chatId = this.chatService.activeChatId();
    if (!chatId) {
      return;
    }

    const deleted = await this.chatService.deleteChat(chatId);
    if (deleted) {
      await this.router.navigate(['/chat']);
    }
  }

  blockUser(): void {
    this.showMenu.set(false);
  }

  reportSpam(): void {
    this.showMenu.set(false);
  }

  replyToMessage(message: Message): void {
    this.quotedMessage.set(message);
  }

  clearQuote(): void {
    this.quotedMessage.set(null);
  }

  onEnter(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (!keyboardEvent.shiftKey) {
      event.preventDefault();
      void this.send();
    }
  }

  onInput(): void {
    const textarea = this.messageInput?.nativeElement;
    if (!textarea) {
      return;
    }

    textarea.style.height = 'auto';

    const minHeight = 24;
    const maxHeight = 120;
    const newHeight = Math.max(minHeight, textarea.scrollHeight);
    textarea.style.height = `${Math.min(newHeight, maxHeight)}px`;
  }

  async send(): Promise<void> {
    const text = this.newMessage.trim();
    if (!text || this.isSending()) {
      return;
    }

    let messageText = this.newMessage;
    const quote = this.quotedMessage();

    if (quote) {
      messageText = `${QUOTE_DELIMITER}${quote.text}${QUOTE_END}\n${messageText}`;
    }

    this.isSending.set(true);
    this.sendError.set(null);

    const sent = await this.chatService.sendMessage(messageText);
    this.isSending.set(false);

    if (!sent) {
      this.sendError.set('Не удалось отправить сообщение. Проверьте соединение.');
      return;
    }

    this.newMessage = '';
    this.quotedMessage.set(null);

    const textarea = this.messageInput?.nativeElement;
    if (textarea) {
      textarea.style.height = 'auto';
    }

    this.scrollToBottom();
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.messageContainer?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    }, 0);
  }
}
