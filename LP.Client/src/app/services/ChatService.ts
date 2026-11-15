// src/app/services/ChatService.ts
import { Injectable, signal, computed, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SignalRConnectionManager } from './SignalRConnectionManager';
import { generateGUID } from '../common/GUID';
import { API_BASE_URL } from '../app.config';

export type MessageStatus = 'pending' | 'delivered' | 'read';

export interface Chat {
  id: string;
  name: string;
  avatar?: string;
  userId: string;
  lastMessage?: string;
  lastMessageTime?: Date | string | null;
  time?: Date | string;
  unreadMessagesCount: number;
  status: boolean;
}

export interface Message {
  id: string;
  chatId: string;
  userId?: string;
  text: string;
  time: Date;
  own: boolean;
  status: MessageStatus;
  userName?: string;
  processed: boolean;
  messageType?: number;
  failed?: boolean;
}

interface HubMessage {
  id: string;
  chatId: string;
  text: string;
  time: string | Date;
  own?: boolean;
  userId?: string;
  status?: MessageStatus;
  userName?: string;
  messageType?: number;
}

interface DeleteChatResult {
  owner: string;
  userId: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  public userId = '';
  public readonly messages = signal<Message[]>([]);
  public readonly chats = signal<Chat[]>([]);
  public readonly activeChatId = signal('');
  public readonly activeUserId = signal('');
  public readonly isLoadingChats = signal(false);
  /** Идёт вход в чат и загрузка истории сообщений. */
  public readonly isLoadingMessages = signal(false);
  /** Причина последней неудачной отправки сообщения (для UI). */
  public readonly lastSendError = signal<string | null>(null);
  public readonly unreadMessageCount = computed(() =>
    this.chats().reduce((sum, chat) => sum + (chat.unreadMessagesCount || 0), 0)
  );

  public get isConnected() {
    return this.connectionManager.isConnected;
  }

  public get isReconnecting() {
    return this.connectionManager.isReconnecting;
  }

  private readonly messageIds = new Set<string>();
  private readonly readReceiptsSent = new Set<string>();
  private readonly joinedChatIds = new Set<string>();
  private readonly joiningChats = new Map<string, Promise<void>>();

