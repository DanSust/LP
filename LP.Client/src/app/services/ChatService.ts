// src/app/services/chat.service.ts
import { Injectable, signal, computed, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { take, switchMap, catchError, map, filter } from 'rxjs/operators';
import { SignalRConnectionManager } from './SignalRConnectionManager';
import { generateGUID } from '../common/GUID';
import { API_BASE_URL } from '../app.config';

type MessageStatus = 'pending' | 'delivered' | 'read';

export interface Chat {
  id: string;
  name: string;
  avatar?: string;
  userId: string;
  lastMessage?: string;
  lastMessageTime?: Date;
  unreadMessagesCount: number;
  status: boolean;
}

export interface Message extends Omit<ChatMessage, 'userId'> {
  userId?: string;
}

interface ChatMessage {
  id: string;
  chatId: string;
  userId?: string;
  text: string;
  time: Date;
  own: boolean;
  status: MessageStatus;
  userName?: string;
  processed: boolean;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  public userId: string = "";
  public messages = signal<Message[]>([]);
  public chats = signal<Chat[]>([]);
  public activeChatId = signal<string>('');
  public activeUserId = signal<string>('');
  public unreadMessageCount = computed(() =>
    this.chats().reduce((sum, chat) => sum + (chat.unreadMessagesCount || 0), 0)
  );

  private messageIds = new Set<string>();
  private readReceiptsSent = new Set<string>();
  private handlersRegistered = false; // Флаг для предотвращения дублирования

  private joinedChatIds = new Set<string>();

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    private connectionManager: SignalRConnectionManager
  ) {
    //console.log('✅ ChatService initialized');
    this.setupConnectionManager();
    this.registerHandlersOnce(); // РЕГИСТРАЦИЯ ОДИН РАЗ
  }

  // ЕДИНОВРЕМЕННАЯ регистрация обработчиков
  private registerHandlersOnce(): void {
    if (this.handlersRegistered) return;

    this.connectionManager.on('ReceiveMessage', (message: ChatMessage) => {
      //console.log('📥 Received message:', message);
      this.addMessage({
        ...message,
        time: new Date(message.time),
        userId: message.userId
      });
      this.updateChatLastMessage(message);

      // Увеличиваем счетчик только если чат не активен
      //console.log(message.chatId, this.activeChatId());
      //if (message.chatId !== this.activeChatId()) {
      if (!message.own) {
        this.incrementUnreadCount(message.chatId, message.id);
      }
    });

    this.connectionManager.on('MessageStatus', (status: { clientMessageId: string; status: MessageStatus }) => {
      //console.log('📥 Message status:', status);
      this.updateMessageStatus(status.clientMessageId, status.status);
    });

    this.connectionManager.on('StatusUpdate', (update: { messageId: string; status: MessageStatus }) => {
      //console.log('📥 Status update:', update);
      this.updateMessageStatus(update.messageId, update.status);
    });

    this.handlersRegistered = true;
    console.log('✅ SignalR handlers registered ONCE');
  }

  // Подключение ко ВСЕМ чатам
  async connectToAllChats(): Promise<void> {
    await this.connectionManager.ensureConnection();
    this.userId = this.connectionManager.userId ?? "";

    const chats = this.chats();
    for (const chat of chats) {
      await this.joinChat(chat.id);
    }
    //console.log(`✅ Connected to ${chats.length} chats`);
  }

  // Обновленный connect - теперь только для выбора активного чата
  async connect(chatId: string, userName: string): Promise<void> {
    this.activeChatId.set(chatId);
    await this.connectionManager.ensureConnection();
    this.userId = this.connectionManager.userId ?? "";

    // Уже подключены ко всем чатам, дополнительно подключаемся к этому
    await this.joinChat(chatId);
    //this.loadMessages(chatId);
  }

  private async joinChat(chatId: string): Promise<void> {
    if (this.joinedChatIds.has(chatId)) {
      //console.log('Already joined chat:', chatId);
      return;
    }
    try {
      await this.connectionManager.invoke('JoinChat', chatId);
      this.joinedChatIds.add(chatId);
      //console.log('✅ Joined chat:', chatId);
    } catch (err) {
      console.error('❌ Error joining chat:', err);
      throw err;
    }
  }

  async leaveChat(chatId: string): Promise<void> {
    try {
      //await this.connectionManager.invoke('LeaveChat', chatId);
      //console.log('✅ Left chat:', chatId);
    } catch (err) {
      console.error('❌ Error leaving chat:', err);
    }
  }

  loadChats(): void {
    this.http.get<Chat[]>(`${this.base}/Chats/list`, { withCredentials: true })
      .pipe(catchError(err => {
        console.error('❌ Error loading chats:', err);
        return of([]);
      }))
      .subscribe({
        next: async (chats) => {
          //console.log('Loaded chats:', chats);
          this.chats.set(chats);

          // ПОДКЛЮЧАЕМСЬ КО ВСЕМ ЧАТАМ
          await this.connectToAllChats();
        }
      });
  }

  clearChatData(chatId: string): void {
    // Очищаем только сообщения конкретного чата из Set'ов
    const messagesInChat = this.messages().filter(m => m.chatId === chatId);

    for (const msg of messagesInChat) {
      this.messageIds.delete(msg.id);
      this.readReceiptsSent.delete(msg.id);
    }
  }

  deleteChat(chatId: string): void {    
    this.http.get<any>(`${this.base}/Chats/delete/${chatId}`, { withCredentials: true }).
      subscribe({
        next: (result) => {
          //console.log(result);
          this.stopChat(result.owner, result.userId);
          this.clearMessages();
          this.chats.update(chats => chats.filter(c => c.id !== chatId));
        }
      });
  }

  analizeChat(chatId: string): void {
    this.http.post<any>(`${this.base}/chats/ai/${chatId}`, null, { withCredentials: true })
      .subscribe({
        next: (response) => {
          console.log(response)
        },
        error: (error) => {          
            console.error('Ошибка:', error);
          }
      });
  }

  loadMessages(chatId: string): void {
    this.clearChatData(chatId); // Очищаем данные старого чата
    this.clearMessages(); // Очищаем messages
    this.http.get<any[]>(`${this.base}/Chats/${chatId}`, { withCredentials: true })
      .subscribe({
        next: (messages) => {
          const processed = messages.map(msg => ({
            ...msg,
            time: new Date(msg.time),
            own: msg.userId === this.userId,
            status: msg.status || 'delivered'
          }));
          this.messages.set(processed);

          this.synchronizeUnreadCount(chatId);
        },
        error: (err) => console.error('❌ Error loading messages:', err)
      });
  }

  filteredMessages = computed(() => {
    const chatId = this.activeChatId();
    return this.messages().filter(msg => msg.chatId === chatId)
  });

  otherUser = computed(() => {
    const chatId = this.activeChatId();
    const chat = this.chats().find(c => c.id === chatId);
    if (!chat) return undefined;
    return {
      userId: chat.userId,
      name: chat.name,
      avatar: chat.avatar
    };
    return this.messages().find(msg => msg.chatId === chatId && msg.userId !== this.userId);
  });

  public activeChat = computed(() =>
    this.chats().find(c => c.id === this.activeChatId()) || null
  );

  async sendMessage(text: string): Promise<void> {
    if (!this.connectionManager.isConnected()) {
      console.error('❌ Not connected');
      return;
    }

    const clientMessageId = generateGUID();
    const message: Message = {
      id: clientMessageId,
      text: text.trim(),
      own: true,
      time: new Date(),
      status: 'pending',
      processed: false,
      chatId: this.activeChatId()
    };

    this.addMessage(message);
    this.updateChatLastMessage(message);

    try {
      await this.connectionManager.invoke('SendMessage', this.activeChatId(), clientMessageId, text);
      //console.log('📤 Message sent:', clientMessageId);
    } catch (err) {
      console.error('❌ Error sending message:', err);
      this.updateMessageStatus(clientMessageId, 'pending');
    }
  }

  async markAsRead(messageId: string): Promise<void> {
    if (!this.connectionManager.isConnected() || this.readReceiptsSent.has(messageId)) {
      console.error('❌ Error marking as read:', messageId);
      return;
    }

    this.readReceiptsSent.add(messageId);
    try {
      await this.connectionManager.invoke('MarkAsRead', this.activeChatId(), messageId);
      this.updateMessageStatus(messageId, 'read');
      //console.log('📤 Sent read receipt:', messageId);
    } catch (err) {
      console.error('❌ Error marking as read:', err);
      this.readReceiptsSent.delete(messageId);
    }
  }

  async markChatAsRead(chatId: string): Promise<void> {
    // Обнуляем локальный счетчик
    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === chatId
          ? { ...chat, unreadMessagesCount: 0 }
          : chat
      )
    );

    // Отправляем read receipt для последнего сообщения
    //const lastNonOwnMessage = [...this.messages()]
    //  .reverse()
    //  .find(msg => msg.chatId === chatId && !msg.own);

    //if (lastNonOwnMessage) {
    //  await this.markAsRead(lastNonOwnMessage.id);
    //}

    const unreadMessages = this.messages().filter(
      msg => msg.chatId === chatId && !msg.own && msg.status !== 'read'
    );

    for (const msg of unreadMessages) {
      await this.markAsRead(msg.id);
    }
  }

  private incrementUnreadCount(chatId: string, msgId: string): void {    
    const lastMessage = this.messages().find(msg =>
      msg.id === msgId && !msg.own
    );
    //console.log(lastMessage);    
    if (!lastMessage || lastMessage?.status === 'read') {
      //console.log('Message already read, not incrementing count');
      return;
    }

    //console.log('incrementUnreadCount ' + chatId);

    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === chatId 
          ? { ...chat, unreadMessagesCount: (chat.unreadMessagesCount || 0) + 1 }
          : chat
      )
    );
  }

  private synchronizeUnreadCount(chatId: string): void {
    const actualUnreadCount = this.messages().filter(msg =>
      msg.chatId === chatId && !msg.own && msg.status !== 'read'
    ).length;

    this.chats.update(chats =>
      chats.map(chat =>
        chat.id === chatId
          ? { ...chat, unreadMessagesCount: actualUnreadCount }
          : chat
      )
    );
  }

  private updateMessageStatus(messageId: string, status: MessageStatus): void {
    this.messages.update(msgs => msgs.map(msg => msg.id === messageId ? { ...msg, status } : msg));
  }

  private addMessage(message: Message): void {
    if (this.messageIds.has(message.id)) return;
    this.messageIds.add(message.id);
    this.messages.update(msgs => [...msgs, message]);
  }

  private updateChatLastMessage(message: Message): void {
    this.chats.update(chats => chats.map(chat =>
      chat.id === message.chatId
        ? { ...chat, lastMessage: message.text, lastMessageTime: message.time }
        : chat
    ));
  }

  private setupConnectionManager(): void {
    this.connectionManager.onDisconnect(() => {
      console.warn('🔌 Connection lost, clearing state');
      this.clearState();
      this.handlersRegistered = false; // Сбрасываем флаг при отключении
    });
  }

  private clearMessages(): void {
    this.messages.set([]);
    this.messageIds.clear();
  }

  private clearState(): void {
    //console.log('🧹 Clearing ChatService state');
    this.clearMessages();
    this.readReceiptsSent.clear();
    this.chats.set([]);
  }

  async startBotDialog(owner: string, user: string): Promise<void> {
    await this.connectionManager.ensureConnection();
    await this.connectionManager.invoke('StartBotDialog', user);
    //console.log('🤖 Bot dialog started with chat:', owner, user);
  }

  async stopChat(owner: string, userId: string): Promise<void> {
    await this.connectionManager.ensureConnection();
    await this.connectionManager.invoke('StopChat', owner, userId);    
  }
}
