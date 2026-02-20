// UserStatusService.ts
import { Injectable, signal, computed } from '@angular/core';
import { SignalRConnectionManager } from './SignalRConnectionManager';

interface UserStatus {
  userId: string;
  isOnline: boolean;
  lastSeen?: Date;
}

@Injectable({ providedIn: 'root' })
export class UserStatusService {
  public userId: string = "";
  public onlineUsers = signal<Set<string>>(new Set());
  public userStatuses = signal<Map<string, UserStatus>>(new Map());
  public onlineCount = computed(() => this.onlineUsers().size);

  constructor(private connectionManager: SignalRConnectionManager) { }


  async connect(userId: string): Promise<void> {    
    await this.connectionManager.ensureConnection(); // ✅ Ждем подключения
    //this.userId = this.connectionManager.userId ?? "";
    await this.registerHandlers();
    this.userId = this.connectionManager.userId ?? userId;
    await this.requestOnlineUsers();
    //await this.getUserStatus(this.userId);
  }

  // ✅ Единственный метод инициализации — регистрация обработчиков
  private registerHandlers(): void {
    this.connectionManager.on('userStatusChanged', (data: { userId: string; status: boolean }) => {
      console.log('📥 User status changed:', data);
      this.updateUserStatus(data.userId, data.status);
    });

    this.connectionManager.on('OnlineUsersList', (userIds: string[]) => {
      console.log('📥 Online users list:', userIds);
      this.setOnlineUsers(userIds);
    });

    console.log('✅ UserStatusService registered handlers');
  }

  // ✅ Методы API
  async getUserStatus(userId: string): Promise<boolean> {
    try {
      console.log('GetUserStatus ' + userId);
      return await this.connectionManager.invoke('GetUserStatus', userId);
    } catch (err) {
      console.error('❌ Error getting user status:', err);
      return false;
    }
  }

  async getOnlineUsers(): Promise<string[]> {
    try {
      return await this.connectionManager.invoke('GetOnlineUsers');
    } catch (err) {
      console.error('❌ Error getting online users:', err);
      return [];
    }
  }

  async requestOnlineUsers(): Promise<void> {
    const users = await this.getOnlineUsers();
    this.setOnlineUsers(users);
  }

  isUserOnline(userId: string): boolean {
    return this.onlineUsers().has(userId);
  }

  getUserPresence(userId: string): UserStatus | undefined {
    return this.userStatuses().get(userId);
  }

  // ✅ Внутренние методы (без изменений)
  private updateUserStatus(userId: string, isOnline: boolean): void {
    const statuses = new Map(this.userStatuses());

    if (isOnline) {
      this.onlineUsers.update(users => new Set(users).add(userId));
      statuses.set(userId, { userId, isOnline: true });
    } else {
      this.onlineUsers.update(users => {
        const newSet = new Set(users);
        newSet.delete(userId);
        return newSet;
      });
      statuses.set(userId, { userId, isOnline: false, lastSeen: new Date() });
    }

    this.userStatuses.set(statuses);
  }

  private setOnlineUsers(userIds: string[]): void {
    this.onlineUsers.set(new Set(userIds));
    const statuses = new Map<string, UserStatus>();
    userIds.forEach(id => statuses.set(id, { userId: id, isOnline: true }));
    this.userStatuses.set(statuses);
  }

  // ✅ Очистка состояния при отключении
  private clearState(): void {
    console.log('🧹 Clearing UserStatusService state on disconnect');
    this.onlineUsers.set(new Set());
    this.userStatuses.set(new Map());
  }
}