  private handlersRegistered = false;
  private chatsLoaded = false;
  private loadChatsPromise: Promise<void> | null = null;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    private connectionManager: SignalRConnectionManager
  ) {
    this.registerHandlersOnce();

    this.connectionManager.onDisconnect(() => {
      // После обрыва связи группы на сервере теряются — их нужно пересоздать.
      this.joinedChatIds.clear();
      this.joiningChats.clear();
    });

    this.connectionManager.onConnected(() => {
      void this.rejoinChats();
    });
  }

  // ---------------------------------------------------------------------------
  // Подключение и список чатов
  // ---------------------------------------------------------------------------

  /**
   * Загружает список чатов ровно один раз. Параллельные вызовы
   * переиспользуют один и тот же промис.
   */
  public loadChats(force = false): Promise<void> {
    if (this.chatsLoaded && !force) {
      return Promise.resolve();
    }

    if (this.loadChatsPromise) {
      return this.loadChatsPromise;
    }

    this.loadChatsPromise = this.fetchChats().finally(() => {
      this.loadChatsPromise = null;
    });

    return this.loadChatsPromise;
  }

  /**
   * Добавляет чат в локальный список или обновляет существующий.
   * Нужен, чтобы чат, созданный из карточки пользователя, был виден
   * в списке сразу, не дожидаясь первого сообщения и перезагрузки списка.
   */
  public upsertChat(chat: Chat): void {
    if (!chat?.id) {
      return;
    }

    const normalized: Chat = {
      ...chat,
      unreadMessagesCount: chat.unreadMessagesCount ?? 0,
      status: chat.status ?? true
    };

    this.chats.update(chats => {
      const index = chats.findIndex(item => item.id === normalized.id);

      if (index === -1) {
        return [normalized, ...chats];
      }

      const next = [...chats];
      next[index] = { ...next[index], ...normalized };
      return next;
    });
  }

  private async fetchChats(): Promise<void> {
    this.isLoadingChats.set(true);

    try {
      const chats = await firstValueFrom(
        this.http.get<Chat[]>(`${this.base}/Chats/list`, { withCredentials: true })
      );

      this.chats.set(Array.isArray(chats) ? chats : []);
      this.chatsLoaded = true;

      await this.connectToAllChats();
    } catch (error) {
      // Не помечаем список загруженным: после восстановления авторизации
      // можно будет повторить запрос.
      console.warn('Не удалось загрузить список чатов', error);
    } finally {
      this.isLoadingChats.set(false);
    }
  }

  private async connectToAllChats(): Promise<void> {
    await this.connectionManager.ensureConnection();
    this.userId = this.connectionManager.userId ?? this.userId;

    await Promise.all(this.chats().map(chat => this.joinChat(chat.id)));
  }

  private async rejoinChats(): Promise<void> {
    this.joinedChatIds.clear();
    this.joiningChats.clear();

    if (!this.chatsLoaded) {
      return;
    }

    await this.connectToAllChats();

    const activeChatId = this.activeChatId();
    if (activeChatId) {
      await this.markChatAsRead(activeChatId);
    }
  }

  /**
   * Вход в группу чата. Повторные вызовы для одного chatId не создают
   * параллельных запросов JoinChat.
   */
  private joinChat(chatId: string): Promise<void> {
    if (!chatId || this.joinedChatIds.has(chatId)) {
      return Promise.resolve();
    }

    const pending = this.joiningChats.get(chatId);
    if (pending) {
      return pending;
    }

    const promise = this.connectionManager
      .invoke('JoinChat', chatId)
      .then(() => {
        this.joinedChatIds.add(chatId);
      })
      .catch(error => {
        console.warn('Не удалось подключиться к чату', chatId, error);
      })
      .finally(() => {
        this.joiningChats.delete(chatId);
      });

    this.joiningChats.set(chatId, promise);
    return promise;
  }

  /**
   * Делает чат активным, подключается к его группе и отмечает сообщения
   * прочитанными. История приходит через JoinChat.
   */
  public async openChat(chatId: string): Promise<void> {
    if (!chatId) {
      return;
    }

    this.isLoadingMessages.set(true);

    try {
      if (!this.chatsLoaded) {
        await this.loadChats();
      }

      let chat = this.chats().find(item => item.id === chatId);

      // Чат мог быть только что создан из карточки пользователя.
      if (!chat && this.chatsLoaded) {
        await this.loadChats(true);
        chat = this.chats().find(item => item.id === chatId);
      }

      this.activeChatId.set(chatId);

      if (chat) {
        this.activeUserId.set(chat.userId);
      }

      await this.joinChat(chatId);
      await this.markChatAsRead(chatId);
    } finally {
      this.isLoadingMessages.set(false);
    }
  }

  // ---------------------------------------------------------------------------
  // SignalR-обработчики
  // ---------------------------------------------------------------------------

  private registerHandlersOnce(): void {
    if (this.handlersRegistered) {
      return;
    }

    this.connectionManager.on('ReceiveMessage', (message: HubMessage) => {
      this.handleIncomingMessage(message);
    });

    this.connectionManager.on('MessageStatus', (status: { clientMessageId?: string; status?: MessageStatus }) => {
      if (!status?.clientMessageId) {
        return;
      }
      this.updateMessageStatus(status.clientMessageId, status.status ?? 'delivered');
    });

    this.connectionManager.on('StatusUpdate', (update: { messageId?: string; status?: MessageStatus }) => {
      if (!update?.messageId) {
        return;
      }
      this.updateMessageStatus(update.messageId, update.status ?? 'read');
    });

    this.handlersRegistered = true;
  }

  private handleIncomingMessage(raw: HubMessage): void {
    if (!raw?.id || !raw.chatId || this.messageIds.has(raw.id)) {
      return;
    }

    // История из JoinChat приходит, пока запрос ещё выполняется. Такие
    // сообщения не должны увеличивать счётчик непрочитанных.
    const isHistoryReplay = this.joiningChats.has(raw.chatId);

    const message: Message = {
      id: raw.id,
      chatId: raw.chatId,
      userId: raw.userId,
      text: raw.text ?? '',
      time: new Date(raw.time),
      own: raw.own ?? (!!raw.userId && raw.userId === this.userId),
      status: raw.status ?? 'delivered',
      userName: raw.userName,
      processed: false,
      messageType: raw.messageType
    };

    this.addMessage(message);
    this.updateChatLastMessage(message);

    if (message.chatId === this.activeChatId()) {
      if (!message.own) {
        void this.markAsRead(message.chatId, message.id);
      }
      this.setUnreadCount(message.chatId, 0);
      return;
    }

    if (!message.own && !isHistoryReplay) {
      this.incrementUnreadCount(message.chatId);
    }
  }

  // ---------------------------------------------------------------------------
  // Отправка и прочтение
  // ---------------------------------------------------------------------------

  public async sendMessage(text: string): Promise<boolean> {
    const chatId = this.activeChatId();
    const trimmed = text.trim();

    if (!chatId || !trimmed) {
      return false;
    }

    const clientMessageId = generateGUID();
    const message: Message = {
      id: clientMessageId,
      chatId,
      text: trimmed,
      own: true,
      time: new Date(),
      status: 'pending',
      processed: false
    };

    this.addMessage(message);
    this.updateChatLastMessage(message);
    this.lastSendError.set(null);

    try {
      await this.connectionManager.invoke('SendMessage', chatId, clientMessageId, trimmed);
      this.updateMessageStatus(clientMessageId, 'delivered');
      return true;
    } catch (error) {
      console.error('Не удалось отправить сообщение', error);

      const rawMessage = error instanceof Error ? error.message : String(error);
      this.lastSendError.set(
        rawMessage.includes('взаимный лайк')
          ? 'Нужен взаимный лайк, чтобы написать этому пользователю'
          : 'Не удалось отправить сообщение. Проверьте соединение.'
      );

      this.messages.update(messages =>
        messages.map(item =>
          item.id === clientMessageId ? { ...item, failed: true } : item
        )
      );
      return false;
    }
  }

  public async markAsRead(chatId: string, messageId: string): Promise<void> {
    if (!chatId || !messageId || this.readReceiptsSent.has(messageId)) {
      return;
    }

    if (!this.connectionManager.isConnected()) {
      return;
    }

    this.readReceiptsSent.add(messageId);

    try {
      await this.connectionManager.invoke('MarkAsRead', chatId, messageId);
      this.updateMessageStatus(messageId, 'read');
    } catch (error) {
      this.readReceiptsSent.delete(messageId);
      console.warn('Не удалось отметить сообщение прочитанным', error);
    }
  }

  public async markChatAsRead(chatId: string): Promise<void> {
    if (!chatId) {
      return;
    }

    this.setUnreadCount(chatId, 0);

    const unreadMessages = this.messages().filter(
      message => message.chatId === chatId && !message.own && message.status !== 'read'
    );

    for (const message of unreadMessages) {
      await this.markAsRead(chatId, message.id);
    }
  }

  // ---------------------------------------------------------------------------
  // Действия с чатом
  // ---------------------------------------------------------------------------

  public async deleteChat(chatId: string): Promise<boolean> {
    if (!chatId) {
      return false;
    }

    try {
      const result = await firstValueFrom(
        this.http.get<DeleteChatResult>(`${this.base}/Chats/delete/${chatId}`, {
          withCredentials: true
        })
      );

      void this.stopChat(result.owner, result.userId);
      this.chats.update(chats => chats.filter(chat => chat.id !== chatId));
      this.removeChatMessages(chatId);

      if (this.activeChatId() === chatId) {
        this.activeChatId.set('');
        this.activeUserId.set('');
      }

      return true;
    } catch (error) {
      console.error('Не удалось удалить чат', error);
      return false;
    }
  }

  public async analizeChat(chatId: string): Promise<void> {
    if (!chatId) {
      return;
    }

    try {
      await firstValueFrom(
        this.http.post(`${this.base}/chats/ai/${chatId}`, null, { withCredentials: true })
      );
    } catch (error) {
      console.error('Не удалось проанализировать чат', error);
    }
  }

  public async startBotDialog(botUserId: string): Promise<void> {
    try {
      await this.connectionManager.invoke('StartBotDialog', botUserId);
    } catch (error) {
      console.warn('Не удалось запустить диалог с ботом', error);
    }
  }

  public async stopChat(owner: string, userId: string): Promise<void> {
    try {
      await this.connectionManager.invoke('StopChat', owner, userId);
    } catch (error) {
      console.warn('Не удалось остановить диалог', error);
    }
  }

  public reset(): void {
    this.chatsLoaded = false;
    this.loadChatsPromise = null;
    this.joinedChatIds.clear();
    this.joiningChats.clear();
    this.messageIds.clear();
    this.readReceiptsSent.clear();

    this.messages.set([]);
    this.chats.set([]);
    this.activeChatId.set('');
    this.activeUserId.set('');
    this.userId = '';

    void this.connectionManager.reset();
  }

  // ---------------------------------------------------------------------------
  // Вспомогательные методы
  // ---------------------------------------------------------------------------

  public readonly activeChat = computed(() =>
    this.chats().find(chat => chat.id === this.activeChatId()) ?? null
  );

  public readonly otherUser = computed(() => {
    const chat = this.activeChat();
    if (!chat) {
      return undefined;
    }

    return {
      userId: chat.userId,
      name: chat.name,
      avatar: chat.avatar
    };
  });

  public readonly filteredMessages = computed(() => {
    const chatId = this.activeChatId();
    return this.messages().filter(message => message.chatId === chatId);
  });

  private addMessage(message: Message): void {
    if (this.messageIds.has(message.id)) {
      return;
    }

    this.messageIds.add(message.id);
    this.messages.update(messages => [...messages, message]);
  }

  private removeChatMessages(chatId: string): void {
    const remaining: Message[] = [];

    for (const message of this.messages()) {
      if (message.chatId === chatId) {
        this.messageIds.delete(message.id);
        this.readReceiptsSent.delete(message.id);
      } else {
        remaining.push(message);
      }
    }

    this.messages.set(remaining);
  }

  private updateMessageStatus(messageId: string, status: MessageStatus): void {
    this.messages.update(messages =>
      messages.map(message => (message.id === messageId ? { ...message, status } : message))
    );
  }

  private updateChatLastMessage(message: Message): void {
    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === message.chatId
          ? { ...chat, lastMessage: message.text, lastMessageTime: message.time }
          : chat
      )
    );
  }

  private incrementUnreadCount(chatId: string): void {
    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === chatId
          ? { ...chat, unreadMessagesCount: (chat.unreadMessagesCount || 0) + 1 }
          : chat
      )
    );
  }

  private setUnreadCount(chatId: string, count: number): void {
    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === chatId ? { ...chat, unreadMessagesCount: count } : chat
      )
    );
  }
}
